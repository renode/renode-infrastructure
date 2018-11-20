//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Micron_MT25Q : ISPIPeripheral, IDoubleWordPeripheral, IBytePeripheral
    {
        public Micron_MT25Q(MappedMemory memory)
        {
            mappedMemory = memory;
            volatileConfigurationRegister = new ByteRegister(this, 0xfb).WithFlag(3, out xip, name: "XIP");
            nonVolatileConfigurationRegister = new WordRegister(this, 0xffff).WithFlag(0, out bytes, name: "addressWith3Bytes");
            statusRegister = new ByteRegister(this).WithFlag(1, out enable, name: "volatileControlBit");
        }

        public void FinishTransmission()
        {
            // If an operation has at least 1 data byte or more than 0 address bytes,
            // we can clear the write enable flag only when we are finishing a transmission.
            switch(status)
            {
                case Operation.Program:
                case Operation.Erase:
                case Operation.WriteVolatileConfigurationRegister:
                    enable.Value = false;
                    break;
            }
            status = Operation.Finished;

            offset = 0;
            bytesSent = 0;
            addressBytes = 0;
            reg = 0;
        }

        public void Reset()
        {
            mappedMemory.Reset();
            volatileConfigurationRegister.Reset();
            nonVolatileConfigurationRegister.Reset();
            FinishTransmission();
        }

        public byte Transmit(byte data)
        {
            byte result = 0;
            if(status == Operation.Finished)
            {
                // First byte denotes the command
                switch(data)
                {
                    case (byte)Commands.ReadID:
                        status = Operation.Read;
                        addressBytes = 0;
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
                        status = Operation.Read;
                        addressBytes = bytes.Value ? 3 : 4;
                        break;
                    case (byte)Commands.ReadStatusRegister:
                        status = Operation.Read;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.PageProgram:
                    case (byte)Commands.DualInputFastProgram:
                    case (byte)Commands.ExtendedDualInputFastProgram:
                    case (byte)Commands.QuadInputFastProgram:
                    case (byte)Commands.ExtendedQuadInputFastProgram:
                        status = Operation.Program;
                        addressBytes = bytes.Value ? 3 : 4;
                        break;
                    case (byte)Commands.WriteEnable:
                        enable.Value = true;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.WriteDisable:
                        enable.Value = false;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.SectorErase:
                        status = Operation.Erase;
                        addressBytes = bytes.Value ? 3 : 4;
                        break;
                    case (byte)Commands.ReadVolatileConfigurationRegister:
                        status = Operation.ReadVolatileConfigurationRegister;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.WriteVolatileConfigurationRegister:
                        status = Operation.WriteVolatileConfigurationRegister;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.ReadNonVolatileConfigurationRegister:
                        status = Operation.ReadNonVolatileConfigurationRegister;
                        addressBytes = 0;
                        break;
                    case (byte)Commands.WriteNonVolatileConfigurationRegister:
                        status = Operation.WriteNonVolatileConfigurationRegister;
                        addressBytes = 0;
                        break;
                    default:
                        this.Log(LogLevel.Error, $"Trying to execute illegal command: {data}.");
                        break;
                }
            }
            else
            {
                // After the command is decoded, we continue its execution on subsequent transfers
                BitHelper.UpdateWithShifted(ref offset, data, addressBytes * 8, 8);
                if(addressBytes == 0)
                {
                    switch(status)
                    {
                        case Operation.Read:
                            result = ReadFromMemory();
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
                        case Operation.ReadVolatileConfigurationRegister:
                            result = (byte)volatileConfigurationRegister.Value;
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
                            if(bytesSent == 2)
                            {
                                result = 0;
                            }
                            else
                            {
                                result = (byte)BitHelper.GetValue(nonVolatileConfigurationRegister.Value, (int)bytesSent * 8, 8);
                            }
                            break;
                        case Operation.WriteNonVolatileConfigurationRegister:
                            if(enable.Value)
                            {
                                BitHelper.UpdateWithShifted(ref reg, data, (int)bytesSent * 8, 8);
                                if(bytesSent == 1)
                                {
                                    bytes.Value = BitHelper.IsBitSet(reg, 0);
                                }

                            }
                            else
                            {
                                this.Log(LogLevel.Error, "Nonvolatile register writes are disabled.");
                            }
                            break;
                        default:
                            break;
                    }
                    bytesSent++;
                }
            }
            if(addressBytes > 0)
            {
                --addressBytes;
            }
            return result;
        }

        public uint ReadDoubleWord(long offset)
        {
            return mappedMemory.ReadDoubleWord(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Error, "Illegal write to flash in XIP mode.");
        }

        public byte ReadByte(long offset)
        {
            return mappedMemory.ReadByte(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Error, "Illegal write to flash in XIP mode.");
        }

        private void EraseSector()
        {
            var segment = new byte[SegmentSize];
            for(var i = 0; i < SegmentSize; i++)
            {
                segment[i] = EmptySegment;
            }
            mappedMemory.WriteBytes(offset, segment, SegmentSize);
        }

        private void WriteToMemory(byte val)
        {
            mappedMemory.WriteByte(bytesSent, val);
        }

        private byte ReadFromMemory()
        {
            return mappedMemory.ReadByte(bytesSent);
        }

        private uint offset;
        private uint reg;
        private uint bytesSent;
        private int addressBytes;
        private Operation status;

        private readonly MappedMemory mappedMemory;
        private readonly ByteRegister volatileConfigurationRegister;
        private readonly WordRegister nonVolatileConfigurationRegister;
        private readonly ByteRegister statusRegister;
        private readonly IFlagRegisterField xip;
        private readonly IFlagRegisterField bytes;
        private readonly IFlagRegisterField enable;

        private const int SegmentSize = 0x10000;
        private const byte EmptySegment = 0xff;

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

        private enum Operation
        {
            Finished,
            Read,
            Program,
            Erase,
            ReadVolatileConfigurationRegister,
            WriteVolatileConfigurationRegister,
            ReadNonVolatileConfigurationRegister,
            WriteNonVolatileConfigurationRegister,
        }
    }
}
