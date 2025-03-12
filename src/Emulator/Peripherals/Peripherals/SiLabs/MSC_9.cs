//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public class MSC_9 : IDoubleWordPeripheral, IKnownSize
    {
        public MSC_9(Machine machine, CPUCore cpu)
        {
            this.machine = machine;
            this.cpu = cpu;

            pageEraseTimer = new LimitTimer(machine.ClockSource, 1000000, this, "page_erase_timer", PageEraseTimeUs, direction: Direction.Ascending,
                                             enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            pageEraseTimer.LimitReached += PageEraseTimerHandleLimitReached;
                                                          

            IRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if (!internal_read)
                {  
                    this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (!internal_read)
                {
                    this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (!internal_read)
                {
                    this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (!internal_read)
                {
                    this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            if (!internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);

            }

            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ReadControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 20)
                    .WithEnumField<DoubleWordRegister, ReadMode>(20, 2, out readMode, name: "MODE")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.ReadDataControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, out autoFlushDisable, name: "AFDIS")
                    .WithReservedBits(2, 10)
                    .WithFlag(12, out flashDataOutputBufferEnable, name: "DOUTBUFEN")
                    .WithReservedBits(13, 19)
                },
                {(long)Registers.WriteControl, new DoubleWordRegister(this)
                    .WithFlag(0, out writeAndEraseEnable, name: "WREN")
                    .WithFlag(1, out abortPageEraseOnInterrupt, name: "IRQERASEABORT")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, out lowPowerErase, name: "LPWRITE")
                    .WithReservedBits(4, 12)
                    .WithValueField(16, 10, out eraseRangeCount, name: "RANGECOUNT")
                    .WithReservedBits(26, 6)
                },
                {(long)Registers.WriteCommand, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { ErasePage(); } }, name: "ERASEPAGE")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) { EndWriteMode(); } }, name: "WRITEEND")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) { EraseRange(); } }, name: "ERASERANGE")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) { EraseAbort(); } }, name: "ERASEABORT")
                    .WithReservedBits(6, 2)
                    .WithFlag(8, FieldMode.Write, writeCallback: (_, value) => { if (value) { MassEraseRegion0(); } }, name: "ERASEABORT")
                    .WithReservedBits(9, 3)
                    .WithFlag(12, FieldMode.Write, writeCallback: (_, value) => { if (value) { ClearWriteDataState(); } }, name: "CLEARWDATA")
                    .WithReservedBits(13, 19)
                },
                {(long)Registers.PageEraseOrWriteAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => { PageEraseOrWriteAddress = (uint)value; }, name: "ADDRB")
                },
                {(long)Registers.WriteData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => { WriteData((uint)value); }, name: "DATAW")
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x08000008)
                    .WithFlag(0, out busy, FieldMode.Read, name: "BUSY")
                    .WithFlag(1, out locked, FieldMode.Read, name: "LOCKED")
                    .WithFlag(2, out invalidAddress, FieldMode.Read, name: "INVADDR")
                    .WithFlag(3, out writeDataReady, FieldMode.Read, name: "WDATAREADY")
                    .WithTaggedFlag("The Current Flash Erase Operation was aborted by interrupt (ERASEABORTED)", 4)
                    .WithTaggedFlag("Write command is in queue (PENDING)", 5)
                    .WithTaggedFlag("Write command timeout flag (TIMEOUT)", 6)
                    .WithTaggedFlag("EraseRange with skipped locked pages (RANGEPARTIAL)", 7)
                    .WithReservedBits(8, 8)
                    .WithTaggedFlag("Register Lock Status (REGLOCK)", 16)
                    .WithReservedBits(17, 7)
                    .WithFlag(24, out flashPowerOnStatus, FieldMode.Read, name: "PWRON")
                    .WithFlag(25, out flashPowerOnStatus, FieldMode.Read, name: "PWRON1")
                    .WithReservedBits(26, 1)
                    .WithFlag(27, out writeOrEraseReady, FieldMode.Read, name: "WREADY")
                    .WithTag("Flash power up checkerboard pattern check fail count (PWRUPCKBDFAILCOUNT)", 28, 4)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out eraseDoneInterrupt, name: "ERASEIF")
                    .WithFlag(1, out writeDoneInterrupt, name: "WRITEIF")
                    .WithFlag(2, out writeDataOverflowInterrupt, name: "WDATAOVIF")
                    .WithReservedBits(3, 5)
                    .WithFlag(8, out powerUpFinishedInterrupt, name: "PWRUPFIF")
                    .WithFlag(9, out powerOffFinishedInterrupt, name: "PWROFFIF")
                    .WithReservedBits(10, 22)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out eraseDoneInterruptEnable, name: "ERASEIEN")
                    .WithFlag(1, out writeDoneInterruptEnable, name: "WRITEIEN")
                    .WithFlag(2, out writeDataOverflowInterruptEnable, name: "WDATAOVIEN")
                    .WithReservedBits(3, 5)
                    .WithFlag(8, out powerUpFinishedInterruptEnable, name: "PWRUPFIEN")
                    .WithFlag(9, out powerOffFinishedInterruptEnable, name: "PWROFFIEN")
                    .WithReservedBits(10, 22)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { PowerUp(); } }, name: "PWRUP")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) { PowerOff(); } }, name: "PWROFF")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.LockConfig, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) => { locked.Value = (value != UnlockCode); }, name: "LOCKKEY")
                    .WithReservedBits(16, 16)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }

        private readonly Machine machine;
        private readonly CPUCore cpu;
        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly LimitTimer pageEraseTimer;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint UnlockCode = 7025;

        // TODO: RENODE-5: change it back to 12ms
        //private const uint PageEraseTimeUs = 12000;
        private const uint PageEraseTimeUs = 100;
        private const uint WordWriteTimeUs = 3;
        private const uint FlashBase = 0x8000000;
        private const uint FlashSize = 0x320000;
        private const uint FlashPageSize = 0x2000;
        private uint pageEraseOrWriteAddress;

#region register fields
        private IEnumRegisterField<ReadMode> readMode;
        private IFlagRegisterField autoFlushDisable;
        private IFlagRegisterField flashDataOutputBufferEnable;
        private IFlagRegisterField writeAndEraseEnable;
        private IFlagRegisterField abortPageEraseOnInterrupt;
        private IFlagRegisterField lowPowerErase;
        private IValueRegisterField eraseRangeCount;
        private IFlagRegisterField busy;
        private IFlagRegisterField locked;
        private IFlagRegisterField invalidAddress;
        private IFlagRegisterField writeDataReady;
        private IFlagRegisterField flashPowerOnStatus;
        private IFlagRegisterField writeOrEraseReady;

        // Interrupt flags
        private IFlagRegisterField eraseDoneInterrupt;
        private IFlagRegisterField writeDoneInterrupt;
        private IFlagRegisterField writeDataOverflowInterrupt;
        private IFlagRegisterField powerUpFinishedInterrupt;
        private IFlagRegisterField powerOffFinishedInterrupt;
        private IFlagRegisterField eraseDoneInterruptEnable;
        private IFlagRegisterField writeDoneInterruptEnable;
        private IFlagRegisterField writeDataOverflowInterruptEnable;
        private IFlagRegisterField powerUpFinishedInterruptEnable;
        private IFlagRegisterField powerOffFinishedInterruptEnable;
#endregion

#region methods
        private uint PageEraseOrWriteAddress
        {
            get => pageEraseOrWriteAddress;
            set
            {
                pageEraseOrWriteAddress = value;
                invalidAddress.Value = (value < FlashBase || value >= (FlashBase + FlashSize));
            }
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((eraseDoneInterruptEnable.Value && eraseDoneInterrupt.Value)
                        || (writeDoneInterruptEnable.Value && writeDoneInterrupt.Value)
                        || (writeDataOverflowInterruptEnable.Value && writeDataOverflowInterrupt.Value)
                        || (powerUpFinishedInterruptEnable.Value && powerUpFinishedInterrupt.Value)
                        || (powerOffFinishedInterruptEnable.Value && powerOffFinishedInterrupt.Value));
                IRQ.Set(irq);
            });
        }

        private void PageEraseTimerHandleLimitReached()
        {
            pageEraseTimer.Enabled = false;

            // For erase operations, the address may be any within the page to be erased.
            uint startAddress = pageEraseOrWriteAddress & ~(FlashPageSize - 1);
            
            this.Log(LogLevel.Info, "CMD ERASE_PAGE - Erasing page: address={0:X}, page address={1:X}", pageEraseOrWriteAddress, startAddress);

            machine.ClockSource.ExecuteInLock(delegate {
                for(var addr = startAddress; addr < startAddress+FlashPageSize; addr += 4)
                {
                    machine.SystemBus.WriteDoubleWord(addr, 0xFFFFFFFF);
                }
                busy.Value = false;
                eraseDoneInterrupt.Value = true;
            });

            UpdateInterrupts();
            //cpu.IsHalted = false;
        }

        // Commands
        private void ErasePage()
        {
            this.Log(LogLevel.Info, "MSC_CMD: ERASE_PAGE");

            if (writeAndEraseEnable.Value && !invalidAddress.Value && !locked.Value)
            {
                // TODO: for now we start a timer and do the actual page erase entirely when the timer expires. 
                pageEraseTimer.Value = 0;
                pageEraseTimer.Enabled = true;
                
                // Halt the CPU until the timer expires. 
                // TODO: RENODE-5
                //cpu.IsHalted = true;
                busy.Value = true;
            }
        }

        private void WriteData(uint value)
        {
            this.Log(LogLevel.Info, "MSC_CMD: WRITE_DATA");

            if (writeAndEraseEnable.Value && !invalidAddress.Value && !locked.Value)
            {
                // TODO: for now single word writes to flash are considered "instantenous".
                machine.ClockSource.ExecuteInLock(delegate {
                    writeDataReady.Value = false;
                    busy.Value = true;
                    uint currentValue = machine.SystemBus.ReadDoubleWord(pageEraseOrWriteAddress);
                    // 0s in currentValue stays 0s. 1s in currentValue are flipped to 0s if the corresponding bit in value is 0.
                    uint newValue = currentValue & (~currentValue | value);
                    machine.SystemBus.WriteDoubleWord(pageEraseOrWriteAddress, newValue);
                    pageEraseOrWriteAddress += 4;
                    busy.Value = false;
                    writeDataReady.Value = true;
                    writeDoneInterrupt.Value = true;
                });
                UpdateInterrupts();
            }
        }

        private void EndWriteMode()
        {
            this.Log(LogLevel.Info, "MSC_CMD: END_WRITE_MODE");

            // TODO: this can be a no-op since we implement writes as instantanous operations.
        }

        private void EraseRange()
        {
            this.Log(LogLevel.Error, "MSC_CMD: ERASE_RANGE not implemented!");
        }

        private void EraseAbort()
        {
            this.Log(LogLevel.Error, "MSC_CMD: ERASE_ABORT not implemented!");
        }

        private void MassEraseRegion0()
        {
            this.Log(LogLevel.Error, "MSC_CMD: MASS_ERASE_REGION0 not implemented!");
        }

        private void ClearWriteDataState()
        {
            this.Log(LogLevel.Error, "MSC_CMD: CLEAR_WRITE_DATA not implemented!");
        }

        private void PowerUp()
        {
            this.Log(LogLevel.Error, "MSC_CMD: POWER_UP not implemented!");
        }

        private void PowerOff()
        {
            this.Log(LogLevel.Error, "MSC_CMD: POWER_OFF not implemented!");
        }
#endregion

#region enums
        private enum ReadMode
        {
            WS0         = 0x0,
            WS1         = 0x1,
            WS2         = 0x2,
            WS3         = 0x3,
        }

        private enum Registers
        {
            IpVersion                                    = 0x0000,
            ReadControl                                  = 0x0004,
            ReadDataControl                              = 0x0008,
            WriteControl                                 = 0x000C,
            WriteCommand                                 = 0x0010,
            PageEraseOrWriteAddress                      = 0x0014,
            WriteData                                    = 0x0018,
            Status                                       = 0x001C,
            InterruptFlags                               = 0x0020,
            InterruptEnable                              = 0x0024,
            UserDataSize                                 = 0x0034,
            Command                                      = 0x0038,
            LockConfig                                   = 0x003C,
            LockWord                                     = 0x0040,
            PowerControl                                 = 0x0050,
            SecureEngineWriteControl                     = 0x0080,
            SecureEngineWriteCommand                     = 0x0084,
            SecureEnginePageEraseOrWriteAddress          = 0x0088,
            SecureEngineWriteData                        = 0x008C,
            SecureEngineStatus                           = 0x0090,
            SecureEngineInterruptFlags                   = 0x0094,
            SecureEngineInterruptEnable                  = 0x0098,
            SecureEngineMemoryFeatureConfig              = 0x009C,
            SecureEngineStartupControl                   = 0x00A0,
            SecureEngineReadDataControl                  = 0x00A4,
            SecureEngineFlashPowerupCheckSkip            = 0x00A8,
            SecureEngineMtpPageControl                   = 0x00AC,
            SecureEngineMtpSectionSize                   = 0x00B0,
            SecureEngineOtpEraseRegister                 = 0x00B4,
            SecureEngineFlashEraseTime                   = 0x00C0,
            SecureEngineFlashProgrammingTime             = 0x00C4,
            SecureEngineConfigurationLock                = 0x00C8,
            PageLockWord0                                = 0x0120,
            PageLockWord1                                = 0x0124,
            PageLockWord2                                = 0x0128,
            PageLockWord3                                = 0x012C,
            PageLockWord4                                = 0x0130,
            PageLockWord5                                = 0x0134,
            PageLockWord6                                = 0x0138,
            PageLockWord7                                = 0x013C,
            PageLockWord8                                = 0x0140,
            PageLockWord9                                = 0x0144,
            PageLockWord10                               = 0x0148,
            PageLockWord11                               = 0x014C,
            PageLockWord12                               = 0x0150,
            SecureEngineFlashRepairAddress0              = 0x0160,
            SecureEngineFlashRepairAddress1              = 0x0164,
            SecureEngineFlashRepairAddress2              = 0x0168,
            SecureEngineFlashRepairAddress3              = 0x016C,
            DataField0                                   = 0x0180,
            DataField1                                   = 0x0184,
            DataField01                                  = 0x0188,
            DataField11                                  = 0x018C,
            TestControl                                  = 0x01A0,
            TestPatternCheckerControl                    = 0x01A4,
            TestPatternCheckAddress                      = 0x01A8,
            TestPatternCheckerDoneAddress                = 0x01AC,
            TestPatternCheckStatus                       = 0x01B0,
            RedundancyPageTestControl                    = 0x01BC,
            TestLock                                     = 0x01C0,
            TestPatternCheckAddress1                     = 0x01C4,
            RootAccessTypeDescriptor0                    = 0x01C8,
            RootAccessTypeDescriptor1                    = 0x01CC,
            RootAccessTypeDescriptor2                    = 0x01D0,
            RootAccessTypeDescriptor3                    = 0x01D4,
            // Set registers
            IpVersion_Set                                = 0x1000,
            ReadControl_Set                              = 0x1004,
            ReadDataControl_Set                          = 0x1008,
            WriteControl_Set                             = 0x100C,
            WriteCommand_Set                             = 0x1010,
            PageEraseOrWriteAddress_Set                  = 0x1014,
            WriteData_Set                                = 0x1018,
            Status_Set                                   = 0x101C,
            InterruptFlags_Set                           = 0x1020,
            InterruptEnable_Set                          = 0x1024,
            UserDataSize_Set                             = 0x1034,
            Command_Set                                  = 0x1038,
            LockConfig_Set                               = 0x103C,
            LockWord_Set                                 = 0x1040,
            PowerControl_Set                             = 0x1050,
            SecureEngineWriteControl_Set                 = 0x1080,
            SecureEngineWriteCommand_Set                 = 0x1084,
            SecureEnginePageEraseOrWriteAddress_Set      = 0x1088,
            SecureEngineWriteData_Set                    = 0x108C,
            SecureEngineStatus_Set                       = 0x1090,
            SecureEngineInterruptFlags_Set               = 0x1094,
            SecureEngineInterruptEnable_Set              = 0x1098,
            SecureEngineMemoryFeatureConfig_Set          = 0x109C,
            SecureEngineStartupControl_Set               = 0x10A0,
            SecureEngineReadDataControl_Set              = 0x10A4,
            SecureEngineFlashPowerupCheckSkip_Set        = 0x10A8,
            SecureEngineMtpPageControl_Set               = 0x10AC,
            SecureEngineMtpSectionSize_Set               = 0x10B0,
            SecureEngineOtpEraseRegister_Set             = 0x10B4,
            SecureEngineFlashEraseTime_Set               = 0x10C0,
            SecureEngineFlashProgrammingTime_Set         = 0x10C4,
            SecureEngineConfigurationLock_Set            = 0x10C8,
            PageLockWord0_Set                            = 0x1120,
            PageLockWord1_Set                            = 0x1124,
            PageLockWord2_Set                            = 0x1128,
            PageLockWord3_Set                            = 0x112C,
            PageLockWord4_Set                            = 0x1130,
            PageLockWord5_Set                            = 0x1134,
            PageLockWord6_Set                            = 0x1138,
            PageLockWord7_Set                            = 0x113C,
            PageLockWord8_Set                            = 0x1140,
            PageLockWord9_Set                            = 0x1144,
            PageLockWord10_Set                           = 0x1148,
            PageLockWord11_Set                           = 0x114C,
            PageLockWord12_Set                           = 0x1150,
            SecureEngineFlashRepairAddress0_Set          = 0x1160,
            SecureEngineFlashRepairAddress1_Set          = 0x1164,
            SecureEngineFlashRepairAddress2_Set          = 0x1168,
            SecureEngineFlashRepairAddress3_Set          = 0x116C,
            DataField0_Set                               = 0x1180,
            DataField1_Set                               = 0x1184,
            DataField01_Set                              = 0x1188,
            DataField11_Set                              = 0x118C,
            TestControl_Set                              = 0x11A0,
            TestPatternCheckerControl_Set                = 0x11A4,
            TestPatternCheckAddress_Set                  = 0x11A8,
            TestPatternCheckerDoneAddress_Set            = 0x11AC,
            TestPatternCheckStatus_Set                   = 0x11B0,
            RedundancyPageTestControl_Set                = 0x11BC,
            TestLock_Set                                 = 0x11C0,
            TestPatternCheckAddress1_Set                 = 0x11C4,
            RootAccessTypeDescriptor0_Set                = 0x11C8,
            RootAccessTypeDescriptor1_Set                = 0x11CC,
            RootAccessTypeDescriptor2_Set                = 0x11D0,
            RootAccessTypeDescriptor3_Set                = 0x11D4,
            // Clear registers
            IpVersion_Clr                                = 0x2000,
            ReadControl_Clr                              = 0x2004,
            ReadDataControl_Clr                          = 0x2008,
            WriteControl_Clr                             = 0x200C,
            WriteCommand_Clr                             = 0x2010,
            PageEraseOrWriteAddress_Clr                  = 0x2014,
            WriteData_Clr                                = 0x2018,
            Status_Clr                                   = 0x201C,
            InterruptFlags_Clr                           = 0x2020,
            InterruptEnable_Clr                          = 0x2024,
            UserDataSize_Clr                             = 0x2034,
            Command_Clr                                  = 0x2038,
            LockConfig_Clr                               = 0x203C,
            LockWord_Clr                                 = 0x2040,
            PowerControl_Clr                             = 0x2050,
            SecureEngineWriteControl_Clr                 = 0x2080,
            SecureEngineWriteCommand_Clr                 = 0x2084,
            SecureEnginePageEraseOrWriteAddress_Clr      = 0x2088,
            SecureEngineWriteData_Clr                    = 0x208C,
            SecureEngineStatus_Clr                       = 0x2090,
            SecureEngineInterruptFlags_Clr               = 0x2094,
            SecureEngineInterruptEnable_Clr              = 0x2098,
            SecureEngineMemoryFeatureConfig_Clr          = 0x209C,
            SecureEngineStartupControl_Clr               = 0x20A0,
            SecureEngineReadDataControl_Clr              = 0x20A4,
            SecureEngineFlashPowerupCheckSkip_Clr        = 0x20A8,
            SecureEngineMtpPageControl_Clr               = 0x20AC,
            SecureEngineMtpSectionSize_Clr               = 0x20B0,
            SecureEngineOtpEraseRegister_Clr             = 0x20B4,
            SecureEngineFlashEraseTime_Clr               = 0x20C0,
            SecureEngineFlashProgrammingTime_Clr         = 0x20C4,
            SecureEngineConfigurationLock_Clr            = 0x20C8,
            PageLockWord0_Clr                            = 0x2120,
            PageLockWord1_Clr                            = 0x2124,
            PageLockWord2_Clr                            = 0x2128,
            PageLockWord3_Clr                            = 0x212C,
            PageLockWord4_Clr                            = 0x2130,
            PageLockWord5_Clr                            = 0x2134,
            PageLockWord6_Clr                            = 0x2138,
            PageLockWord7_Clr                            = 0x213C,
            PageLockWord8_Clr                            = 0x2140,
            PageLockWord9_Clr                            = 0x2144,
            PageLockWord10_Clr                           = 0x2148,
            PageLockWord11_Clr                           = 0x214C,
            PageLockWord12_Clr                           = 0x2150,
            SecureEngineFlashRepairAddress0_Clr          = 0x2160,
            SecureEngineFlashRepairAddress1_Clr          = 0x2164,
            SecureEngineFlashRepairAddress2_Clr          = 0x2168,
            SecureEngineFlashRepairAddress3_Clr          = 0x216C,
            DataField0_Clr                               = 0x2180,
            DataField1_Clr                               = 0x2184,
            DataField01_Clr                              = 0x2188,
            DataField11_Clr                              = 0x218C,
            TestControl_Clr                              = 0x21A0,
            TestPatternCheckerControl_Clr                = 0x21A4,
            TestPatternCheckAddress_Clr                  = 0x21A8,
            TestPatternCheckerDoneAddress_Clr            = 0x21AC,
            TestPatternCheckStatus_Clr                   = 0x21B0,
            RedundancyPageTestControl_Clr                = 0x21BC,
            TestLock_Clr                                 = 0x21C0,
            TestPatternCheckAddress1_Clr                 = 0x21C4,
            RootAccessTypeDescriptor0_Clr                = 0x21C8,
            RootAccessTypeDescriptor1_Clr                = 0x21CC,
            RootAccessTypeDescriptor2_Clr                = 0x21D0,
            RootAccessTypeDescriptor3_Clr                = 0x21D4,
            // Toggle registers
            IpVersion_Tgl                                = 0x3000,
            ReadControl_Tgl                              = 0x3004,
            ReadDataControl_Tgl                          = 0x3008,
            WriteControl_Tgl                             = 0x300C,
            WriteCommand_Tgl                             = 0x3010,
            PageEraseOrWriteAddress_Tgl                  = 0x3014,
            WriteData_Tgl                                = 0x3018,
            Status_Tgl                                   = 0x301C,
            InterruptFlags_Tgl                           = 0x3020,
            InterruptEnable_Tgl                          = 0x3024,
            UserDataSize_Tgl                             = 0x3034,
            Command_Tgl                                  = 0x3038,
            LockConfig_Tgl                               = 0x303C,
            LockWord_Tgl                                 = 0x3040,
            PowerControl_Tgl                             = 0x3050,
            SecureEngineWriteControl_Tgl                 = 0x3080,
            SecureEngineWriteCommand_Tgl                 = 0x3084,
            SecureEnginePageEraseOrWriteAddress_Tgl      = 0x3088,
            SecureEngineWriteData_Tgl                    = 0x308C,
            SecureEngineStatus_Tgl                       = 0x3090,
            SecureEngineInterruptFlags_Tgl               = 0x3094,
            SecureEngineInterruptEnable_Tgl              = 0x3098,
            SecureEngineMemoryFeatureConfig_Tgl          = 0x309C,
            SecureEngineStartupControl_Tgl               = 0x30A0,
            SecureEngineReadDataControl_Tgl              = 0x30A4,
            SecureEngineFlashPowerupCheckSkip_Tgl        = 0x30A8,
            SecureEngineMtpPageControl_Tgl               = 0x30AC,
            SecureEngineMtpSectionSize_Tgl               = 0x30B0,
            SecureEngineOtpEraseRegister_Tgl             = 0x30B4,
            SecureEngineFlashEraseTime_Tgl               = 0x30C0,
            SecureEngineFlashProgrammingTime_Tgl         = 0x30C4,
            SecureEngineConfigurationLock_Tgl            = 0x30C8,
            PageLockWord0_Tgl                            = 0x3120,
            PageLockWord1_Tgl                            = 0x3124,
            PageLockWord2_Tgl                            = 0x3128,
            PageLockWord3_Tgl                            = 0x312C,
            PageLockWord4_Tgl                            = 0x3130,
            PageLockWord5_Tgl                            = 0x3134,
            PageLockWord6_Tgl                            = 0x3138,
            PageLockWord7_Tgl                            = 0x313C,
            PageLockWord8_Tgl                            = 0x3140,
            PageLockWord9_Tgl                            = 0x3144,
            PageLockWord10_Tgl                           = 0x3148,
            PageLockWord11_Tgl                           = 0x314C,
            PageLockWord12_Tgl                           = 0x3150,
            SecureEngineFlashRepairAddress0_Tgl          = 0x3160,
            SecureEngineFlashRepairAddress1_Tgl          = 0x3164,
            SecureEngineFlashRepairAddress2_Tgl          = 0x3168,
            SecureEngineFlashRepairAddress3_Tgl          = 0x316C,
            DataField0_Tgl                               = 0x3180,
            DataField1_Tgl                               = 0x3184,
            DataField01_Tgl                              = 0x3188,
            DataField11_Tgl                              = 0x318C,
            TestControl_Tgl                              = 0x31A0,
            TestPatternCheckerControl_Tgl                = 0x31A4,
            TestPatternCheckAddress_Tgl                  = 0x31A8,
            TestPatternCheckerDoneAddress_Tgl            = 0x31AC,
            TestPatternCheckStatus_Tgl                   = 0x31B0,
            RedundancyPageTestControl_Tgl                = 0x31BC,
            TestLock_Tgl                                 = 0x31C0,
            TestPatternCheckAddress1_Tgl                 = 0x31C4,
            RootAccessTypeDescriptor0_Tgl                = 0x31C8,
            RootAccessTypeDescriptor1_Tgl                = 0x31CC,
            RootAccessTypeDescriptor2_Tgl                = 0x31D0,
            RootAccessTypeDescriptor3_Tgl                = 0x31D4,
       }
#endregion        
    }
}