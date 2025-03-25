//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
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
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class EFR32xG2_SYSRTC_1 : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_SYSRTC_1(Machine machine, uint frequency)
        {
            this.machine = machine;
            this.timerFrequency = frequency;
            
            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "sysrtctimer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            IRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            timerIsRunning = false;
            timer.Enabled = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        public byte ReadByte(long offset)
        {
            int byteOffset = (int)(offset & 0x3);
            uint registerValue = ReadRegister(offset, true);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
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
                if(!internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);
                }
            }
            else
            {
                if(!internal_read)
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
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enable, name: "EN")
                    .WithTaggedFlag("DISABLING", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.SoftwareReset, new DoubleWordRegister(this)
                    .WithFlag(0, out swrst_swrst_bit, FieldMode.Write,
                        writeCallback: (_, __) => Swrst_Swrst_Write(_, __),
                        name: "Swrst")
                    .WithFlag(1, out swrst_resetting_bit, FieldMode.Read,
                            valueProviderCallback: (_) => {
                                return swrst_resetting_bit.Value;               
                            },
                            name: "Resetting")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, CFG_DEBUGRUN>(0, 1, out cfg_debugrun_bit, 
                    valueProviderCallback: (_) => {
                        return cfg_debugrun_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                    },
                    name: "Debugrun")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithEnumField<DoubleWordRegister, STATUS_LOCKSTATUS>(1, 1, out status_lockstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        return status_lockstatus_bit.Value;               
                    },
                    name: "Lockstatus")
                    .WithTaggedFlag("FAILDETLOCKSTATUS", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                        writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                        name: "Lockkey")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartCommand(); } }, name: "START")
                  .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StopCommand(); } }, name: "STOP")
                  .WithReservedBits(2, 30)
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                },
                {(long)Registers.Group0InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterrupt, name: "OVFIF")
                    .WithFlag(1, out compare0Interrupt, name: "COMP0IF")
                    .WithFlag(2, out compare1Interrupt, name: "COMP1IF")
                    .WithFlag(3, out capture0Interrupt, name: "CAP0IF")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group0InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterruptEnable, name: "OVFIEN")
                    .WithFlag(1, out compare0InterruptEnable, name: "COMP0IEN")
                    .WithFlag(2, out compare1InterruptEnable, name: "COMP1IEN")
                    .WithFlag(3, out capture0InterruptEnable, name: "CAP0IEN")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group0Control, new DoubleWordRegister(this)
                    .WithFlag(0, out compare0Enable, name: "CMP0EN")
                    .WithFlag(1, out compare1Enable, name: "CMP1EN")
                    .WithFlag(2, out capture0Enable, name: "CAP0EN")
                    .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CMP0CMOA>(3, 3, out ctrl_group0_cmp0cmoa_field, 
                        valueProviderCallback: (_) => {
                            return ctrl_group0_cmp0cmoa_field.Value;               
                        },
                        name: "Cmp0cmoa")
                    .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CMP1CMOA>(6, 3, out ctrl_group0_cmp1cmoa_field, 
                        valueProviderCallback: (_) => {
                            return ctrl_group0_cmp1cmoa_field.Value;               
                        },
                        name: "Cmp1cmoa")
                    .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CAP0EDGE>(9, 2, out ctrl_group0_cap0edge_field, 
                        valueProviderCallback: (_) => {
                            return ctrl_group0_cap0edge_field.Value;               
                        },
                        name: "Cap0edge")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Compare0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out compare0Value, name: "CMP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Compare1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out compare1Value, name: "CMP1VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Capture0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out capture0Value, name: "CAP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private LimitTimer timer;
        private uint timerFrequency;
        public bool timerIsRunning = false;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        public event Action CompareMatchGroup0Channel0;
        public event Action CompareMatchGroup0Channel1;
        public bool Group0_Capture_Enabled
        {
            get 
            {
                return capture0Enable.Value;
            }
        }

#region register fields
        private IFlagRegisterField enable;
        private IFlagRegisterField swrst_swrst_bit;
        private IFlagRegisterField swrst_resetting_bit;
        private IEnumRegisterField<CFG_DEBUGRUN> cfg_debugrun_bit;
        private IValueRegisterField lock_lockkey_field;
        private IEnumRegisterField<STATUS_LOCKSTATUS> status_lockstatus_bit;
        private IFlagRegisterField compare0Enable;
        private IEnumRegisterField<CTRL_GROUP0_CMP0CMOA> ctrl_group0_cmp0cmoa_field;
        private IFlagRegisterField compare1Enable;
        private IEnumRegisterField<CTRL_GROUP0_CMP1CMOA> ctrl_group0_cmp1cmoa_field;
        private IFlagRegisterField capture0Enable;
        protected IEnumRegisterField<CTRL_GROUP0_CAP0EDGE> ctrl_group0_cap0edge_field;
        private IValueRegisterField compare0Value;
        private IValueRegisterField compare1Value;
        private IValueRegisterField capture0Value;
        // Interrupts
        private IFlagRegisterField overflowInterrupt;
        private IFlagRegisterField compare0Interrupt;
        private IFlagRegisterField compare1Interrupt;
        private IFlagRegisterField capture0Interrupt;
        private IFlagRegisterField overflowInterruptEnable;
        private IFlagRegisterField compare0InterruptEnable;
        private IFlagRegisterField compare1InterruptEnable;
        private IFlagRegisterField capture0InterruptEnable;
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        public uint TimerCounter
        {
            get
            {
                if (timerIsRunning)
                {
                    if (timer.Enabled)
                    {
                        TrySyncTime();
                        return (uint)timer.Value;
                    }
                    else
                    {
                        return (uint)timer.Limit;
                    }
                }
                return 0;
            }
            
            set
            {
                timer.Value = value;
                RestartTimer();
            }
        }

        private void StartCommand()
        {
            timerIsRunning = true;
            RestartTimer(true);
        }

        private void StopCommand()
        {
            timerIsRunning = false;
            timer.Enabled = false;
        }

        private void TimerLimitReached()
        {
            bool restartFromZero = false;

            if (timer.Limit == 0xFFFFFFFF)
            {
                overflowInterrupt.Value = true;
                restartFromZero = true;
            }
            if (timer.Limit == compare0Value.Value + 1)
            {
                compare0Interrupt.Value = true;
                //Invoke event (simulated hardware signal)
                CompareMatchGroup0Channel0?.Invoke();

            }
            if (timer.Limit == compare1Value.Value + 1)
            {
                compare1Interrupt.Value = true;
                // Invoke event (simulated hardware signal)
                CompareMatchGroup0Channel1?.Invoke();
            }

            // RENODE-65: add support for capture functionality
            UpdateInterrupts();
            RestartTimer(restartFromZero);
        }

        private void RestartTimer(bool restartFromZero = false)
        {
            if (!timerIsRunning)
            {
                return;
            }

            uint currentValue = restartFromZero ? 0 : TimerCounter;

            timer.Enabled = false;
            uint limit = 0xFFFFFFFF;

            // Compare interrupt fires "on the next cycle", therefore we just set 
            // the timer to the +1 value and fire the interrupt right away when
            // we hit the limit.
        
            if (compare0Enable.Value 
                && currentValue < (compare0Value.Value + 1)
                && (compare0Value.Value + 1) < limit)
            {
                limit = (uint)compare0Value.Value + 1;
            }

            if (compare1Enable.Value 
                && currentValue < (compare1Value.Value + 1)
                && (compare1Value.Value + 1) < limit)
            {
                limit = (uint)compare1Value.Value + 1;
            }

            // RENODE-65: add support for capture functionality

            timer.Limit = limit;
            timer.Enabled = true;
            timer.Value = currentValue;
        }
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((overflowInterruptEnable.Value && overflowInterrupt.Value)
                            || (compare0InterruptEnable.Value && compare0Interrupt.Value)
                            || (compare1InterruptEnable.Value && compare1Interrupt.Value)
                            || (capture0InterruptEnable.Value && capture0Interrupt.Value));
                IRQ.Set(irq);
            });
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private void WriteWSTATIC()
        {
            if(enable.Value)
            {
                this.Log(LogLevel.Error, "Trying to write to a WSTATIC register while peripheral is enabled EN = {0}", enable);
            }
        }

        protected void Swrst_Swrst_Write(bool a, bool b)
        {
            if (b == true)
            {
                swrst_resetting_bit.Value = true;
                Reset();
                ResetRegisters();
                swrst_resetting_bit.Value = false;
            }
        }

        protected void ResetRegisters()
        {
            foreach (Registers reg in System.Enum.GetValues(typeof(Registers)))
            {
                if (reg != (Registers)0x2008 && (uint)reg >= ClearRegisterOffset && (uint)reg < ToggleRegisterOffset)
                {
                    WriteDoubleWord((long)reg, 0xFFFFFFFF);
                }
                Lock_Lockkey_Write(0, 0x4776);
            }
        }

        protected void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x4776)
            {
                status_lockstatus_bit.Value = STATUS_LOCKSTATUS.UNLOCKED;
            }
            else
            {
                status_lockstatus_bit.Value = STATUS_LOCKSTATUS.LOCKED;
            }
        }

        public void CaptureGroup0()
        {
            this.Log(LogLevel.Debug, "Capturing SYSRTC Group 0");
            WriteDoubleWord((long)Registers.Group0Capture0, TimerCounter);
            capture0Interrupt.Value = true;
        }

#endregion

#region enums
        private enum Registers
        {
            IpVersion                 = 0x0000,
            Enable                    = 0x0004,
            SoftwareReset             = 0x0008,
            Config                    = 0x000C,
            Command                   = 0x0010,
            Status                    = 0x0014,
            Counter                   = 0x0018,
            SyncBusy                  = 0x001C,
            Lock                      = 0x0020,
            FailureDetection          = 0x0030,
            FailureDetectionLock      = 0x0034,
            Group0InterruptFlags      = 0x0040,
            Group0InterruptEnable     = 0x0044,
            Group0Control             = 0x0048,
            Group0Compare0            = 0x004C,
            Group0Compare1            = 0x0050,
            Group0Capture0            = 0x0054,
            Group0SyncBusy            = 0x0058,
            // Set registers
            IpVersion_Set             = 0x1000,
            Enable_Set                = 0x1004,
            SoftwareReset_Set         = 0x1008,
            Config_Set                = 0x100C,
            Command_Set               = 0x1010,
            Status_Set                = 0x1014,
            Counter_Set               = 0x1018,
            SyncBusy_Set              = 0x101C,
            Lock_Set                  = 0x1020,
            FailureDetection_Set      = 0x1030,
            FailureDetectionLock_Set  = 0x1034,
            Group0InterruptFlags_Set  = 0x1040,
            Group0InterruptEnable_Set = 0x1044,
            Group0Control_Set         = 0x1048,
            Group0Compare0_Set        = 0x104C,
            Group0Compare1_Set        = 0x1050,
            Group0Capture0_Set        = 0x1054,
            Group0SyncBusy_Set        = 0x1058,
            // Clear registers
            IpVersion_Clr             = 0x2000,
            Enable_Clr                = 0x2004,
            SoftwareReset_Clr         = 0x2008,
            Config_Clr                = 0x200C,
            Command_Clr               = 0x2010,
            Status_Clr                = 0x2014,
            Counter_Clr               = 0x2018,
            SyncBusy_Clr              = 0x201C,
            Lock_Clr                  = 0x2020,
            FailureDetection_Clr      = 0x2030,
            FailureDetectionLock_Clr  = 0x2034,
            Group0InterruptFlags_Clr  = 0x2040,
            Group0InterruptEnable_Clr = 0x2044,
            Group0Control_Clr         = 0x2048,
            Group0Compare0_Clr        = 0x204C,
            Group0Compare1_Clr        = 0x2050,
            Group0Capture0_Clr        = 0x2054,
            Group0SyncBusy_Clr        = 0x2058,
            // Toggle registers
            IpVersion_Tgl             = 0x3000,
            Enable_Tgl                = 0x3004,
            SoftwareReset_Tgl         = 0x3008,
            Config_Tgl                = 0x300C,
            Command_Tgl               = 0x3010,
            Status_Tgl                = 0x3014,
            Counter_Tgl               = 0x3018,
            SyncBusy_Tgl              = 0x301C,
            Lock_Tgl                  = 0x3020,
            FailureDetection_Tgl      = 0x3030,
            FailureDetectionLock_Tgl  = 0x3034,
            Group0InterruptFlags_Tgl  = 0x3040,
            Group0InterruptEnable_Tgl = 0x3044,
            Group0Control_Tgl         = 0x3048,
            Group0Compare0_Tgl        = 0x304C,
            Group0Compare1_Tgl        = 0x3050,
            Group0Capture0_Tgl        = 0x3054,
            Group0SyncBusy_Tgl        = 0x3058,
        }
        protected enum CFG_DEBUGRUN
        {
            DISABLE = 0, // SYSRTC is frozen in debug mode
            ENABLE = 1, // SYSRTC is running in debug mode
        }
        protected enum STATUS_LOCKSTATUS
        {
            UNLOCKED = 0, // SYSRTC registers are unlocked
            LOCKED = 1, // SYSRTC registers are locked
        }
        protected enum CTRL_GROUP0_CMP0CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        protected enum CTRL_GROUP0_CMP1CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        protected enum CTRL_GROUP0_CAP0EDGE
        {
            RISING = 0, // Rising edges detected
            FALLING = 1, // Falling edges detected
            BOTH = 2, // Both edges detected
        }
#endregion        
    }
}