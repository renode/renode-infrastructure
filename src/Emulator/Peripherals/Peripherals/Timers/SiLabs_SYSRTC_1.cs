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
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Timers
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class SiLabs_SYSRTC_1 : IDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_SYSRTC_1(Machine machine, uint frequency)
        {
            this.machine = machine;
            this.timerFrequency = frequency;
            
            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "sysrtctimer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            IRQ = new GPIO();
            AlternateIRQ = new GPIO();
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
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, __) => Reset(), name: "SWRST")
                    .WithTaggedFlag("RESETTING", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithFlag(0, out debugRunEnabled, writeCallback: (_, __) => { WriteWSTATIC(); }, name: "DEBUGRUN")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithFlag(1, out locked, FieldMode.Read, name: "LOCKSTATUS")
                    .WithTaggedFlag("FAILDETLOCKSTATUS", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) => { locked.Value = (LockValue != value); }, name: "LOCKKEY")
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
                    .WithFlag(0, out group0OverflowInterrupt, name: "OVFIF")
                    .WithFlag(1, out group0Compare0Interrupt, name: "COMP0IF")
                    .WithFlag(2, out group0Compare1Interrupt, name: "COMP1IF")
                    .WithFlag(3, out group0Capture0Interrupt, name: "CAP0IF")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group0InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out group0OverflowInterruptEnable, name: "OVFIEN")
                    .WithFlag(1, out group0Compare0InterruptEnable, name: "COMP0IEN")
                    .WithFlag(2, out group0Compare1InterruptEnable, name: "COMP1IEN")
                    .WithFlag(3, out group0Capture0InterruptEnable, name: "CAP0IEN")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group0Control, new DoubleWordRegister(this)
                    .WithFlag(0, out group0Compare0Enable, name: "CMP0EN")
                    .WithFlag(1, out group0Compare1Enable, name: "CMP1EN")
                    .WithFlag(2, out group0Capture0Enable, name: "CAP0EN")
                    .WithEnumField<DoubleWordRegister, CompareMatchOutputAction>(3, 3, out group0Compare0MatchOutputAction, name: "CMP0CMOA")
                    .WithEnumField<DoubleWordRegister, CompareMatchOutputAction>(6, 3, out group0Compare1MatchOutputAction, name: "CMP1CMOA")
                    .WithEnumField<DoubleWordRegister, CaptureEdgeSelect>(9, 2, out group0CaptureEdgeSelect, name: "CAP0EDGE")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Compare0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group0Compare0Value, name: "CMP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Compare1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group0Compare1Value, name: "CMP1VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group0Capture0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group0Capture0Value, name: "CAP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group1InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out group1OverflowInterrupt, name: "OVFIF")
                    .WithFlag(1, out group1Compare0Interrupt, name: "COMP0IF")
                    .WithFlag(2, out group1Compare1Interrupt, name: "COMP1IF")
                    .WithFlag(3, out group1Capture0Interrupt, name: "CAP0IF")
                    .WithFlag(4, out group1AlternateOverflowInterrupt, name: "ALTOVFIF")
                    .WithFlag(5, out group1AlternateCompare0Interrupt, name: "ALTCOMP0IF")
                    .WithFlag(6, out group1AlternateCompare1Interrupt, name: "ALTCOMP1IF")
                    .WithFlag(7, out group1AlternateCapture0Interrupt, name: "ALTCAP0IF")
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group1InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out group1OverflowInterruptEnable, name: "OVFIEN")
                    .WithFlag(1, out group1Compare0InterruptEnable, name: "COMP0IEN")
                    .WithFlag(2, out group1Compare1InterruptEnable, name: "COMP1IEN")
                    .WithFlag(3, out group1Capture0InterruptEnable, name: "CAP0IEN")
                    .WithFlag(4, out group1AlternateOverflowInterruptEnable, name: "ALTOVFIEN")
                    .WithFlag(5, out group1AlternateCompare0InterruptEnable, name: "ALTCOMP0IEN")
                    .WithFlag(6, out group1AlternateCompare1InterruptEnable, name: "ALTCOMP1IEN")
                    .WithFlag(7, out group1AlternateCapture0InterruptEnable, name: "ALTCAP0IEN")
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group1Control, new DoubleWordRegister(this)
                    .WithFlag(0, out group1Compare0Enable, name: "CMP0EN")
                    .WithFlag(1, out group1Compare1Enable, name: "CMP1EN")
                    .WithFlag(2, out group1Capture0Enable, name: "CAP0EN")
                    .WithEnumField<DoubleWordRegister, CompareMatchOutputAction>(3, 3, out group1Compare0MatchOutputAction, name: "CMP0CMOA")
                    .WithEnumField<DoubleWordRegister, CompareMatchOutputAction>(6, 3, out group1Compare1MatchOutputAction, name: "CMP1CMOA")
                    .WithEnumField<DoubleWordRegister, CaptureEdgeSelect>(9, 2, out group1CaptureEdgeSelect, name: "CAP0EDGE")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group1Compare0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group1Compare0Value, name: "CMP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group1Compare1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group1Compare1Value, name: "CMP1VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Group1Capture0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out group1Capture0Value, name: "CAP0VALUE")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        public GPIO AlternateIRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private LimitTimer timer;
        private uint timerFrequency;
        public bool timerIsRunning = false;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint LockValue = 0x4776;
        public event Action CompareMatchGroup0Channel0;
        public event Action CompareMatchGroup0Channel1;
        public event Action CompareMatchGroup1Channel0;
        public event Action CompareMatchGroup1Channel1;

#region register fields
        private IFlagRegisterField enable;
        private IFlagRegisterField debugRunEnabled;
        private IFlagRegisterField locked;
        private IFlagRegisterField group0Compare0Enable;
        private IEnumRegisterField<CompareMatchOutputAction> group0Compare0MatchOutputAction;
        private IFlagRegisterField group0Compare1Enable;
        private IEnumRegisterField<CompareMatchOutputAction> group0Compare1MatchOutputAction;
        private IFlagRegisterField group0Capture0Enable;
        protected IEnumRegisterField<CaptureEdgeSelect> group0CaptureEdgeSelect;
        private IValueRegisterField group0Compare0Value;
        private IValueRegisterField group0Compare1Value;
        private IValueRegisterField group0Capture0Value;
        private IFlagRegisterField group1Compare0Enable;
        private IEnumRegisterField<CompareMatchOutputAction> group1Compare0MatchOutputAction;
        private IFlagRegisterField group1Compare1Enable;
        private IEnumRegisterField<CompareMatchOutputAction> group1Compare1MatchOutputAction;
        private IFlagRegisterField group1Capture0Enable;
        protected IEnumRegisterField<CaptureEdgeSelect> group1CaptureEdgeSelect;
        private IValueRegisterField group1Compare0Value;
        private IValueRegisterField group1Compare1Value;
        private IValueRegisterField group1Capture0Value;
        // Interrupts
        private IFlagRegisterField group0OverflowInterrupt;
        private IFlagRegisterField group0Compare0Interrupt;
        private IFlagRegisterField group0Compare1Interrupt;
        private IFlagRegisterField group0Capture0Interrupt;
        private IFlagRegisterField group0OverflowInterruptEnable;
        private IFlagRegisterField group0Compare0InterruptEnable;
        private IFlagRegisterField group0Compare1InterruptEnable;
        private IFlagRegisterField group0Capture0InterruptEnable;
        private IFlagRegisterField group1OverflowInterrupt;
        private IFlagRegisterField group1Compare0Interrupt;
        private IFlagRegisterField group1Compare1Interrupt;
        private IFlagRegisterField group1Capture0Interrupt;
        private IFlagRegisterField group1AlternateOverflowInterrupt;
        private IFlagRegisterField group1AlternateCompare0Interrupt;
        private IFlagRegisterField group1AlternateCompare1Interrupt;
        private IFlagRegisterField group1AlternateCapture0Interrupt;
        private IFlagRegisterField group1OverflowInterruptEnable;
        private IFlagRegisterField group1Compare0InterruptEnable;
        private IFlagRegisterField group1Compare1InterruptEnable;
        private IFlagRegisterField group1Capture0InterruptEnable;
        private IFlagRegisterField group1AlternateOverflowInterruptEnable;
        private IFlagRegisterField group1AlternateCompare0InterruptEnable;
        private IFlagRegisterField group1AlternateCompare1InterruptEnable;
        private IFlagRegisterField group1AlternateCapture0InterruptEnable;
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
                group0OverflowInterrupt.Value = true;
                group1OverflowInterrupt.Value = true;
                group1AlternateOverflowInterrupt.Value = true;
                restartFromZero = true;
            }
            if (group0Compare0Enable.Value && timer.Limit == group0Compare0Value.Value + 1)
            {
                group0Compare0Interrupt.Value = true;
                //Invoke event (simulated hardware signal)
                CompareMatchGroup0Channel0?.Invoke();

            }
            if (group0Compare1Enable.Value && timer.Limit == group0Compare1Value.Value + 1)
            {
                group0Compare1Interrupt.Value = true;
                // Invoke event (simulated hardware signal)
                CompareMatchGroup0Channel1?.Invoke();
            }
            if (group1Compare0Enable.Value && timer.Limit == group1Compare0Value.Value + 1)
            {
                group1Compare0Interrupt.Value = true;
                group1AlternateCompare0Interrupt.Value = true;
                //Invoke event (simulated hardware signal)
                CompareMatchGroup1Channel0?.Invoke();
            }
            if (group1Compare1Enable.Value && timer.Limit == group1Compare1Value.Value + 1)
            {
                group1Compare1Interrupt.Value = true;
                group1AlternateCompare1Interrupt.Value = true;
                //Invoke event (simulated hardware signal)
                CompareMatchGroup1Channel1?.Invoke();
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
        
            if (group0Compare0Enable.Value 
                && currentValue < (group0Compare0Value.Value + 1)
                && (group0Compare0Value.Value + 1) < limit)
            {
                limit = (uint)group0Compare0Value.Value + 1;
            }

            if (group0Compare1Enable.Value 
                && currentValue < (group0Compare1Value.Value + 1)
                && (group0Compare1Value.Value + 1) < limit)
            {
                limit = (uint)group0Compare1Value.Value + 1;
            }

            if (group1Compare0Enable.Value 
                && currentValue < (group1Compare0Value.Value + 1)
                && (group1Compare0Value.Value + 1) < limit)
            {
                limit = (uint)group1Compare0Value.Value + 1;
            }

            if (group1Compare1Enable.Value 
                && currentValue < (group1Compare1Value.Value + 1)
                && (group1Compare1Value.Value + 1) < limit)
            {
                limit = (uint)group1Compare1Value.Value + 1;
            }

            // RENODE-65: add support for capture functionality

            timer.Limit = limit;
            timer.Enabled = true;
            timer.Value = currentValue;
        }
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = ((group0OverflowInterruptEnable.Value && group0OverflowInterrupt.Value)
                            || (group0Compare0InterruptEnable.Value && group0Compare0Interrupt.Value)
                            || (group0Compare1InterruptEnable.Value && group0Compare1Interrupt.Value)
                            || (group0Capture0InterruptEnable.Value && group0Capture0Interrupt.Value)
                            || (group1OverflowInterruptEnable.Value && group1OverflowInterrupt.Value)
                            || (group1Compare0InterruptEnable.Value && group1Compare0Interrupt.Value)
                            || (group1Compare1InterruptEnable.Value && group1Compare1Interrupt.Value)
                            || (group1Capture0InterruptEnable.Value && group1Capture0Interrupt.Value));
                IRQ.Set(irq);

                irq = ((group1AlternateOverflowInterruptEnable.Value && group1AlternateOverflowInterrupt.Value)
                       || (group1AlternateCompare0InterruptEnable.Value && group1AlternateCompare0Interrupt.Value)
                       || (group1AlternateCompare1InterruptEnable.Value && group1AlternateCompare1Interrupt.Value)
                       || (group1AlternateCapture0InterruptEnable.Value && group1AlternateCapture0Interrupt.Value));
                AlternateIRQ.Set(irq);
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

        public void CaptureGroup0()
        {
            this.Log(LogLevel.Debug, "Capturing SYSRTC Group 0");
            group0Capture0Value.Value = TimerCounter;
            group0Capture0Interrupt.Value = true;
        }

        public void CaptureGroup1()
        {
            this.Log(LogLevel.Debug, "Capturing SYSRTC Group 1");
            group1Capture0Value.Value = TimerCounter;
            group1Capture0Interrupt.Value = true;
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
            Group1InterruptFlags      = 0x0060,
            Group1InterruptEnable     = 0x0064,
            Group1Control             = 0x0068,
            Group1Compare0            = 0x006C,
            Group1Compare1            = 0x0070,
            Group1Capture0            = 0x0074,
            Group1SyncBusy            = 0x0078,
            Group2InterruptFlags      = 0x0080,
            Group2InterruptEnable     = 0x0084,
            Group2Control             = 0x0088,
            Group2Compare0            = 0x008C,
            Group2SyncBusy            = 0x0098,
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
            Group1InterruptFlags_Set  = 0x1060,
            Group1InterruptEnable_Set = 0x1064,
            Group1Control_Set         = 0x1068,
            Group1Compare0_Set        = 0x106C,
            Group1Compare1_Set        = 0x1070,
            Group1Capture0_Set        = 0x1074,
            Group1SyncBusy_Set        = 0x1078,
            Group2InterruptFlags_Set  = 0x1080,
            Group2InterruptEnable_Set = 0x1084,
            Group2Control_Set         = 0x1088,
            Group2Compare0_Set        = 0x108C,
            Group2SyncBusy_Set        = 0x1098,
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
            Group1InterruptFlags_Clr  = 0x2060,
            Group1InterruptEnable_Clr = 0x2064,
            Group1Control_Clr         = 0x2068,
            Group1Compare0_Clr        = 0x206C,
            Group1Compare1_Clr        = 0x2070,
            Group1Capture0_Clr        = 0x2074,
            Group1SyncBusy_Clr        = 0x2078,
            Group2InterruptFlags_Clr  = 0x2080,
            Group2InterruptEnable_Clr = 0x2084,
            Group2Control_Clr         = 0x2088,
            Group2Compare0_Clr        = 0x208C,
            Group2SyncBusy_Clr        = 0x2098,
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
            Group1InterruptFlags_Tgl  = 0x3060,
            Group1InterruptEnable_Tgl = 0x3064,
            Group1Control_Tgl         = 0x3068,
            Group1Compare0_Tgl        = 0x306C,
            Group1Compare1_Tgl        = 0x3070,
            Group1Capture0_Tgl        = 0x3074,
            Group1SyncBusy_Tgl        = 0x3078,
            Group2InterruptFlags_Tgl  = 0x3080,
            Group2InterruptEnable_Tgl = 0x3084,
            Group2Control_Tgl         = 0x3088,
            Group2Compare0_Tgl        = 0x308C,
            Group2SyncBusy_Tgl        = 0x3098,
        }

        protected enum CompareMatchOutputAction
        {
            Clear  = 0, // Cleared on the next cycle
            Set    = 1, // Set on the next cycle
            Pulse  = 2, // Set on the next cycle, cleared on the cycle after
            Toggle = 3, // Inverted on the next cycle
            CpmIf  = 4, // Export this channel's CMP IF
        }

        protected enum CaptureEdgeSelect
        {
            Rising  = 0, // Rising edges detected
            Falling = 1, // Falling edges detected
            Both    = 2, // Both edges detected
        }
#endregion        
    }
}