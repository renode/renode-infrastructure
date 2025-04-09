//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
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

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class EFR32xG2_MSC_3 : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_MSC_3(Machine machine, CPUCore cpu, uint flashSize, uint flashPageSize)
        {
            this.machine = machine;
            this.cpu = cpu;
            this.FlashSize = flashSize;
            this.FlashPageSize = flashPageSize;

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
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if (!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);
                }
            }
            else
            {
                if (!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            return result;
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
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
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
                    .WithReservedBits(25, 2)
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
                {(long)Registers.PageLock0, new DoubleWordRegister(this)
                    .WithValueField(0, 22, name: "PAGELOCK0")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.PageLock1, new DoubleWordRegister(this)
                    .WithValueField(0, 22, name: "PAGELOCK1")
                    .WithReservedBits(22, 10)
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
        private uint FlashSize;
        private uint FlashPageSize;
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
            IpVersion                           = 0x0000,
            ReadControl                         = 0x0004,
            ReadDataControl                     = 0x0008,
            WriteControl                        = 0x000C,
            WriteCommand                        = 0x0010,
            PageEraseOrWriteAddress             = 0x0014,
            WriteData                           = 0x0018,
            Status                              = 0x001C,
            InterruptFlags                      = 0x0020,
            InterruptEnable                     = 0x0024,
            UserDataSize                        = 0x0034,
            Command                             = 0x0038,
            LockConfig                          = 0x003C,
            LockWord                            = 0x0040,
            PowerControl                        = 0x0050,
            PageLock0                           = 0x0120,
            PageLock1                           = 0x0124,
            // Set registers
            IpVersion_Set                       = 0x1000,
            ReadControl_Set                     = 0x1004,
            ReadDataControl_Set                 = 0x1008,
            WriteControl_Set                    = 0x100C,
            WriteCommand_Set                    = 0x1010,
            PageEraseOrWriteAddress_Set         = 0x1014,
            WriteData_Set                       = 0x1018,
            Status_Set                          = 0x101C,
            InterruptFlags_Set                  = 0x1020,
            InterruptEnable_Set                 = 0x1024,
            UserDataSize_Set                    = 0x1034,
            Command_Set                         = 0x1038,
            ConfigLock_Set                      = 0x103C,
            LockWord_Set                        = 0x1040,
            PowerControl_Set                    = 0x1050,
            PageLock0_Set                       = 0x1120,
            PageLock1_Set                       = 0x1124,
            // Clear registers
            IpVersion_Clr                       = 0x2000,
            ReadControl_Clr                     = 0x2004,
            ReadDataControl_Clr                 = 0x2008,
            WriteControl_Clr                    = 0x200C,
            WriteCommand_Clr                    = 0x2010,
            PageEraseOrWriteAddress_Clr         = 0x2014,
            WriteData_Clr                       = 0x2018,
            Status_Clr                          = 0x201C,
            InterruptFlags_Clr                  = 0x2020,
            InterruptEnable_Clr                 = 0x2024,
            UserDataSize_Clr                    = 0x2034,
            Command_Clr                         = 0x2038,
            ConfigLock_Clr                      = 0x203C,
            LockWord_Clr                        = 0x2040,
            PowerControl_Clr                    = 0x2050,
            PageLock0_Clr                       = 0x2120,
            PageLock1_Clr                       = 0x2124,
            // Toggle registers
            IpVersion_Tgl                       = 0x3000,
            ReadControl_Tgl                     = 0x3004,
            ReadDataControl_Tgl                 = 0x3008,
            WriteControl_Tgl                    = 0x300C,
            WriteCommand_Tgl                    = 0x3010,
            PageEraseOrWriteAddress_Tgl         = 0x3014,
            WriteData_Tgl                       = 0x3018,
            Status_Tgl                          = 0x301C,
            InterruptFlags_Tgl                  = 0x3020,
            InterruptEnable_Tgl                 = 0x3024,
            UserDataSize_Tgl                    = 0x3034,
            Command_Tgl                         = 0x3038,
            LockConfig_Tgl                      = 0x303C,
            LockWord_Tgl                        = 0x3040,
            PowerControl_Tgl                    = 0x3050,
            PageLock0_Tgl                       = 0x3120,
            PageLock1_Tgl                       = 0x3124,
        }
#endregion        
    }
}