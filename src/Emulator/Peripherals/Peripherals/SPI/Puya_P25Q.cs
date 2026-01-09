//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Puya_P25Q : ISPIPeripheral, IGPIOReceiver
    {
        public Puya_P25Q(MappedMemory underlyingMemory)
        {
            addressBuffer = new List<byte>();
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.StatusRegister1, new ByteRegister(this)
                    .WithTaggedFlag("BUSY", 0)
                    .WithFlag(1, out writeEnabled, name: "WEL (Write Enable Latch)")
                    .WithEnumField(2, 5, out blockProtectBits, name: "BP0-BP4 (Block Protect Bits)")
                    .WithFlag(7, out statusRegisterProtect0, name: "SRP0 (Status Register Protect 0)")
                },
                {(long)Registers.StatusRegister2, new ByteRegister(this)
                    .WithFlag(0, out statusRegisterProtect1, name: "SRP1 (Status Register Protect 1)")
                    .WithFlag(1, out enableQuad, name: "QE (Quad Enable)")
                    .WithTaggedFlag("EP_FAIL (Erase/Program Fail)", 2)
                    .WithTaggedFlag("LB1 (Write protect control and status)", 3)
                    .WithTaggedFlag("LB2 (Write protect control and status)", 4)
                    .WithTaggedFlag("LB3 (Write protect control and status)", 5)
                    .WithTaggedFlag("CMP (Array protection)", 6)
                    .WithTaggedFlag("SUS (Suspend)", 7)
                },
                {(long)Registers.Configure, new ByteRegister(this)
                    .WithTaggedFlag("DLP (Data Learning Pattern)", 0)
                    .WithTaggedFlag("DC (Dummy Cycle)", 1)
                    .WithTaggedFlag("WPS (Write Project Scheme)", 2)
                    .WithEnumField(3, 2, out pageSizeBits, name: "MPM (Multi Page Mode)")
                    .WithReservedBits(5, 2)
                    .WithTaggedFlag("HOLD/RST (Hold of Reset)", 7)
                }
            };
            registers = new ByteRegisterCollection(this, registerMap);
            this.underlyingMemory = underlyingMemory;
        }

        public void Reset()
        {
            resetEnabled = false;
            registers.Reset();
            WriteProtect.Set();
            FinishTransmission();
        }

        public byte Transmit(byte data)
        {
            byte returnValue = 0x0;
            if(!currentCommand.HasValue)
            {
                this.NoisyLog("Selecting new command");
                currentCommand = (Commands)data;
                switch(currentCommand.Value)
                {
                case Commands.WriteEnable:
                    writeEnabled.Value = true;
                    break;
                case Commands.WriteDisable:
                    writeEnabled.Value = false;
                    break;
                case Commands.ResetEnable:
                    resetEnabled = true;
                    break;
                case Commands.Reset:
                    if(resetEnabled)
                    {
                        resetWriteEnable = true;
                        Reset();
                    }
                    else
                    {
                        this.WarningLog("Trying to reset while Reset Enable is not active. Operation will be ignored.");
                    }
                    break;
                }
                return returnValue;
            }

            this.NoisyLog("Current command: {0}", currentCommand.Value);
            switch(currentCommand.Value)
            {
            case Commands.ReadStatusRegister1:
                returnValue = registers.Read((long)Registers.StatusRegister1);
                break;
            case Commands.ReadStatusRegister2:
                returnValue = registers.Read((long)Registers.StatusRegister2);
                break;
            case Commands.ReadConfigureRegister:
                returnValue = registers.Read((long)Registers.Configure);
                break;
            case Commands.WriteStatusRegister:
                WriteStatusRegister(Registers.StatusRegister1, data);
                break;
            case Commands.WriteStatusRegister2:
                WriteStatusRegister(Registers.StatusRegister2, data);
                break;
            case Commands.WriteConfigureRegister:
                WriteConfigureRegister(data);
                break;
            case Commands.PageProgram:
            case Commands.QuadPageProgram:
                WriteUnderlyingMemory(data);
                break;
            case Commands.SectorErase4K:
            case Commands.BlockErase32K:
            case Commands.BlockErase64K:
                EraseUnderlyingMemory(currentCommand.Value, data);
                break;
            default:
                if(Enum.IsDefined(typeof(Commands), currentCommand.Value))
                {
                    this.WarningLog("Unsupported command: {0}", currentCommand.Value);
                }
                else
                {
                    this.ErrorLog("Invalid command: {0}", currentCommand.Value);
                }
                break;
            }
            return returnValue;
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Transmission finished.");
            currentCommand = null;
            addressBuffer.Clear();
            temporaryAddress = 0x0;
            if(resetWriteEnable)
            {
                writeEnabled.Value = false;
            }
            resetWriteEnable = false;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                this.ErrorLog("This peripheral supports gpio input only on index 0, but {0} was called.", number);
                return;
            }
            WriteProtect.Set(value);
        }

        public GPIO WriteProtect { get; } = new GPIO();

        public Range? ProtectedRange
        {
            get
            {
                if(((uint)blockProtectBits.Value & BlockProtectNoneMask) == (uint)BlockProtect.None)
                {
                    return null;
                }
                if(((uint)blockProtectBits.Value & BlockProtectAllMask) == (uint)BlockProtect.All)
                {
                    return new Range(0, MaxFlashSize);
                }

                switch(blockProtectBits.Value)
                {
                case BlockProtect.Lower4KB:
                    return new Range(0x0, (ulong)4.KB());
                case BlockProtect.Lower8KB:
                    return new Range(0x0, (ulong)8.KB());
                case BlockProtect.Lower16KB:
                    return new Range(0x0, (ulong)16.KB());
                case BlockProtect.Lower32KB:
                    return new Range(0x0, (ulong)32.KB());
                case BlockProtect.Lower64KB:
                    return new Range(0x0, (ulong)64.KB());
                case BlockProtect.Lower128KB:
                    return new Range(0x0, (ulong)128.KB());
                case BlockProtect.Lower256KB:
                    return new Range(0x0, (ulong)256.KB());
                case BlockProtect.Lower512KB:
                    return new Range(0x0, (ulong)512.KB());
                case BlockProtect.Lower1MB:
                    return new Range(0x0, (ulong)1.MB());
                case BlockProtect.Upper4KB:
                    return new Range(MaxFlashSize - (ulong)4.KB(), (ulong)4.KB());
                case BlockProtect.Upper8KB:
                    return new Range(MaxFlashSize - (ulong)8.KB(), (ulong)8.KB());
                case BlockProtect.Upper16KB:
                    return new Range(MaxFlashSize - (ulong)16.KB(), (ulong)16.KB());
                case BlockProtect.Upper32KB_1:
                case BlockProtect.Upper32KB_2:
                    return new Range(MaxFlashSize - (ulong)32.KB(), (ulong)32.KB());
                case BlockProtect.Upper64KB:
                    return new Range(MaxFlashSize - (ulong)64.KB(), (ulong)64.KB());
                case BlockProtect.Upper128KB:
                    return new Range(MaxFlashSize - (ulong)128.KB(), (ulong)128.KB());
                case BlockProtect.Upper256KB:
                    return new Range(MaxFlashSize - (ulong)256.KB(), (ulong)256.KB());
                case BlockProtect.Upper512KB:
                    return new Range(MaxFlashSize - (ulong)512.KB(), (ulong)512.KB());
                case BlockProtect.Upper1MB:
                    return new Range(MaxFlashSize - (ulong)1.MB(), (ulong)1.MB());
                default:
                    this.ErrorLog("Invalid block protect configuration.");
                    return null;
                }
            }
        }

        private void WriteStatusRegister(Registers register, byte data)
        {
            if(statusRegisterProtect1.Value || (statusRegisterProtect0.Value && !WriteProtect.IsSet))
            {
                this.WarningLog("Trying to write status register while SRP is enabled. Operation will be ignored.");
                return;
            }
            if(!writeEnabled.Value)
            {
                this.WarningLog("Attempted to perform a Write Status operation while flash is in write-disabled state. Operation will be ignored.");
                return;
            }
            registers.Write((long)register, data);
            resetWriteEnable = true;
        }

        private void WriteConfigureRegister(byte data)
        {
            if(statusRegisterProtect1.Value || (statusRegisterProtect0.Value && !WriteProtect.IsSet))
            {
                this.WarningLog("Trying to write configure register while SRP is enabled. Operation will be ignored.");
                return;
            }
            if(!writeEnabled.Value)
            {
                this.WarningLog("Attempted to perform a Write Configure operation while flash is in write-disabled state. Operation will be ignored.");
                return;
            }
            registers.Write((long)Registers.Configure, data);
        }

        private void WriteUnderlyingMemory(byte data)
        {
            if(!writeEnabled.Value)
            {
                this.ErrorLog("Attempted to perform a Page Program operation while flash is in write-disabled state. Operation will be ignored");
                return;
            }

            if(addressBuffer.Count < AddressByteCount)
            {
                this.NoisyLog("Accumulating address bytes.");
                addressBuffer.Add(data);
                if(addressBuffer.Count == AddressByteCount)
                {
                    temporaryAddress = BitHelper.ToUInt32(addressBuffer.ToArray(), 0, AddressByteCount, true);
                    this.NoisyLog("Calculated address for memory write operation: 0x {0:X}", temporaryAddress);
                }
                return;
            }

            var protectedRange = ProtectedRange;
            if(protectedRange.HasValue && protectedRange.Value.Contains(temporaryAddress))
            {
                this.ErrorLog("Attempted to perform a Page Program operation on a protected block. Operation will be ignored");
                return;
            }

            this.NoisyLog("Writing 0x{1:X} to address 0x{0:X}", temporaryAddress, data);
            underlyingMemory.WriteByte(temporaryAddress, data);
            var currentPage = temporaryAddress / PageProgramSize;
            var nextPage = (temporaryAddress + 1) / PageProgramSize;
            if(nextPage == currentPage)
            {
                temporaryAddress++;
            }
            else
            {
                // Address should wrap around to the start of the page
                // when attempting to cross the page boundary
                temporaryAddress = currentPage * PageProgramSize;
            }
            resetWriteEnable = true;
        }

        private void EraseUnderlyingMemory(Commands command, byte data)
        {
            if(!writeEnabled.Value)
            {
                this.ErrorLog("Attempted to perform a Block Erase operation while flash is in write-disabled state. Operation will be ignored");
                return;
            }

            if(addressBuffer.Count < AddressByteCount)
            {
                this.NoisyLog("Accumulating address bytes.");
                addressBuffer.Add(data);
                if(addressBuffer.Count != AddressByteCount)
                {
                    return;
                }
            }

            var address = BitHelper.ToUInt32(addressBuffer.ToArray(), 0, AddressByteCount, true);
            this.NoisyLog("Calculated address for memory erase operation: 0x {0:X}", address);

            var protectedRange = ProtectedRange;
            if(protectedRange.HasValue && protectedRange.Value.Contains(address))
            {
                this.ErrorLog("Attempted to perform a Block Erase operation on a protected block. Operation will be ignored");
                return;
            }

            var size = 0;
            switch(command)
            {
            case Commands.SectorErase4K:
                size = 4.KB();
                break;
            case Commands.BlockErase32K:
                size = 32.KB();
                break;
            case Commands.BlockErase64K:
                size = 64.KB();
                break;
            default:
                this.ErrorLog("Encountered invalid size of requested memory erase operation.");
                return;
            }
            underlyingMemory.SetRange(address, size, 0xFF);
            writeEnabled.Value = false;
        }

        private uint PageProgramSize
        {
            get
            {
                switch(pageSizeBits.Value)
                {
                case PageSize._256Bytes:
                    return DefaultPageProgramSize;
                case PageSize._512Bytes:
                    return DefaultPageProgramSize << 1;
                case PageSize._1024Bytes:
                    return DefaultPageProgramSize << 2;
                default:
                    this.ErrorLog("Invalid page program size configuration, returning 0.");
                    return 0;
                }
            }
        }

        private Commands? currentCommand;
        private uint temporaryAddress;
        private bool resetWriteEnable;
        private bool resetEnabled;

        private readonly ByteRegisterCollection registers;
        private readonly MappedMemory underlyingMemory;
        private readonly IEnumRegisterField<BlockProtect> blockProtectBits;
        private readonly IEnumRegisterField<PageSize> pageSizeBits;
        private readonly IFlagRegisterField statusRegisterProtect0;
        private readonly IFlagRegisterField statusRegisterProtect1;
        private readonly IFlagRegisterField writeEnabled;
        private readonly IFlagRegisterField enableQuad;
        private readonly List<byte> addressBuffer;

        private const int AddressByteCount = 3;
        private const uint DefaultPageProgramSize = 256;
        private const uint BlockProtectAllMask = 0x6;
        private const uint BlockProtectNoneMask = 0x7;

        private const ulong MaxFlashSize = 0x200000;

        public enum BlockProtect : uint
        {
            Upper64KB = 1,
            Upper128KB = 2,
            Upper256KB = 3,
            Upper512KB = 4,
            Upper1MB = 5,
            // [8:6] Reserved
            Lower64KB = 9,
            Lower128KB = 10,
            Lower256KB = 11,
            Lower512KB = 12,
            Lower1MB = 13,
            // [16:14] Rederved
            Upper4KB = 17,
            Upper8KB = 18,
            Upper16KB = 19,
            // [24:21] Reserved
            Lower4KB = 25,
            Lower8KB = 26,
            Lower16KB = 27,
            Lower32KB = 28,
            // these contain possible "don't care bits"
            None = 0,
            All = 6,
            Upper32KB_1 = 20,
            Upper32KB_2 = 21
        }

        private enum PageSize : uint
        {
            _256Bytes = 0,
            _512Bytes = 1,
            _1024Bytes = 2
            // Reserved = 3
        }

        private enum Commands : byte
        {
            // Read
            ReadFast                                = 0x0B,
            Read                                    = 0x03,
            ReadDualOutput                          = 0x3B,
            Read2IO                                 = 0xBB,
            ReadQuadOutput                          = 0x6B,
            Read4IO                                 = 0xEB,
            ReadWord4IO                             = 0xE7,

            // Program and Erase
            PageErase                               = 0x81,
            SectorErase4K                           = 0x20,
            BlockErase32K                           = 0x52,
            BlockErase64K                           = 0xD8,
            ChipErase                               = 0x60,
            ChipErase2                              = 0xC7,
            PageProgram                             = 0x02,
            QuadPageProgram                         = 0x32,
            ProgramOrEraseSuspend                   = 0x75,
            ProgramOrEraseResume                    = 0x7A,

            // Protection
            WriteEnable                             = 0x06,
            WriteDisable                            = 0x04,
            VolatileStatusRegisterWriteEnable       = 0x50,
            IndividualBlockLock                     = 0x36,
            IndividualBlockUnlock                   = 0x39,
            ReadBlockLockStatus                     = 0x3D,
            GlobalBlockLock                         = 0x7E,
            GlobalBlockUnlock                       = 0x98,

            // Security
            EraseSecurityRegisters                  = 0x44,
            ProgramSecurityRegisters                = 0x42,
            ReadSecurityRegisters                   = 0x48,

            // Status Register
            ReadStatusRegister1                     = 0x05,
            ReadStatusRegister2                     = 0x35,
            ReadConfigureRegister                   = 0x15,
            WriteStatusRegister                     = 0x01,
            WriteStatusRegister2                    = 0x31,
            WriteConfigureRegister                  = 0x11,

            // Data Buffer
            BufferClear                             = 0x9E,
            BufferLoad                              = 0x9A,
            BufferRead                              = 0x98,
            BufferWrite                             = 0x9C,
            BufferToMainMemoryPageProgram           = 0x9D,

            // Other
            ResetEnable                             = 0x66,
            Reset                                   = 0x99,
            EnableQPI                               = 0x38,
            ReadJEDECID                             = 0x9F,
            ReadManufactureID                       = 0x90,
            DualReadManufactureID                   = 0x92,
            QuadReadManufactureID                   = 0x94,
            DeepPowerDown                           = 0xB9,
            ReleaseFromDeepPowerDown                = 0xAB,
            SetBurstLength                          = 0x77,
            ReadSFDP                                = 0x5A,
            ReleaseReadEnchanced                    = 0xFF,
            ReadUniqueID                            = 0x4B
        }

        private enum Registers : uint
        {
            StatusRegister1 = 0,
            StatusRegister2,
            Configure
        }
    }
}
