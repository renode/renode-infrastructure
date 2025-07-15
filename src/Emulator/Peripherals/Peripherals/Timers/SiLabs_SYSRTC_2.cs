//
// Copyright (c) 2010-2025 Silicon Labs
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
    public class SiLabs_SYSRTC_2 : IDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_SYSRTC_2(Machine machine, uint frequency)
        {
            this.machine = machine;
            this.timerFrequency = frequency;
            
            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "sysrtctimer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            msTimer = new LimitTimer(machine.ClockSource, 1000, this, "sysrtcmstimer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            msTimer.LimitReached += MsTimerLimitReached;

            MsIRQ = new GPIO();
            AppIRQ = new GPIO();
            AppAlternateIRQ = new GPIO();

            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            timerIsRunning = false;
            msTimerIsRunning = false;
            timer.Enabled = false;
            msTimer.Enabled = false;
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
                if (LogRegisterAccess && !internal_read)
                {  
                    this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (LogRegisterAccess && !internal_read)
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
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            if (LogRegisterAccess && !internal_read)
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
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
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
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enable, name: "EN")
                    .WithTaggedFlag("DISABLING", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartCommand(); } }, name: "START")
                  .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StopCommand(); } }, name: "STOP")
                  .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) { MsStartCommand(); } }, name: "MSSTART")
                  .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) { MsStopCommand(); } }, name: "MSSTOP")
                  .WithReservedBits(4, 28)
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithTaggedFlag("LOCKSTATUS", 1)
                    .WithTaggedFlag("FAILDETLOCKSTATUS", 2)
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => msTimerIsRunning, name: "MSRUNNING")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                },
                {(long)Registers.MsCounter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>  MsTimerCounter, name: "MSCNT")
                },
                {(long)Registers.MsCompareValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out msCounterCompareValue, name: "MSCMPVAL")
                    .WithChangeCallback((_, __) => RestartMsTimer())
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out msCounterOverflowInterrupt, name: "MSOVFIF")
                    .WithFlag(1, out msCounterCompareInterrupt, name: "MSCMPIF")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out msCounterOverflowInterruptEnable, name: "MSOVFIEN")
                    .WithFlag(1, out msCounterCompareInterruptEnable, writeCallback: (_, value) => {if (value) RestartMsTimer();}, name: "MSCMPIEN")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
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
                    .WithTag("CMP0CMOA", 3, 3)
                    .WithTag("CMP1CMOA", 6, 3)
                    .WithTag("CAP0EDGE", 9, 2)
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
                {(long)Registers.Group0PreTrigger, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out group0PretriggerHfxoStart, name: "HFXOSTART")
                    .WithValueField(4, 4, out group0PretriggerEmuWakeup, name: "EMUWAKEUP")
                    .WithFlag(8, out group0PretriggerHfxoActive, name: "HFXOACTIVE")
                    .WithFlag(9, out group0PretriggerEmuActive, name: "EMUACTIVE")
                    .WithReservedBits(10, 22)
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
                    .WithFlag(4, out group1AlternateOverflowInterruptEnable, name: "ALTOVFIF")
                    .WithFlag(5, out group1AlternateCompare0InterruptEnable, name: "ALTCOMP0IF")
                    .WithFlag(6, out group1AlternateCompare1InterruptEnable, name: "ALTCOMP1IF")
                    .WithFlag(7, out group1AlternateCapture0InterruptEnable, name: "ALTCAP0IF")
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Group1Control, new DoubleWordRegister(this)
                    .WithFlag(0, out group1Compare0Enable, name: "CMP0EN")
                    .WithFlag(1, out group1Compare1Enable, name: "CMP1EN")
                    .WithFlag(2, out group1Capture0Enable, name: "CAP0EN")
                    .WithTag("CMP0CMOA", 3, 3)
                    .WithTag("CMP1CMOA", 6, 3)
                    .WithTag("CAP0EDGE", 9, 2)
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
                {(long)Registers.Group1PreTrigger, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out group1PretriggerHfxoStart, name: "HFXOSTART")
                    .WithValueField(4, 4, out group1PretriggerEmuWakeup, name: "EMUWAKEUP")
                    .WithFlag(8, out group1PretriggerHfxoActive, name: "HFXOACTIVE")
                    .WithFlag(9, out group1PretriggerEmuActive, name: "EMUACTIVE")
                    .WithReservedBits(10, 22)
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                // Group2 is reserved to the SE, which is not modeled.
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO MsIRQ { get; }
        public GPIO AppIRQ { get; }
        public GPIO AppAlternateIRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private LimitTimer timer;
        private LimitTimer msTimer;
        private uint timerFrequency;
        private bool timerIsRunning = false;
        private bool msTimerIsRunning = false; 
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        public bool LogRegisterAccess = false;
        public bool LogInterrupts = false;

        // A reference to hfxo so that the sysrtc can call methods the hfxo exposes
        private SiLabs_IHFXO _hfxo;
        public SiLabs_IHFXO hfxo
        {
            get
            {
                if (Object.ReferenceEquals(_hfxo, null))
                {
                    foreach(var hfxo in machine.GetPeripheralsOfType<SiLabs_IHFXO>())
                    {
                        _hfxo = hfxo;
                    }
                }
                return _hfxo;
            }
            set
            {
                _hfxo = value;
            }
        }
        
#region register fields
        private IFlagRegisterField enable;
        private IValueRegisterField msCounterCompareValue;
        private IFlagRegisterField group0Compare0Enable;
        private IFlagRegisterField group0Compare1Enable;
        private IFlagRegisterField group0Capture0Enable;
        private IValueRegisterField group0Compare0Value;
        private IValueRegisterField group0Compare1Value;
        private IValueRegisterField group0Capture0Value;
        private IValueRegisterField group0PretriggerHfxoStart;
        private IValueRegisterField group0PretriggerEmuWakeup;
        private IFlagRegisterField group0PretriggerHfxoActive;
        private IFlagRegisterField group0PretriggerEmuActive;
        private IFlagRegisterField group1Compare0Enable;
        private IFlagRegisterField group1Compare1Enable;
        private IFlagRegisterField group1Capture0Enable;
        private IValueRegisterField group1Compare0Value;
        private IValueRegisterField group1Compare1Value;
        private IValueRegisterField group1Capture0Value;
        private IValueRegisterField group1PretriggerHfxoStart;
        private IValueRegisterField group1PretriggerEmuWakeup;
        private IFlagRegisterField group1PretriggerHfxoActive;
        private IFlagRegisterField group1PretriggerEmuActive;

        // Interrupts
        private IFlagRegisterField msCounterOverflowInterrupt;
        private IFlagRegisterField msCounterCompareInterrupt;
        private IFlagRegisterField msCounterOverflowInterruptEnable;
        private IFlagRegisterField msCounterCompareInterruptEnable;
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

        public uint MsTimerCounter
        {
            get
            {
                if (msTimerIsRunning)
                {
                    if (msTimer.Enabled)
                    {
                        TrySyncTime();
                        return (uint)msTimer.Value;
                    }
                    else
                    {
                        return (uint)msTimer.Limit;
                    }
                }
                return 0;
            }
            // Set is only used for testing purposes, the MSCNT register is read-only
            set
            {
                msTimer.Value = value;
                RestartMsTimer();
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

        private void MsStartCommand()
        {
            if (msTimerIsRunning)
            {
                return;
            }

            msTimerIsRunning = true;
            RestartMsTimer();
        }

        private void MsStopCommand()
        {
            msTimerIsRunning = false;
            msTimer.Enabled = false;
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
            }
            if (group0Compare1Enable.Value && timer.Limit == group0Compare1Value.Value + 1)
            {
                group0Compare1Interrupt.Value = true;
            }
            if (group1Compare0Enable.Value && timer.Limit == group1Compare0Value.Value + 1)
            {
                group1Compare0Interrupt.Value = true;
                group1AlternateCompare0Interrupt.Value = true;
            }
            if (group1Compare1Enable.Value && timer.Limit == group1Compare1Value.Value + 1)
            {
                group1Compare1Interrupt.Value = true;
                group1AlternateCompare1Interrupt.Value = true;
            }
            if (!group0PretriggerHfxoActive.Value && group0PretriggerHfxoStart.Value > 0
                && (group0Compare0Enable.Value && timer.Limit == group0Compare0Value.Value + 1 - group0PretriggerHfxoStart.Value)
                )
            {
                group0PretriggerHfxoActive.Value = true;   // Cleared by software
                hfxo.OnRequest(HFXO_REQUESTER.SYSRTC);
            }
            if (!group0PretriggerEmuActive.Value && group0PretriggerEmuWakeup.Value > 0
                && (group0Compare0Enable.Value && timer.Limit == group0Compare0Value.Value + 1 - group0PretriggerEmuWakeup.Value)
                )
            {
                group0PretriggerEmuActive.Value = true;   // Cleared by software
                // Do nothing. EMU is not implemented in Renode
            }
            if (!group1PretriggerHfxoActive.Value && group1PretriggerHfxoStart.Value > 0
                && (group1Compare0Enable.Value && timer.Limit == group1Compare0Value.Value + 1 - group1PretriggerHfxoStart.Value)
                )
            {
                group1PretriggerHfxoActive.Value = true;   // Cleared by software
                hfxo.OnRequest(HFXO_REQUESTER.SYSRTC);
            }
            if (!group1PretriggerEmuActive.Value && group1PretriggerEmuWakeup.Value > 0
                && (group1Compare0Enable.Value && timer.Limit == group1Compare0Value.Value + 1 - group1PretriggerEmuWakeup.Value)
                )
            {
                group1PretriggerEmuActive.Value = true;   // Cleared by software
                // Do nothing. EMU is not implemented in Renode
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

            // Compute the next pretrigger for each group.

            uint group0Pretrigger = 0;
            if (!group0PretriggerHfxoActive.Value && !group0PretriggerEmuActive.Value)
            {
                group0Pretrigger = (uint)Math.Max(group0PretriggerHfxoStart.Value, group0PretriggerEmuWakeup.Value);
            }
            else if (!group0PretriggerHfxoActive.Value)
            {
                group0Pretrigger = (uint)group0PretriggerHfxoStart.Value;
            }
            else if (!group0PretriggerEmuActive.Value)
            {
                group0Pretrigger = (uint)group0PretriggerEmuWakeup.Value;
            }

            uint group1Pretrigger = 0;
            if (!group1PretriggerHfxoActive.Value && !group1PretriggerEmuActive.Value)
            {
                group1Pretrigger = (uint)Math.Max(group1PretriggerHfxoStart.Value, group1PretriggerEmuWakeup.Value);
            }
            else if (!group1PretriggerHfxoActive.Value)
            {
                group1Pretrigger = (uint)group1PretriggerHfxoStart.Value;
            }
            else if (!group1PretriggerEmuActive.Value)
            {
                group1Pretrigger = (uint)group1PretriggerEmuWakeup.Value;
            }

            // Compare interrupt fires "on the next cycle", therefore we just set 
            // the timer to the +1 value and fire the interrupt right away when
            // we hit the limit.

            if (group0Compare0Enable.Value 
                && currentValue < (group0Compare0Value.Value + 1 - group0Pretrigger)
                && (group0Compare0Value.Value + 1 - group0Pretrigger) < limit)
            {
                limit = (uint)group0Compare0Value.Value + 1 - group0Pretrigger;
            }

            if (group0Compare1Enable.Value 
                && currentValue < (group0Compare1Value.Value + 1)
                && (group0Compare1Value.Value + 1) < limit)
            {
                limit = (uint)group0Compare1Value.Value + 1;
            }

            if (group1Compare0Enable.Value 
                && currentValue < (group1Compare0Value.Value + 1 - group1Pretrigger)
                && (group1Compare0Value.Value + 1 - group1Pretrigger) < limit)
            {
                limit = (uint)group1Compare0Value.Value + 1 - group1Pretrigger;
            }

            if (group1Compare1Enable.Value 
                && currentValue < (group1Compare1Value.Value + 1)
                && (group1Compare1Value.Value + 1) < limit)
            {
                limit = (uint)group1Compare1Value.Value + 1;
            }

            // RENODE-65: add support for capture functionality

            timer.Frequency = timerFrequency;
            timer.Limit = limit;
            timer.Enabled = true;
            timer.Value = currentValue;
        }

        private void MsTimerLimitReached()
        {
            if (!msTimerIsRunning)
            {
                return;
            }

            bool overflow = (msTimer.Limit == 0xFFFFFFFF); 

            // For now we assume MS compare is enabled if the corresponding interrupt flag is enabled.
            if (msCounterCompareInterruptEnable.Value && MsTimerCounter == msCounterCompareValue.Value)
            {
                msCounterCompareInterrupt.Value = true;
                
            }

            if (msCounterOverflowInterruptEnable.Value && overflow)
            {
                msCounterOverflowInterrupt.Value = true;
            }
            
            UpdateInterrupts();
            RestartMsTimer(overflow);
        }

        private void RestartMsTimer(bool restartFromZero = false)
        {
            uint currentValue = restartFromZero ? 0 : MsTimerCounter;
            uint limit = 0xFFFFFFFF;

            // For now we assume MS compare is enabled if the corresponding interrupt flag is enabled.
            if (msCounterCompareInterruptEnable.Value
                && msCounterCompareValue.Value > currentValue)
            {
                limit = (uint)msCounterCompareValue.Value;
            }

            msTimer.Limit = limit;
            msTimer.Enabled = true;
            msTimer.Value = currentValue;
        }
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((msCounterCompareInterruptEnable.Value && msCounterCompareInterrupt.Value)
                           || (msCounterOverflowInterruptEnable.Value && msCounterOverflowInterrupt.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out IF);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: MS_IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                MsIRQ.Set(irq);
                        
                irq = ((group0OverflowInterruptEnable.Value && group0OverflowInterrupt.Value)
                       || (group0Compare0InterruptEnable.Value && group0Compare0Interrupt.Value)
                       || (group0Compare1InterruptEnable.Value && group0Compare1Interrupt.Value)
                       || (group0Capture0InterruptEnable.Value && group0Capture0Interrupt.Value)
                       || (group1OverflowInterruptEnable.Value && group1OverflowInterrupt.Value)
                       || (group1Compare0InterruptEnable.Value && group1Compare0Interrupt.Value)
                       || (group1Compare1InterruptEnable.Value && group1Compare1Interrupt.Value)
                       || (group1Capture0InterruptEnable.Value && group1Capture0Interrupt.Value));
                if (LogInterrupts && irq)
                {
                    var IF0 = 0U;
                    var IEN0 = 0U;
                    var IF1 = 0U;
                    var IEN1 = 0U;
                    registersCollection.TryRead((long)Registers.Group0InterruptFlags, out IF0);
                    registersCollection.TryRead((long)Registers.Group0InterruptEnable, out IEN0);
                    registersCollection.TryRead((long)Registers.Group1InterruptFlags, out IF1);
                    registersCollection.TryRead((long)Registers.Group1InterruptEnable, out IEN1);
                    this.Log(LogLevel.Info, "{0}: APP_IRQ set (IF_GRP0=0x{1:X}, IEN_GRP0=0x{2:X} IF_GRP1=0x{3:X}, IEN_GRP1=0x{4:X})", this.GetTime(), IF0, IEN0, IF1, IEN1);
                }
                AppIRQ.Set(irq);

                irq = ((group1AlternateOverflowInterruptEnable.Value && group1AlternateOverflowInterrupt.Value)
                       || (group1AlternateCompare0InterruptEnable.Value && group1AlternateCompare0Interrupt.Value)
                       || (group1AlternateCompare1InterruptEnable.Value && group1AlternateCompare1Interrupt.Value)
                       || (group1AlternateCapture0InterruptEnable.Value && group1AlternateCapture0Interrupt.Value));
                if (LogInterrupts && irq)
                {
                    var IF1 = 0U;
                    var IEN1 = 0U;
                    registersCollection.TryRead((long)Registers.Group1InterruptFlags, out IF1);
                    registersCollection.TryRead((long)Registers.Group1InterruptEnable, out IEN1);
                    this.Log(LogLevel.Info, "{0}: APP_ALT_IRQ set (IF_GRP1=0x{1:X}, IEN_GRP1=0x{2:X})", this.GetTime(), IF1, IEN1);
                }
                AppAlternateIRQ.Set(irq);                
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
#endregion

#region enums
        private enum Registers
        {
            IpVersion                       = 0x0000,
            Enable                          = 0x0004,
            SoftwareReset                   = 0x0008,
            Config                          = 0x000C,
            Command                         = 0x0010,
            Status                          = 0x0014,
            Counter                         = 0x0018,
            SyncBusy                        = 0x001C,
            Lock                            = 0x0020,
            MsCounter                       = 0x0024,
            MsCompareValue                  = 0x0028,
            MsBufferedCompareValue          = 0x002C,
            InterruptFlags                  = 0x0030,
            InterruptEnable                 = 0x0034,
            FailureDetection                = 0x0040,
            FailureDetectionLock            = 0x0044,
            Group0InterruptFlags            = 0x0050,
            Group0InterruptEnable           = 0x0054,
            Group0Control                   = 0x0058,
            Group0Compare0                  = 0x005C,
            Group0Compare1                  = 0x0060,
            Group0Capture0                  = 0x0064,
            Group0SyncBusy                  = 0x0068,
            Group0PreTrigger                = 0x006C,
            Group1InterruptFlags            = 0x0080,
            Group1InterruptEnable           = 0x0084,
            Group1Control                   = 0x0088,
            Group1Compare0                  = 0x008C,
            Group1Compare1                  = 0x0090,
            Group1Capture0                  = 0x0094,
            Group1SyncBusy                  = 0x0098,
            Group1PreTrigger                = 0x009C,
            Group2InterruptFlags            = 0x00B0,
            Group2InterruptEnable           = 0x00B4,
            Group2Control                   = 0x00B8,
            Group2Compare0                  = 0x00BC,
            Group2SyncBusy                  = 0x00C8,
            // Set registers
            IpVersion_Set                   = 0x1000,
            Enable_Set                      = 0x1004,
            SoftwareReset_Set               = 0x1008,
            Config_Set                      = 0x100C,
            Command_Set                     = 0x1010,
            Status_Set                      = 0x1014,
            Counter_Set                     = 0x1018,
            SyncBusy_Set                    = 0x101C,
            Lock_Set                        = 0x1020,
            MsCounter_Set                   = 0x1024,
            MsCompareValue_Set              = 0x1028,
            MsBufferedCompareValue_Set      = 0x102C,
            InterruptFlags_Set              = 0x1030,
            InterruptEnable_Set             = 0x1034,
            FailureDetection_Set            = 0x1040,
            FailureDetectionLock_Set        = 0x1044,
            Group0InterruptFlags_Set        = 0x1050,
            Group0InterruptEnable_Set       = 0x1054,
            Group0Control_Set               = 0x1058,
            Group0Compare0_Set              = 0x105C,
            Group0Compare1_Set              = 0x1060,
            Group0Capture0_Set              = 0x1064,
            Group0SyncBusy_Set              = 0x1068,
            Group0PreTrigger_Set            = 0x106C,
            Group1InterruptFlags_Set        = 0x1080,
            Group1InterruptEnable_Set       = 0x1084,
            Group1Control_Set               = 0x1088,
            Group1Compare0_Set              = 0x108C,
            Group1Compare1_Set              = 0x1090,
            Group1Capture0_Set              = 0x1094,
            Group1SyncBusy_Set              = 0x1098,
            Group1PreTrigger_Set            = 0x109C,
            Group2InterruptFlags_Set        = 0x10B0,
            Group2InterruptEnable_Set       = 0x10B4,
            Group2Control_Set               = 0x10B8,
            Group2Compare0_Set              = 0x10BC,
            Group2SyncBusy_Set              = 0x10C8,
            // Clear registers
            IpVersion_Clr                   = 0x2000,
            Enable_Clr                      = 0x2004,
            SoftwareReset_Clr               = 0x2008,
            Config_Clr                      = 0x200C,
            Command_Clr                     = 0x2010,
            Status_Clr                      = 0x2014,
            Counter_Clr                     = 0x2018,
            SyncBusy_Clr                    = 0x201C,
            Lock_Clr                        = 0x2020,
            MsCounter_Clr                   = 0x2024,
            MsCompareValue_Clr              = 0x2028,
            MsBufferedCompareValue_Clr      = 0x202C,
            InterruptFlags_Clr              = 0x2030,
            InterruptEnable_Clr             = 0x2034,
            FailureDetection_Clr            = 0x2040,
            FailureDetectionLock_Clr        = 0x2044,
            Group0InterruptFlags_Clr        = 0x2050,
            Group0InterruptEnable_Clr       = 0x2054,
            Group0Control_Clr               = 0x2058,
            Group0Compare0_Clr              = 0x205C,
            Group0Compare1_Clr              = 0x2060,
            Group0Capture0_Clr              = 0x2064,
            Group0SyncBusy_Clr              = 0x2068,
            Group0PreTrigger_Clr            = 0x206C,
            Group1InterruptFlags_Clr        = 0x2080,
            Group1InterruptEnable_Clr       = 0x2084,
            Group1Control_Clr               = 0x2088,
            Group1Compare0_Clr              = 0x208C,
            Group1Compare1_Clr              = 0x2090,
            Group1Capture0_Clr              = 0x2094,
            Group1SyncBusy_Clr              = 0x2098,
            Group1PreTrigger_Clr            = 0x209C,
            Group2InterruptFlags_Clr        = 0x20B0,
            Group2InterruptEnable_Clr       = 0x20B4,
            Group2Control_Clr               = 0x20B8,
            Group2Compare0_Clr              = 0x20BC,
            Group2SyncBusy_Clr              = 0x20C8,
             // Toggle registers
            IpVersion_Tgl                   = 0x3000,
            Enable_Tgl                      = 0x3004,
            SoftwareReset_Tgl               = 0x3008,
            Config_Tgl                      = 0x300C,
            Command_Tgl                     = 0x3010,
            Status_Tgl                      = 0x3014,
            Counter_Tgl                     = 0x3018,
            SyncBusy_Tgl                    = 0x301C,
            Lock_Tgl                        = 0x3020,
            MsCounter_Tgl                   = 0x3024,
            MsCompareValue_Tgl              = 0x3028,
            MsBufferedCompareValue_Tgl      = 0x302C,
            InterruptFlags_Tgl              = 0x3030,
            InterruptEnable_Tgl             = 0x3034,
            FailureDetection_Tgl            = 0x3040,
            FailureDetectionLock_Tgl        = 0x3044,
            Group0InterruptFlags_Tgl        = 0x3050,
            Group0InterruptEnable_Tgl       = 0x3054,
            Group0Control_Tgl               = 0x3058,
            Group0Compare0_Tgl              = 0x305C,
            Group0Compare1_Tgl              = 0x3060,
            Group0Capture0_Tgl              = 0x3064,
            Group0SyncBusy_Tgl              = 0x3068,
            Group0PreTrigger_Tgl            = 0x306C,
            Group1InterruptFlags_Tgl        = 0x3080,
            Group1InterruptEnable_Tgl       = 0x3084,
            Group1Control_Tgl               = 0x3088,
            Group1Compare0_Tgl              = 0x308C,
            Group1Compare1_Tgl              = 0x3090,
            Group1Capture0_Tgl              = 0x3094,
            Group1SyncBusy_Tgl              = 0x3098,
            Group1PreTrigger_Tgl            = 0x309C,
            Group2InterruptFlags_Tgl        = 0x30B0,
            Group2InterruptEnable_Tgl       = 0x30B4,
            Group2Control_Tgl               = 0x30B8,
            Group2Compare0_Tgl              = 0x30BC,
            Group2SyncBusy_Tgl              = 0x30C8,
        }
#endregion        
    }
}