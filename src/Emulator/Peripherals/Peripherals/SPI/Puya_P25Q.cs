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

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Puya_P25Q : ISPIPeripheral, IGPIOReceiver
    {
        public Puya_P25Q(MappedMemory underlyingMemory)
        {
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.StatusRegister1, new ByteRegister(this)
                    .WithTaggedFlag("BUSY", 0)
                    .WithFlag(1, out writeEnabled, name: "WEL (Write Enable Latch)")
                    .WithTaggedFlag("BP0 (Block Protect 0)", 2)
                    .WithTaggedFlag("BP1 (Block Protect 1)", 3)
                    .WithTaggedFlag("BP2 (Block Protect 2)", 4)
                    .WithTaggedFlag("BP3 (Block Protect 3)", 5)
                    .WithTaggedFlag("BP4 (Block Protect 4)", 6)
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
                    .WithTag("MPM (Multi Page Mode)", 3, 2)
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

        private Commands? currentCommand;
        private bool resetWriteEnable;
        private bool resetEnabled;

        private readonly ByteRegisterCollection registers;
        private readonly MappedMemory underlyingMemory;
        private readonly IFlagRegisterField statusRegisterProtect0;
        private readonly IFlagRegisterField statusRegisterProtect1;
        private readonly IFlagRegisterField writeEnabled;
        private readonly IFlagRegisterField enableQuad;

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
