//
// Copyright (c) 2010-2019 Antmicro
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
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Micron_MT25Q : ISPIPeripheral
    {
        public Micron_MT25Q(MappedMemory underlyingMemory)
        {
            // original MT25Q supports capacity 8MB to 256MB,
            // but we extended it down to 64KB
            // to become compatible with N25Q line
            if(underlyingMemory.Size < 64.KB() || underlyingMemory.Size > 256.MB() || !Misc.IsPowerOfTwo((ulong)underlyingMemory.Size))
            {
                throw new ConstructionException("Size of the underlying memory must be a power of 2 value in range 64KB - 256MB");
            }

            volatileConfigurationRegister = new ByteRegister(this, 0xfb).WithFlag(3, name: "XIP");
            nonVolatileConfigurationRegister = new WordRegister(this, 0xffff).WithFlag(0, out numberOfAddressBytes, name: "addressWith3Bytes");
            enhancedVolatileConfigurationRegister = new ByteRegister(this, 0xff)
                .WithValueField(0, 3, name: "Output driver strength")
                .WithReservedBits(3, 1)
                .WithTaggedFlag("Reset/hold", 4)
                //these flags are intentionally not implemented, as they described physical details
                .WithFlag(5, name: "Double transfer rate protocol")
                .WithFlag(6, name: "Dual I/O protocol")
                .WithFlag(7, name: "Quad I/O protocol");
            statusRegister = new ByteRegister(this).WithFlag(1, out enable, name: "volatileControlBit");
            flagStatusRegister = new ByteRegister(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => numberOfAddressBytes.Value, name: "Addressing")
                //other bits indicate either protection errors (not implemented) or pending operations (they already finished)
                .WithReservedBits(3, 1)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "ProgramOrErase");

            this.underlyingMemory = underlyingMemory;
            underlyingMemory.ResetByte = EmptySegment;

            deviceData = GetDeviceData();
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
            switch(currentOperation.Operation)
            {
                case Operation.Program:
                case Operation.Erase:
                case Operation.WriteRegister:
                    //although the docs are not clear, it seems that all register writes should clear the flag
                    enable.Value = false;
                    break;
            }
            state = State.RecognizeOperation;
            currentOperation = default(DecodedOperation);
            temporaryNonVolatileConfiguration = 0;
        }

        public void Reset()
        {
            statusRegister.Reset();
            flagStatusRegister.Reset();
            volatileConfigurationRegister.Reset();
            nonVolatileConfigurationRegister.Reset();
            enhancedVolatileConfigurationRegister.Reset();
            FinishTransmission();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Transmitting data 0x{0:X}, current state: {1}", data, state);
            switch(state)
            {
                case State.RecognizeOperation:
                    // When the command is decoded, depending on the operation we will either start accumulating address bytes
                    // or immediately handle the command bytes
                    RecognizeOperation(data);
                    break;
                case State.AccumulateCommandAddressBytes:
                    AccumulateAddressBytes(data, State.HandleCommand);
                    break;
                case State.AccumulateNoDataCommandAddressBytes:
                    AccumulateAddressBytes(data, State.HandleNoDataCommand);
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

        public MappedMemory UnderlyingMemory => underlyingMemory;

        private void AccumulateAddressBytes(byte addressByte, State nextState)
        {
            if(currentOperation.TryAccumulateAddress(addressByte))
            {
                this.Log(LogLevel.Noisy, "Address accumulated: 0x{0:X}", currentOperation.ExecutionAddress);
                state = nextState;
            }
        }

        private byte[] GetDeviceData()
        {
            // capacity code:
            // 0x10 -  64 KB
            // 0x11 - 128 KB
            // 0x12 - 256 KB
            // ..
            // 0x19 -  32 MB
            //     NOTE: there is a gap between 0x19 and 0x20
            // 0x20 -  64 MB
            // 0x21 - 128 MB
            // 0x22 - 256 MB
            byte capacityCode = 0;

            if(underlyingMemory.Size <= 32.MB())
            {
                capacityCode = (byte)BitHelper.GetMostSignificantSetBitIndex((ulong)underlyingMemory.Size);
            }
            else
            {
                capacityCode = (byte)((BitHelper.GetMostSignificantSetBitIndex((ulong)underlyingMemory.Size) - 26) + 0x20);
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

        private void RecognizeOperation(byte firstByte)
        {
            currentOperation.Operation = Operation.None;
            currentOperation.AddressLength = 0;
            state = State.HandleCommand;
            switch(firstByte)
            {
                case (byte)Commands.ReadID:
                case (byte)Commands.MultipleIoReadID:
                    currentOperation.Operation = Operation.ReadID;
                    break;
                case (byte)Commands.FastRead:
                    // fast read - 3 bytes of address + a dummy byte
                    currentOperation.Operation = Operation.ReadFast;
                    currentOperation.AddressLength = 3;
                    state = State.AccumulateCommandAddressBytes;
                    break;
                case (byte)Commands.Read:
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
                    currentOperation.Operation = Operation.Read;
                    currentOperation.AddressLength = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateCommandAddressBytes;
                    break;
                case (byte)Commands.PageProgram:
                case (byte)Commands.DualInputFastProgram:
                case (byte)Commands.ExtendedDualInputFastProgram:
                case (byte)Commands.QuadInputFastProgram:
                case (byte)Commands.ExtendedQuadInputFastProgram:
                    currentOperation.Operation = Operation.Program;
                    currentOperation.AddressLength = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateCommandAddressBytes;
                    break;
                case (byte)Commands.WriteEnable:
                    this.Log(LogLevel.Noisy, "Setting write enable latch");
                    enable.Value = true;
                    return; //return to prevent further logging
                case (byte)Commands.WriteDisable:
                    this.Log(LogLevel.Noisy, "Unsetting write enable latch");
                    enable.Value = false;
                    return; //return to prevent further logging
                case (byte)Commands.SectorErase:
                    currentOperation.Operation = Operation.Erase;
                    currentOperation.EraseSize = EraseSize.Sector;
                    currentOperation.AddressLength = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateNoDataCommandAddressBytes;
                    break;
                case (byte)Commands.DieErase:
                    currentOperation.Operation = Operation.Erase;
                    currentOperation.EraseSize = EraseSize.Die;
                    currentOperation.AddressLength = numberOfAddressBytes.Value ? 3 : 4;
                    state = State.AccumulateNoDataCommandAddressBytes;
                    break;
                case (byte)Commands.ReadStatusRegister:
                    currentOperation.Operation = Operation.ReadRegister;
                    currentOperation.Register = Register.Status;
                    break;
                case (byte)Commands.WriteStatusRegister:
                    currentOperation.Operation = Operation.WriteRegister;
                    currentOperation.Register = Register.Status;
                    break;
                case (byte)Commands.ReadFlagStatusRegister:
                    currentOperation.Operation = Operation.ReadRegister;
                    currentOperation.Register = Register.FlagStatus;
                    break;
                case (byte)Commands.ReadVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.ReadRegister;
                    currentOperation.Register = Register.VolatileConfiguration;
                    break;
                case (byte)Commands.WriteVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.WriteRegister;
                    currentOperation.Register = Register.VolatileConfiguration;
                    break;
                case (byte)Commands.ReadNonVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.ReadRegister;
                    currentOperation.Register = Register.NonVolatileConfiguration;
                    break;
                case (byte)Commands.WriteNonVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.WriteRegister;
                    currentOperation.Register = Register.NonVolatileConfiguration;
                    break;
                case (byte)Commands.ReadEnhancedVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.ReadRegister;
                    currentOperation.Register = Register.EnhancedVolatileConfiguration;
                    break;
                case (byte)Commands.WriteEnhancedVolatileConfigurationRegister:
                    currentOperation.Operation = Operation.WriteRegister;
                    currentOperation.Register = Register.EnhancedVolatileConfiguration;
                    break;
                default:
                    this.Log(LogLevel.Error, "Command decoding failed on byte: 0x{0:X}.", firstByte);
                    return;
            }
            this.Log(LogLevel.Noisy, "Decoded operation: {0}, write enabled {1}", currentOperation, enable.Value);
        }

        private byte HandleCommand(byte data)
        {
            byte result = 0;
            switch(currentOperation.Operation)
            {
                case Operation.ReadFast:
                    // handle dummy byte and switch to read
                    currentOperation.Operation = Operation.Read;
                    currentOperation.CommandBytesHandled--;
                    this.Log(LogLevel.Noisy, "Handling dummy byte in ReadFast operation");
                    break;
                case Operation.Read:
                    result = ReadFromMemory();
                    break;
                case Operation.ReadID:
                    if(currentOperation.CommandBytesHandled < deviceData.Length)
                    {
                        result = deviceData[currentOperation.CommandBytesHandled];
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Trying to read beyond the length of the device ID table.");
                        result = 0;
                    }
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
                case Operation.ReadRegister:
                    result = ReadRegister(currentOperation.Register);
                    break;
                case Operation.WriteRegister:
                    WriteRegister(currentOperation.Register, data);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled operation encountered while processing command bytes: {0}", currentOperation.Operation);
                    break;
            }
            currentOperation.CommandBytesHandled++;
            this.Log(LogLevel.Noisy, "Handled command: {0}, returning 0x{1:X}", currentOperation, result);
            return result;
        }

        private void WriteRegister(Register register, byte data)
        {
            if(!enable.Value)
            {
                this.Log(LogLevel.Error, "Trying to write a register, but write enable latch is not set");
                return;
            }
            switch(register)
            {
                case Register.VolatileConfiguration:
                    volatileConfigurationRegister.Write(0, data);
                    break;
                case Register.NonVolatileConfiguration:
                    if((currentOperation.CommandBytesHandled) >= 2)
                    {
                        this.Log(LogLevel.Error, "Trying to write to register {0} with more than expected 2 bytes.", Register.NonVolatileConfiguration);
                        break;
                    }
                    BitHelper.UpdateWithShifted(ref temporaryNonVolatileConfiguration, data, currentOperation.CommandBytesHandled * 8, 8);
                    if(currentOperation.CommandBytesHandled == 1)
                    {
                        nonVolatileConfigurationRegister.Write(0, (ushort)temporaryNonVolatileConfiguration);
                    }
                    break;
                //listing all cases as other registers are not writable at all
                case Register.EnhancedVolatileConfiguration:
                    enhancedVolatileConfigurationRegister.Write(0, data);
                    break;
                case Register.Status:
                default:
                    this.Log(LogLevel.Warning, "Trying to write 0x{0} to unsupported register \"{1}\"", data, register);
                    break;
            }
        }

        private byte ReadRegister(Register register)
        {
            switch(register)
            {
                case Register.Status:
                    // The documentation states that at least 1 byte will be read
                    // If more than 1 byte is read, the same byte is returned
                    return statusRegister.Read();
                case Register.FlagStatus:
                    // The documentation states that at least 1 byte will be read
                    // If more than 1 byte is read, the same byte is returned
                    return flagStatusRegister.Read();
                case Register.VolatileConfiguration:
                    // The documentation states that at least 1 byte will be read
                    // If more than 1 byte is read, the same byte is returned
                    return volatileConfigurationRegister.Read();
                case Register.NonVolatileConfiguration:
                    // The documentation states that at least 2 bytes will be read
                    // After all 16 bits of the register have been read, 0 is returned
                    if((currentOperation.CommandBytesHandled) < 2)
                    {
                        return (byte)BitHelper.GetValue(nonVolatileConfigurationRegister.Read(), currentOperation.CommandBytesHandled * 8, 8);
                    }
                    return 0;
                case Register.EnhancedVolatileConfiguration:
                    return enhancedVolatileConfigurationRegister.Read();
                case Register.ExtendedAddress:
                default:
                    this.Log(LogLevel.Warning, "Trying to read from unsupported register \"{0}\"", register);
                    return 0;
            }
        }

        private void HandleNoDataCommand()
        {
            // The documentation describes more commands that don't have any data bytes (just code + address)
            // but at the moment we have implemented just these ones
            switch(currentOperation.Operation)
            {
                case Operation.Erase:
                    if(enable.Value)
                    {
                        if(currentOperation.ExecutionAddress >= underlyingMemory.Size)
                        {
                            this.Log(LogLevel.Error, "Cannot erase memory because current address 0x{0:X} exceeds configured memory size.", currentOperation.ExecutionAddress);
                            return;
                        }
                        switch(currentOperation.EraseSize)
                        {
                            case EraseSize.Sector:
                                EraseSector();
                                break;
                            case EraseSize.Die:
                                EraseDie();
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Unsupported erase type: {0}", currentOperation.EraseSize);
                                break;
                        }
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Erase operations are disabled.");
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Encountered unexpected command: {0}", currentOperation);
                    break;
            }
        }

        private void EraseDie()
        {
            var position = 0;
            var segment = new byte[SegmentSize];
            for(var i = 0; i < SegmentSize; i++)
            {
                segment[i] = EmptySegment;
            }

            while(position < underlyingMemory.Size)
            {
                var length = (int)Math.Min(SegmentSize, underlyingMemory.Size - position);

                underlyingMemory.WriteBytes(position, segment, length);
                position += length;
            }
        }

        private void EraseSector()
        {
            var segment = new byte[SegmentSize];
            for(var i = 0; i < SegmentSize; i++)
            {
                segment[i] = EmptySegment;
            }
            // The documentations states that on erase the operation address is
            // aligned to the segment size

            var position = SegmentSize * (currentOperation.ExecutionAddress / SegmentSize);
            underlyingMemory.WriteBytes(position, segment);
        }

        private void WriteToMemory(byte val)
        {
            if(currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled > underlyingMemory.Size)
            {
                this.Log(LogLevel.Error, "Cannot write to address 0x{0:X} because it is bigger than configured memory size.", currentOperation.ExecutionAddress);
                return;
            }

            var position = currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled;
            underlyingMemory.WriteByte(position, val);
        }

        private byte ReadFromMemory()
        {
            if(currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled > underlyingMemory.Size)
            {
                this.Log(LogLevel.Error, "Cannot read from address 0x{0:X} because it is bigger than configured memory size.", currentOperation.ExecutionAddress);
                return 0;
            }

            var position = currentOperation.ExecutionAddress + currentOperation.CommandBytesHandled;
            return  underlyingMemory.ReadByte(position);
        }

        private State state;
        private DecodedOperation currentOperation;
        private uint temporaryNonVolatileConfiguration; //this should be an ushort, but due to C# type promotions it's easier to use uint

        private readonly byte[] deviceData;
        private readonly int SegmentSize = 64.KB();
        private readonly IFlagRegisterField enable;
        private readonly ByteRegister statusRegister;
        private readonly ByteRegister flagStatusRegister;
        private readonly IFlagRegisterField numberOfAddressBytes;
        private readonly ByteRegister volatileConfigurationRegister;
        private readonly ByteRegister enhancedVolatileConfigurationRegister;
        private readonly WordRegister nonVolatileConfigurationRegister;
        private readonly MappedMemory underlyingMemory;

        private const byte EmptySegment = 0xff;
        private const byte ManufacturerID = 0x20;
        private const byte RemainingIDBytes = 0x10;
        private const byte MemoryType = 0xBB;           // device voltage: 1.8V
        private const byte DeviceConfiguration = 0x0;   // standard
        private const byte DeviceGeneration = 0x1;      // 2nd generation
        private const byte ExtendedDeviceID = DeviceGeneration << 6;

        private struct DecodedOperation
        {
            public Operation Operation;
            public Register Register;
            public EraseSize EraseSize;
            public int AddressLength
            {
                get
                {
                    return addressLength;
                }
                set
                {
                    addressLength = value;
                    if(addressLength > 0)
                    {
                        AddressBytes = new byte[addressLength];
                    }
                }
            }
            public uint ExecutionAddress;
            public int CommandBytesHandled;

            public bool TryAccumulateAddress(byte data)
            {
                AddressBytes[currentAddressByte] = data;
                currentAddressByte++;
                if(currentAddressByte == AddressLength)
                {
                    ExecutionAddress = BitHelper.ToUInt32(AddressBytes, 0, AddressLength, false);
                    return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"Operation: {Operation}"
                    .AppendIf(Operation == Operation.ReadRegister || Operation == Operation.WriteRegister, $", register: {Register}")
                    .AppendIf(EraseSize != 0, $", erase size: {EraseSize}")
                    .AppendIf(AddressLength != 0, $", address length: {AddressLength}")
                    .ToString();
            }

            private byte[] AddressBytes;
            private int addressLength;
            private int currentAddressByte;
        }

        private enum EraseSize
        {
            Die = 1, // starting from 1 to leave 0 as explicitly unused
            Sector,
            Subsector32K,
            Subsector4K
        }

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
            DieErase = 0xC4,

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
            ReadFast,
            ReadID,
            Program,
            Erase,
            ReadRegister,
            WriteRegister,
        }

        private enum Register
        {
            Status = 1, //starting from 1 to leave 0 as an unused value
            FlagStatus,
            ExtendedAddress,
            NonVolatileConfiguration,
            VolatileConfiguration,
            EnhancedVolatileConfiguration
        }
    }
}
