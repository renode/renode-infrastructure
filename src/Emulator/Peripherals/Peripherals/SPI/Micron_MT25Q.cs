//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Micron_MT25Q : ISPIPeripheral, IDoubleWordPeripheral, IBytePeripheral
    {
        public Micron_MT25Q(MicronFlashSize size)
        {
            if(!Enum.IsDefined(typeof(MicronFlashSize), size))
            {
                throw new ConstructionException($"Undefined memory size: {size}");
            }
            volatileConfigurationRegister = new ByteRegister(this, 0xfb).WithFlag(3, name: "XIP");
            nonVolatileConfigurationRegister = new WordRegister(this, 0xffff).WithFlag(0, out numberOfAddressBytes, name: "addressWith3Bytes");
            statusRegister = new ByteRegister(this).WithFlag(1, out enable, name: "volatileControlBit");
            fileBackendSize = (uint)size;
            isCustomFileBackend = false;
            dataBackend = DataStorage.Create(fileBackendSize, 0xFF);
            deviceData = GetDeviceData();
        }

        public void UseDataFromFile(string imageFile, bool persistent = false)
        {
            if(isCustomFileBackend)
            {
                throw new RecoverableException("Cannot override existing file storage.");
            }
            if(!File.Exists(imageFile))
            {
                throw new RecoverableException($"File {imageFile} does not exist.");
            }
            var fileLength = new FileInfo(imageFile).Length;
            if(fileLength > fileBackendSize)
            {
                this.Log(LogLevel.Warning, "The provided file is bigger than the configured memory size and as a result part of the file's data will not be accessible in the emulation.");
            }
            dataBackend = DataStorage.Create(imageFile, fileBackendSize, persistent);
            isCustomFileBackend = true;
        }

        public void FinishTransmission()
        {
            switch(state)
            {
                case State.RecognizeOperation:
                case State.AccumulateCommandAddressBytes:
                case State.AccumulateNoDataCommandAddressBytes:
                    this.Log(LogLevel.Warning, "Transmission finished in the unexpected state: {0}", state);
                    break;
            }
            // If an operation has at least 1 data byte or more than 0 address bytes,
            // we can clear the write enable flag only when we are finishing a transmission.
            switch(currentOperation)
            {
                case Operation.Program:
                case Operation.Erase:
                case Operation.WriteVolatileConfigurationRegister:
                    enable.Value = false;
                    break;
            }
            address = null;
            currentAddressByte = 0;
            commandBytesHandled = 0;
            commandAddressBytesCount = 0;
            state = State.RecognizeOperation;
            currentOperation = Operation.None;
        }

        public void Reset()
        {
            volatileConfigurationRegister.Reset();
            nonVolatileConfigurationRegister.Reset();
            FinishTransmission();
        }

        public void Dispose()
        {
            dataBackend.Dispose();
        }

        public byte Transmit(byte data)
        {
            switch(state)
            {
                case State.RecognizeOperation:
                    // When the command is decoded, depending on the operation we will either start accumulating address bytes
                    // or immediately handle the command bytes
                    currentOperation = RecognizeOperation(data);
                    break;
                case State.AccumulateCommandAddressBytes:
                    // Warning: `commandExecutionAddress` value is intentionally `0` during the aggregation process
                    commandExecutionAddress = AccumulateAddressBytes(data, State.HandleCommand);
                    break;
                case State.AccumulateNoDataCommandAddressBytes:
                    // Warning: `commandExecutionAddress` value is intentionally `0` during the aggregation process
                    commandExecutionAddress = AccumulateAddressBytes(data, State.HandleNoDataCommand);
                    break;
                case State.HandleCommand:
                    // Process the remaining command bytes
                    return HandleCommand(data);
            }

            // Warning: commands without data require immediate handling after the address was accumulated
            if(state == State.HandleNoDataCommand)
            {
                HandleNoDataCommand();
            }
            return 0;
        }

        public uint ReadDoubleWord(long localOffset)
        {
            if(localOffset + 4 > fileBackendSize)
            {
                this.Log(LogLevel.Error, "Cannot read from address 0x{0:X} because it is bigger than configured memory size.", localOffset);
                return 0;
            }
            dataBackend.Position = localOffset;
            return BitHelper.ToUInt32(dataBackend.ReadBytes(4), 0, 4, false);
        }

        public void WriteDoubleWord(long localOffset, uint value)
        {
            this.Log(LogLevel.Error, "Illegal write to flash in XIP mode.");
        }

        public byte ReadByte(long localOffset)
        {
            if(localOffset >= fileBackendSize)
            {
                this.Log(LogLevel.Error, "Cannot read from address 0x{0:X} because it is bigger than configured memory size.", localOffset);
                return 0;
            }
            dataBackend.Position = localOffset;
            return (byte)dataBackend.ReadByte();
        }

        public void WriteByte(long localOffset, byte value)
        {
            this.Log(LogLevel.Error, "Illegal write to flash in XIP mode.");
        }

        private byte[] GetDeviceData()
        {
            byte capacityCode = 0;
            switch(fileBackendSize)
            {
                case (int)MicronFlashSize.Gb_2:
                    capacityCode = 0x22;
                    break;
                case (int)MicronFlashSize.Gb_1:
                    capacityCode = 0x21;
                    break;
                case (int)MicronFlashSize.Mb_512:
                    capacityCode = 0x20;
                    break;
                case (int)MicronFlashSize.Mb_256:
                    capacityCode = 0x19;
                    break;
                case (int)MicronFlashSize.Mb_128:
                    capacityCode = 0x18;
                    break;
                case (int)MicronFlashSize.Mb_64:
                    capacityCode = 0x17;
                    break;
                default:
                    throw new ConstructionException($"Cannot retrieve capacity code for undefined memory size: 0x{fileBackendSize:X}");
            }

            var data = new byte[20];
            data[0] = ManufacturerID;
            data[1] = MemoryType;
            data[2] = capacityCode;
            data[3] = RemainingIDBytes;
            data[4] = ExtendedDeviceID;
            data[5] = DeviceConfiguration;
            // unique ID code (bytes 7:20)
            return data;
        }

        private Operation RecognizeOperation(byte firstByte)
        {
            // The type of the command is distinguished by its first byte.
            if(TryDecodeFirstCommandByte(firstByte, out var operation))
            {
                if(commandAddressBytesCount > 0)
                {
                    address = new byte[commandAddressBytesCount];
                }
            }
            return operation;
        }

        private bool TryDecodeFirstCommandByte(byte firstByte, out Operation decodedOperation)
        {
            decodedOperation = Operation.None;
            commandAddressBytesCount = 0;
            state = State.HandleCommand;
            switch(firstByte)
            {
                case (byte)Commands.ReadID:
                    decodedOperation = Operation.ReadID;
                    break;
                case (byte)Commands.Read:
                case (byte)Commands.FastRead:
                case (byte)Commands.DualOutputFastRead:
                case (byte)Commands.DualInputOutputFastRead:
                case (byte)Commands.QuadOutputFastRead:
                case (byte)Commands.QuadInputOutputFastRead:
                case (byte)Commands.DtrFastRead:
                case (byte)Commands.DtrDualOutputFastRead:
                case (byte)Commands.DtrDualInputOutputFastRead:
                case (byte)Commands.DtrQuadOutputFastRead:
                case (byte)Commands.DtrQuadInputOutputFastRead:
                case (byte)Commands.QuadInputOutputWordRead:
                    decodedOperation = Operation.Read;
                    commandAddressBytesCount = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateCommandAddressBytes;
                    break;
                case (byte)Commands.ReadStatusRegister:
                    decodedOperation = Operation.ReadStatusRegister;
                    break;
                case (byte)Commands.PageProgram:
                case (byte)Commands.DualInputFastProgram:
                case (byte)Commands.ExtendedDualInputFastProgram:
                case (byte)Commands.QuadInputFastProgram:
                case (byte)Commands.ExtendedQuadInputFastProgram:
                    decodedOperation = Operation.Program;
                    commandAddressBytesCount = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateCommandAddressBytes;
                    break;
                case (byte)Commands.WriteEnable:
                    enable.Value = true;
                    break;
                case (byte)Commands.WriteDisable:
                    enable.Value = false;
                    break;
                case (byte)Commands.SectorErase:
                    decodedOperation = Operation.Erase;
                    commandAddressBytesCount = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateNoDataCommandAddressBytes;
                    break;
                case (byte)Commands.ReadVolatileConfigurationRegister:
                    decodedOperation = Operation.ReadVolatileConfigurationRegister;
                    break;
                case (byte)Commands.WriteVolatileConfigurationRegister:
                    decodedOperation = Operation.WriteVolatileConfigurationRegister;
                    break;
                case (byte)Commands.ReadNonVolatileConfigurationRegister:
                    decodedOperation = Operation.ReadNonVolatileConfigurationRegister;
                    break;
                case (byte)Commands.WriteNonVolatileConfigurationRegister:
                    decodedOperation = Operation.WriteNonVolatileConfigurationRegister;
                    break;
                default:
                    this.Log(LogLevel.Error, "Command decoding failed on byte: {0}.", firstByte);
                    return false;
            }
            return true;
        }

        private uint AccumulateAddressBytes(byte data, State nextState)
        {
            uint result = 0;
            if(TryGetAccumulatedAddress(data, out result))
            {
                state = nextState;
            }
            return result;
        }

        private bool TryGetAccumulatedAddress(byte data, out uint result)
        {
            result = 0;
            address[currentAddressByte] = data;
            currentAddressByte++;
            if(currentAddressByte == commandAddressBytesCount)
            {
                result = BitHelper.ToUInt32(address, 0, commandAddressBytesCount, false);
                return true;
            }
            return false;
        }

        private byte HandleCommand(byte data)
        {
            byte result = 0;
            switch(currentOperation)
            {
                case Operation.Read:
                    result = ReadFromMemory();
                    break;
                case Operation.ReadID:
                    if(commandBytesHandled < deviceData.Length)
                    {
                        result = deviceData[commandBytesHandled];
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Trying to read beyond the length of the device ID table.");
                        result = 0;
                    }
                    break;
                case Operation.ReadStatusRegister:
                    // The documentation states that at least 1 byte will be read
                    // If more than 1 byte is read, the same byte is returned
                    result = statusRegister.Value;
                    break;
                case Operation.Program:
                    if(enable.Value)
                    {
                        WriteToMemory(data);
                        result = data;
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Memory write operations are disabled.");
                    }
                    break;
                case Operation.ReadVolatileConfigurationRegister:
                    // The documentation states that at least 1 byte will be read
                    // If more than 1 byte is read, the same byte is returned
                    result = volatileConfigurationRegister.Value;
                    break;
                case Operation.WriteVolatileConfigurationRegister:
                    if(enable.Value)
                    {
                        volatileConfigurationRegister.Write(0, data);
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Volatile register writes are disabled.");
                    }
                    break;
                case Operation.ReadNonVolatileConfigurationRegister:
                    // The documentation states that at least 2 bytes will be read
                    // After all 16 bits of the register have been read, 0 is returned
                    result = 0;
                    // `commandBytesHandled` is incremented and the end of this method and we want its value to match the comment above
                    if((commandBytesHandled + 1) <= 2)
                    {
                        result = (byte)BitHelper.GetValue(nonVolatileConfigurationRegister.Value, (int)commandBytesHandled * 8, 8);
                    }
                    break;
                case Operation.WriteNonVolatileConfigurationRegister:
                    // `commandBytesHandled` is incremented and the end of this method and we want its value to match the log message below
                    if((commandBytesHandled + 1) > 2)
                    {
                        this.Log(LogLevel.Error, "Operation {0} is longer than expected 2 bytes.", Operation.WriteNonVolatileConfigurationRegister);
                        break;
                    }
                    if(enable.Value)
                    {
                        nonVolatileConfigurationRegister.Write((int)commandBytesHandled * 8, data);
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Nonvolatile register writes are disabled.");
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled operation encountered while processing command bytes: {0}", currentOperation);
                    break;
            }
            commandBytesHandled++;
            return result;
        }

        private void HandleNoDataCommand()
        {
            // The documentation describes more commands that don't have any data bytes (just code + address)
            // but at the moment we have implemented only one
            switch(currentOperation)
            {
                case Operation.Erase:
                    if(enable.Value)
                    {
                        EraseSector();
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Sector erase operations are disabled.");
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Encountered unexpected command: {0}", currentOperation);
                    break;
            }
        }

        private void EraseSector()
        {
            if(commandExecutionAddress >= fileBackendSize)
            {
                this.Log(LogLevel.Error, "Cannot erase memory because current address 0x{0:X} is bigger than configured memory size.", commandExecutionAddress);
                return;
            }
            var segment = new byte[SegmentSize];
            for(var i = 0; i < SegmentSize; i++)
            {
                segment[i] = EmptySegment;
            }
            // The documentations states that on erase the operation address is
            // aligned to the segment size
            dataBackend.Position = SegmentSize * (commandExecutionAddress / SegmentSize);
            dataBackend.Write(segment, 0, SegmentSize);
        }

        private void WriteToMemory(byte val)
        {
            if(commandExecutionAddress + commandBytesHandled > fileBackendSize)
            {
                this.Log(LogLevel.Error, "Cannot write to address 0x{0:X} because it is bigger than configured memory size.", commandExecutionAddress);
                return;
            }
            dataBackend.Position = commandExecutionAddress + commandBytesHandled;
            dataBackend.WriteByte(val);
        }

        private byte ReadFromMemory()
        {
            if(commandExecutionAddress + commandBytesHandled > fileBackendSize)
            {
                this.Log(LogLevel.Error, "Cannot read from address 0x{0:X} because it is bigger than configured memory size.", commandExecutionAddress);
                return 0;
            }
            dataBackend.Position = commandExecutionAddress + commandBytesHandled;
            return (byte)dataBackend.ReadByte();
        }

        private State state;
        private byte[] address;
        private Stream dataBackend;
        private int currentAddressByte;
        private uint commandBytesHandled;
        private bool isCustomFileBackend;
        private Operation currentOperation;
        private uint commandExecutionAddress;
        private int commandAddressBytesCount;

        private readonly byte[] deviceData;
        private readonly uint fileBackendSize;
        private readonly int SegmentSize = 64.KB();
        private readonly IFlagRegisterField enable;
        private readonly ByteRegister statusRegister;
        private readonly IFlagRegisterField numberOfAddressBytes;
        private readonly ByteRegister volatileConfigurationRegister;
        private readonly WordRegister nonVolatileConfigurationRegister;

        private const byte EmptySegment = 0xff;

        private const byte ManufacturerID = 0x20;
        private const byte RemainingIDBytes = 0x10;
        private const byte MemoryType = 0xBB;           // device voltage: 1.8V
        private const byte DeviceConfiguration = 0x0;   // standard
        private const byte DeviceGeneration = 0x1;      // 2nd generation
        private const byte ExtendedDeviceID = DeviceGeneration << 6;

        private enum Commands : byte
        {
            // Software RESET Operations
            ResetEnable = 0x66,
            ResetMemory = 0x99,

            // READ ID Operations
            ReadID = 0x9F,
            MultipleIoReadID = 0xAF,
            ReadSerialFlashDiscoveryParameter = 0x5A,

            // READ MEMORY Operations
            Read = 0x03,
            FastRead = 0x0B,
            DualOutputFastRead = 0x3B,
            DualInputOutputFastRead = 0xBB,
            QuadOutputFastRead = 0x6B,
            QuadInputOutputFastRead = 0xEB,
            DtrFastRead = 0x0D,
            DtrDualOutputFastRead = 0x3D,
            DtrDualInputOutputFastRead = 0xBD,
            DtrQuadOutputFastRead = 0x6D,
            DtrQuadInputOutputFastRead = 0xED,
            QuadInputOutputWordRead = 0xE7,

            // READ MEMORY Operations with 4-Byte Address
            Read4byte = 0x13,
            FastRead4byte = 0x0C,
            DualOutputFastRead4byte = 0x3C,
            DualInputOutputFastRead4byte = 0xBC,
            QuadOutputFastRead4byte = 0x6C,
            QuadInputOutputFastRead4byte = 0xEC,
            DtrFastRead4byte = 0x0E,
            DtrDualInputOutputFastRead4byte = 0xBE,
            DtrQuadInputOutputFastRead4byte = 0xEE,

            // WRITE Operations
            WriteEnable = 0x06,
            WriteDisable = 0x04,

            // READ REGISTER Operations
            ReadStatusRegister = 0x05,
            ReadFlagStatusRegister = 0x70,
            ReadNonVolatileConfigurationRegister = 0xB5,
            ReadVolatileConfigurationRegister = 0x85,
            ReadEnhancedVolatileConfigurationRegister = 0x65,
            ReadExtendedAddressRegister = 0xC8,
            ReadGeneralPurposeReadRegister = 0x96,

            // WRITE REGISTER Operations
            WriteStatusRegister = 0x01,
            WriteNonVolatileConfigurationRegister = 0xB1,
            WriteVolatileConfigurationRegister = 0x81,
            WriteEnhancedVolatileConfigurationRegister = 0x61,
            WriteExtendedAddressRegister = 0xC5,

            // CLEAR FLAG STATUS REGISTER Operation
            ClearFlagStatusRegister = 0x50,

            // PROGRAM Operations
            PageProgram = 0x02,
            DualInputFastProgram = 0xA2,
            ExtendedDualInputFastProgram = 0xD2,
            QuadInputFastProgram = 0x32,
            ExtendedQuadInputFastProgram = 0x38,

            // PROGRAM Operations with 4-Byte Address
            PageProgram4byte = 0x12,
            QuadInputFastProgram4byte = 0x34,
            QuadInputExtendedFastProgram4byte = 0x3E,

            // ERASE Operations
            SubsectorErase32kb = 0x52,
            SubsectorErase4kb = 0x20,
            SectorErase = 0xD8,
            BulkErase = 0x60,

            // ERASE Operations with 4-Byte Address
            SectorErase4byte = 0xDC,
            SubsectorErase4byte4kb = 0x21,
            SubsectorErase4byte32kb = 0x5C,

            // SUSPEND/RESUME Operations
            ProgramEraseSuspend = 0x75,
            ProgramEraseResume = 0x7A,

            // ONE-TIME PROGRAMMABLE (OTP) Operations
            ReadOtpArray = 0x4B,
            ProgramOtpArray = 0x42,

            // 4-BYTE ADDRESS MODE Operations
            Enter4byteAddressMode = 0xB7,
            Exit4byteAddressMode = 0xE9,

            // QUAD PROTOCOL Operations
            EnterQuadInputOutputMode = 0x35,
            ResetQuadInputOutputMode = 0xF5,

            // Deep Power-Down Operations
            EnterDeepPowerDown = 0xB9,
            ReleaseFromDeepPowerdown = 0xAB,

            // ADVANCED SECTOR PROTECTION Operations
            ReadSectorProtection = 0x2D,
            ProgramSectorProtection = 0x2C,
            ReadVolatileLockBits = 0xE8,
            WriteVolatileLockBits = 0xE5,
            ReadNonvolatileLockBits = 0xE2,
            WriteNonvolatileLockBits = 0xE3,
            EraseNonvolatileLockBits = 0xE4,
            ReadGlobalFreezeBit = 0xA7,
            WriteGlobalFreezeBit = 0xA6,
            ReadPassword = 0x27,
            WritePassword = 0x28,
            UnlockPassword = 0x29,

            // ADVANCED SECTOR PROTECTION Operations with 4-Byte Address
            ReadVolatileLockBits4byte = 0xE0,
            WriteVolatileLockBits4byte = 0xE1,

            // ADVANCED FUNCTION INTERFACE Operations
            InterfaceActivation = 0x9B,
            CyclicRedundancyCheck = 0x27
        }

        private enum State
        {
            RecognizeOperation,
            AccumulateCommandAddressBytes,
            AccumulateNoDataCommandAddressBytes,
            HandleCommand,
            HandleNoDataCommand
        }

        private enum Operation
        {
            None,
            Read,
            ReadID,
            Program,
            Erase,
            ReadStatusRegister,
            ReadVolatileConfigurationRegister,
            WriteVolatileConfigurationRegister,
            ReadNonVolatileConfigurationRegister,
            WriteNonVolatileConfigurationRegister
        }
    }

    public enum MicronFlashSize : uint
    {
        // On the left side we have Gigabits/Megabits, on the right side we have Megabytes
        Gb_2 = 0x10000000,  //256 MB
        Gb_1 = 0x8000000,   //128 MB
        Mb_512 = 0x4000000, //64 MB
        Mb_256 = 0x2000000, //32 MB
        Mb_128 = 0x1000000, //16 MB
        Mb_64 = 0x800000,   //8 MB
    }
}