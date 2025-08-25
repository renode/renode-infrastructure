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

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SiLabs_TIMER_2 : IDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_TIMER_2(Machine machine, uint frequency, bool logRegisterAccess = false, bool logInterrupts = false)
        {
            this.machine = machine;
            this.timerFrequency = frequency;
            this.LogRegisterAccess = logRegisterAccess;
            this.LogInterrupts = logInterrupts;
            
            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "timer_2", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            channel = new Channel[NumberOfChannels];
            for(var idx = 0; idx < NumberOfChannels; ++idx)
            {
                var i = idx;
                channel[i] = new Channel(machine, this, (uint)i);
            }

            IRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            timerIsRunning = false;
            timer.Enabled = false;
            topValue = TopValueInitValue;
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
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, TimerMode>(0, 2, out timerMode, name: "MODE")
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("SYNC", 3)
                    .WithFlag(4, out oneShotMode, name: "OSMEN")
                    .WithTaggedFlag("QDM", 5)
                    .WithTaggedFlag("DEBUGRUN", 6)
                    .WithTaggedFlag("DMACLRACT", 7)
                    .WithEnumField<DoubleWordRegister, ClockSource>(8, 2, out clockSource, name: "CLKSEL")
                    .WithTaggedFlag("RETIMEEN", 10)
                    .WithTaggedFlag("DISSYNCOUT", 11)
                    .WithTaggedFlag("RETIMESEL", 12)
                    .WithTaggedFlag("UPDATEMODE", 13)
                    .WithReservedBits(14, 2)
                    .WithTaggedFlag("ATI", 16)
                    .WithTaggedFlag("RSSCOIST", 17)
                    .WithValueField(18, 10, out prescaler, name: "PRESC")
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithTag("RISEA", 0, 2)
                    .WithTag("FALLA", 2, 2)
                    .WithTaggedFlag("X2CNT", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartCommand(); } }, name: "START")
                  .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StopCommand(); } }, name: "STOP")
                  .WithTaggedFlag("UPDATECMD", 2)
                  .WithReservedBits(3, 29)
                },
                {(long)Registers.Top, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => topValue, writeCallback: (_, value) => topValue = (uint)value, name: "TOP")
                    .WithChangeCallback((_, __) => RestartTimer(false))
                },
                {(long)Registers.TopBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => topValue, writeCallback: (_, value) => topValue = (uint)value, name: "TOPB")
                    .WithChangeCallback((_, __) => RestartTimer(false))
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => (timer.Direction == Direction.Descending), name: "DIR")
                    .WithTaggedFlag("TOPBV", 2)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("TIMERLOCKSTATUS", 4)
                    .WithTaggedFlag("DTILOCKSTATUS", 5)
                    .WithTaggedFlag("SYNCBUSY", 6)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("OCBV0", 8)
                    .WithTaggedFlag("OCBV1", 9)
                    .WithTaggedFlag("OCBV2", 10)
                    .WithTaggedFlag("OCBV3", 11)
                    .WithTaggedFlag("OCBV4", 12)
                    .WithTaggedFlag("OCBV5", 13)
                    .WithTaggedFlag("OCBV6", 14)
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("ICFEMPTY0", 16)
                    .WithTaggedFlag("ICFEMPTY1", 17)
                    .WithTaggedFlag("ICFEMPTY2", 18)
                    .WithTaggedFlag("ICFEMPTY3", 19)
                    .WithTaggedFlag("ICFEMPTY4", 20)
                    .WithTaggedFlag("ICFEMPTY5", 21)
                    .WithTaggedFlag("ICFEMPTY6", 22)
                    .WithReservedBits(23, 1)
                    .WithTaggedFlag("CCPOL0", 24)
                    .WithTaggedFlag("CCPOL1", 25)
                    .WithTaggedFlag("CCPOL2", 26)
                    .WithTaggedFlag("CCPOL3", 27)
                    .WithTaggedFlag("CCPOL4", 28)
                    .WithTaggedFlag("CCPOL5", 29)
                    .WithTaggedFlag("CCPOL6", 30)
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterrupt, name: "OFIF")
                    .WithFlag(1, out underflowInterrupt, name: "UFIF")
                    .WithTaggedFlag("DIRCHGIF", 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].captureCompareInterrupt, name: "CCIF0")
                    .WithFlag(5, out channel[1].captureCompareInterrupt, name: "CCIF1")
                    .WithFlag(6, out channel[2].captureCompareInterrupt, name: "CCIF2")
                    .WithFlag(7, out channel[3].captureCompareInterrupt, name: "CCIF3")
                    .WithFlag(8, out channel[4].captureCompareInterrupt, name: "CCIF4")
                    .WithFlag(9, out channel[5].captureCompareInterrupt, name: "CCIF5")
                    .WithFlag(10, out channel[6].captureCompareInterrupt, name: "CCIF6")
                    .WithTaggedFlag("ICFWLFULLIF0", 11)
                    .WithTaggedFlag("ICFWLFULLIF1", 12)
                    .WithTaggedFlag("ICFWLFULLIF2", 13)
                    .WithTaggedFlag("ICFWLFULLIF3", 14)
                    .WithTaggedFlag("ICFWLFULLIF4", 15)
                    .WithTaggedFlag("ICFWLFULLIF5", 16)
                    .WithTaggedFlag("ICFWLFULLIF6", 17)
                    .WithTaggedFlag("ICFOFIF0", 18)
                    .WithTaggedFlag("ICFOFIF1", 19)
                    .WithTaggedFlag("ICFOFIF2", 20)
                    .WithTaggedFlag("ICFOFIF3", 21)
                    .WithTaggedFlag("ICFOFIF4", 22)
                    .WithTaggedFlag("ICFOFIF5", 23)
                    .WithTaggedFlag("ICFOFIF6", 24)
                    .WithTaggedFlag("ICFUFIF0", 25)
                    .WithTaggedFlag("ICFUFIF0", 26)
                    .WithTaggedFlag("ICFUFIF0", 27)
                    .WithTaggedFlag("ICFUFIF0", 28)
                    .WithTaggedFlag("ICFUFIF0", 29)
                    .WithTaggedFlag("ICFUFIF0", 30)
                    .WithTaggedFlag("ICFUFIF0", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterruptEnable, name: "OFIEN")
                    .WithFlag(1, out underflowInterruptEnable, name: "UFIEN")
                    .WithTaggedFlag("DIRCHGIEN", 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].captureCompareInterruptEnable, name: "CCIEN0")
                    .WithFlag(5, out channel[1].captureCompareInterruptEnable, name: "CCIEN1")
                    .WithFlag(6, out channel[2].captureCompareInterruptEnable, name: "CCIEN2")
                    .WithFlag(7, out channel[3].captureCompareInterruptEnable, name: "CCIEN3")
                    .WithFlag(8, out channel[4].captureCompareInterruptEnable, name: "CCIEN4")
                    .WithFlag(9, out channel[5].captureCompareInterruptEnable, name: "CCIEN5")
                    .WithFlag(10, out channel[6].captureCompareInterruptEnable, name: "CCIEN6")
                    .WithTaggedFlag("ICFWLFULLIEN0", 11)
                    .WithTaggedFlag("ICFWLFULLIEN1", 12)
                    .WithTaggedFlag("ICFWLFULLIEN2", 13)
                    .WithTaggedFlag("ICFWLFULLIEN3", 14)
                    .WithTaggedFlag("ICFWLFULLIEN4", 15)
                    .WithTaggedFlag("ICFWLFULLIEN5", 16)
                    .WithTaggedFlag("ICFWLFULLIEN6", 17)
                    .WithTaggedFlag("ICFOFIEN0", 18)
                    .WithTaggedFlag("ICFOFIEN1", 19)
                    .WithTaggedFlag("ICFOFIEN2", 20)
                    .WithTaggedFlag("ICFOFIEN3", 21)
                    .WithTaggedFlag("ICFOFIEN4", 22)
                    .WithTaggedFlag("ICFOFIEN5", 23)
                    .WithTaggedFlag("ICFOFIEN6", 24)
                    .WithTaggedFlag("ICFUFIEN0", 25)
                    .WithTaggedFlag("ICFUFIEN0", 26)
                    .WithTaggedFlag("ICFUFIEN0", 27)
                    .WithTaggedFlag("ICFUFIEN0", 28)
                    .WithTaggedFlag("ICFUFIEN0", 29)
                    .WithTaggedFlag("ICFUFIEN0", 30)
                    .WithTaggedFlag("ICFUFIEN0", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                },
            };

            var startOffset = (long)Registers.Channel0Config;
            var configOffset = (long)Registers.Channel0Config - startOffset;
            var controlOffset = (long)Registers.Channel0Control - startOffset;
            var phaseOffset = (long)Registers.Channel0Phase - startOffset;
            var phaseBufferOffset = (long)Registers.Channel0PhaseBuffer - startOffset;
            var outputCompareOffset = (long)Registers.Channel0OutputCompare - startOffset;
            var outputCompareBufferOffset = (long)Registers.Channel0OutputCompareBuffer - startOffset;
            var ditherOffset = (long)Registers.Channel0Dither - startOffset;
            var ditherBufferOffset = (long)Registers.Channel0DitherBuffer - startOffset;
            var inputCaptureFifoOffset = (long)Registers.Channel0InputCapture - startOffset;
            var inputCaptureOverflowOffset = (long)Registers.Channel0InputCaptureOverflow - startOffset;
            var blockSize = (long)Registers.Channel1Config - (long)Registers.Channel0Config;
            for(var index = 0; index < NumberOfChannels; index++)
            {
                var i = index;
                // Channel_n_Config
                registerDictionary.Add(startOffset + blockSize*i + configOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, ChannelMode>(0, 2, out channel[i].mode, name: "MODE")
                        .WithReservedBits(2, 2)
                        .WithFlag(4, out channel[i].compareOutputInitialState, name: "COIST")
                        .WithReservedBits(5, 12)
                        .WithTag("INSEL", 17, 2)
                        .WithTaggedFlag("PRSCONF", 19)
                        .WithTaggedFlag("FILT", 20)
                        .WithTaggedFlag("ICFWL", 21)
                        .WithReservedBits(22, 10)
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_Control
                registerDictionary.Add(startOffset + blockSize*i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithReservedBits(0, 2)
                        .WithTaggedFlag("OUTINV", 2)
                        .WithReservedBits(3, 5)
                        .WithTag("CMOA", 8, 2)
                        .WithTag("COFOA", 10, 2)
                        .WithTag("CUFOA", 12, 2)
                        .WithReservedBits(14, 10)
                        .WithTag("ICEDGE", 24, 2)
                        .WithTag("ICEVCTRL", 26, 2)
                        .WithReservedBits(28, 4)
                );
                // Channel_n_Phase
                registerDictionary.Add(startOffset + blockSize*i + phaseOffset,
                    new DoubleWordRegister(this)
                        .WithTag("PHASE", 0, 32)
                );
                // Channel_n_PhaseBuffer
                registerDictionary.Add(startOffset + blockSize*i + phaseBufferOffset,
                    new DoubleWordRegister(this)
                        .WithTag("PHASEB", 0, 32)
                );
                // Channel_n_OutputCompare
                registerDictionary.Add(startOffset + blockSize*i + outputCompareOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => channel[i].outputCompareValue, writeCallback: (_, value) => channel[i].outputCompareValue = (uint)value, name: "OC")
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_OutputCompareBuffer
                registerDictionary.Add(startOffset + blockSize*i + outputCompareBufferOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => channel[i].outputCompareValue, writeCallback: (_, value) => channel[i].outputCompareValue = (uint)value, name: "OCB")
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_Dither
                registerDictionary.Add(startOffset + blockSize*i + ditherOffset,
                    new DoubleWordRegister(this)
                        .WithTag("DITHER", 0, 4)
                        .WithReservedBits(4, 28)
                );
                // Channel_n_DitherBuffer
                registerDictionary.Add(startOffset + blockSize*i + ditherBufferOffset,
                    new DoubleWordRegister(this)
                        .WithTag("DITHERB", 0, 4)
                        .WithReservedBits(4, 28)
                );
                // Channel_n_InputCaptureFifo
                registerDictionary.Add(startOffset + blockSize*i + inputCaptureFifoOffset,
                    new DoubleWordRegister(this)
                        .WithTag("ICF", 0, 32)
                );
                // Channel_n_InputCaptureOverflow
                registerDictionary.Add(startOffset + blockSize*i + inputCaptureOverflowOffset,
                    new DoubleWordRegister(this)
                        .WithTag("ICOF", 0, 32)
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private LimitTimer timer;
        private uint timerFrequency;
        private bool timerIsRunning = false;
        private Channel[] channel;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint NumberOfChannels = 7;
        private const uint TopValueInitValue = 0xFFFF;
        private uint topValue = TopValueInitValue;
        private bool LogRegisterAccess;
        private bool LogInterrupts;
        
#region register fields
        private IEnumRegisterField<TimerMode> timerMode;
        private IFlagRegisterField oneShotMode;
        private IEnumRegisterField<ClockSource> clockSource;
        private IValueRegisterField prescaler;
        // Interrupt fields
        private IFlagRegisterField overflowInterrupt;
        private IFlagRegisterField underflowInterrupt;
        private IFlagRegisterField overflowInterruptEnable;
        private IFlagRegisterField underflowInterruptEnable;
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        public uint Frequency
        {
            get
            {
                double frequency;
                switch(clockSource.Value)
                {
                    case ClockSource.Prescaled:
                    // The selected timer clock will be divided by PRESC+1 before clocking the counter
                    frequency = (double)timerFrequency / (double)(prescaler.Value + 1);
                    break;
                    default:
                        // TODO: for now we only support "prescaled" clock source
                        throw new Exception("Clock source unsupported");
                }
                return (uint)frequency;
            }
        }

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
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = (overflowInterruptEnable.Value && overflowInterrupt.Value)
                          || (underflowInterruptEnable.Value && underflowInterrupt.Value); 
                Array.ForEach(channel, x => irq |= x.Interrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out IF);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
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

        private void RestartTimer(bool restartFromInitialValue = true)
        {
            if (!timerIsRunning)
            {
                return;
            }

            if (timerMode.Value != TimerMode.Up && timerMode.Value != TimerMode.Down)
            {
                throw new Exception("Mode not supported");
            }

            uint currentValue = restartFromInitialValue ? (timerMode.Value == TimerMode.Up ? 0 : topValue) : TimerCounter;

            timer.Enabled = false;

            timer.Frequency = Frequency;
            timer.Direction = timerMode.Value == TimerMode.Up ? Direction.Ascending : Direction.Descending;
            timer.Limit = topValue;
            timer.Enabled = true;
            timer.Value = currentValue;
            
            for(uint i = 0; i < NumberOfChannels; i++)
            {
                channel[i].TimerRestartedCallback((uint)timer.Value, (uint)timer.Limit, timer.Direction);
            }        
        }

        private void TimerLimitReached()
        {
            bool topValueReached = false;

            if (timer.Value == ((timerMode.Value == TimerMode.Up) ? 0 : topValue))
            {
                // Timer overflowed/underflowed
                topValueReached = true;
                
                if (timerMode.Value == TimerMode.Up)
                {
                    overflowInterrupt.Value = true;
                }
                else
                {
                    underflowInterrupt.Value = true;
                }
            }

            for(uint i = 0; i < NumberOfChannels; i++)
            {
                if (channel[i].mode.Value == ChannelMode.OutputCompare
                    && timer.Limit == channel[i].outputCompareValue)
                {
                    channel[i].captureCompareInterrupt.Value = true;
                }
            }

            UpdateInterrupts();
            
            if (oneShotMode.Value)
            {
                timerIsRunning = false;
            }
            else
            {
                RestartTimer(topValueReached);
            }
        }
#endregion

#region channel class
        private class Channel
        {
            public Channel(Machine machine, SiLabs_TIMER_2 parent, uint index)
            {
                this.parent = parent;
                this.index = index;
                this.timer = new LimitTimer(machine.ClockSource, 1000000, parent, $"timer-2-cc{index}", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                            enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
                this.timer.LimitReached += TimerLimitReached;
            }

            private SiLabs_TIMER_2 parent;
            private uint index;
            private LimitTimer timer;
            public uint outputCompareValue;
            public IEnumRegisterField<ChannelMode> mode;
            public IFlagRegisterField compareOutputInitialState;
            // Interrupts
            public IFlagRegisterField captureCompareInterrupt;
            public IFlagRegisterField captureCompareInterruptEnable;

            public bool Interrupt => (captureCompareInterrupt.Value && captureCompareInterruptEnable.Value);

            public void TimerRestartedCallback(uint value, uint limit, Direction direction)
            {
                if (mode.Value == ChannelMode.OutputCompare)
                {
                    if (outputCompareValue > limit)
                    {
                        throw new Exception("OC > TOP");
                    }

                    if (direction == Direction.Ascending
                        && outputCompareValue > value)
                    {
                        RestartTimer(outputCompareValue - value);
                    }
                    else if (direction == Direction.Descending
                             && outputCompareValue < value)
                    {
                        RestartTimer(value - outputCompareValue);
                    }
                }
            }

            private void RestartTimer(uint delay)
            {
                timer.Enabled = false;
                timer.Frequency = parent.Frequency;
                timer.Limit = delay;
                timer.Enabled = true;
            }

            private void TimerLimitReached()
            {
                timer.Enabled = false;
                captureCompareInterrupt.Value = true;
                parent.UpdateInterrupts();
            }
        }
#endregion

#region enums
        private enum TimerMode
        {
            Up     = 0,
            Down   = 1,
            UpDown = 2
        }

        private enum ClockSource
        {
            Prescaled               = 0,  // Prescaled EMO1GRPACLK
            CapureCompareChannel1   = 1,  // Compare/Capture Channel 1 Input
            TimerUndeflowOrOverflow = 2,  // Timer is clocked by underflow(down-count) or overflow(up- count) in the lower numbered neighbor Timer
        }

        private enum TimerAction
        {
            None        = 0,
            Start       = 1,
            Stop        = 2,
            ReloadStart = 3,
        }

        private enum ChannelMode
        {
            Off                  = 0,
            InputCapture         = 1,
            OutputCompare        = 2,
            PulseWidthModulation = 3,
        }

        private enum ChannelInputSelection
        {
            TimerCaptureComparePin = 0,
            SynchronousPrs         = 1,
            AsynchronousLevelPrs   = 2,
            AsynchronousPulsePrs   = 3,
        }

        private enum ChannelOutputAction
        {
            None   = 0,
            Toggle = 1,
            Clear  = 2,
            Set    = 3,
        }

        private enum Registers
        {
            IpVersion                                 = 0x0000,
            Config                                    = 0x0004,
            Control                                   = 0x0008,
            Command                                   = 0x000C,
            Status                                    = 0x0010,
            Status2                                   = 0x0014,
            InterruptFlags                            = 0x0018,
            InterruptEnable                           = 0x001C,
            Top                                       = 0x0020,
            TopBuffer                                 = 0x0024,
            Counter                                   = 0x0028,
            Lock                                      = 0x002C,
            Enable                                    = 0x0030,
            Channel0Config                            = 0x0060,
            Channel0Control                           = 0x0064,
            Channel0Phase                             = 0x0068,
            Channel0PhaseBuffer                       = 0x006C,
            Channel0OutputCompare                     = 0x0070,
            Channel0OutputCompareBuffer               = 0x0074,
            Channel0Dither                            = 0x0078,
            Channel0DitherBuffer                      = 0x007C,
            Channel0InputCapture                      = 0x0084,
            Channel0InputCaptureOverflow              = 0x0088,
            Channel1Config                            = 0x0090,
            Channel1Control                           = 0x0094,
            Channel1Phase                             = 0x0098,
            Channel1PhaseBuffer                       = 0x009C,
            Channel1OutputCompare                     = 0x00A0,
            Channel1OutputCompareBuffer               = 0x00A4,
            Channel1Dither                            = 0x00A8,
            Channel1DitherBuffer                      = 0x00AC,
            Channel1InputCapture                      = 0x00B4,
            Channel1InputCaptureOverflow              = 0x00B8,
            Channel2Config                            = 0x00C0,
            Channel2Control                           = 0x00C4,
            Channel2Phase                             = 0x00C8,
            Channel2PhaseBuffer                       = 0x00CC,
            Channel2OutputCompare                     = 0x00D0,
            Channel2OutputCompareBuffer               = 0x00D4,
            Channel2Dither                            = 0x00D8,
            Channel2DitherBuffer                      = 0x00DC,
            Channel2InputCapture                      = 0x00E4,
            Channel2InputCaptureOverflow              = 0x00E8,
            Channel3Config                            = 0x00F0,
            Channel3Control                           = 0x00F4,
            Channel3Phase                             = 0x00F8,
            Channel3PhaseBuffer                       = 0x00FC,
            Channel3OutputCompare                     = 0x0100,
            Channel3OutputCompareBuffer               = 0x0104,
            Channel3Dither                            = 0x0108,
            Channel3DitherBuffer                      = 0x010C,
            Channel3InputCapture                      = 0x0114,
            Channel3InputCaptureOverflow              = 0x0118,
            Channel4Config                            = 0x0120,
            Channel4Control                           = 0x0124,
            Channel4Phase                             = 0x0128,
            Channel4PhaseBuffer                       = 0x012C,
            Channel4OutputCompare                     = 0x0130,
            Channel4OutputCompareBuffer               = 0x0134,
            Channel4Dither                            = 0x0138,
            Channel4DitherBuffer                      = 0x013C,
            Channel4InputCapture                      = 0x0144,
            Channel4InputCaptureOverflow              = 0x0148,
            Channel5Config                            = 0x0150,
            Channel5Control                           = 0x0154,
            Channel5Phase                             = 0x0158,
            Channel5PhaseBuffer                       = 0x015C,
            Channel5OutputCompare                     = 0x0160,
            Channel5OutputCompareBuffer               = 0x0164,
            Channel5Dither                            = 0x0168,
            Channel5DitherBuffer                      = 0x016C,
            Channel5InputCapture                      = 0x0174,
            Channel5InputCaptureOverflow              = 0x0178,
            Channel6Config                            = 0x0180,
            Channel6Control                           = 0x0184,
            Channel6Phase                             = 0x0188,
            Channel6PhaseBuffer                       = 0x018C,
            Channel6OutputCompare                     = 0x0190,
            Channel6OutputCompareBuffer               = 0x0194,
            Channel6Dither                            = 0x0198,
            Channel6DitherBuffer                      = 0x019C,
            Channel6InputCapture                      = 0x01A4,
            Channel6InputCaptureOverflow              = 0x01A8,
            DtiConfig                                 = 0x01B0,
            DtiTimeConfig                             = 0x01B4,
            DtiFaultConfig                            = 0x01B8,
            DtiControl                                = 0x01BC,
            DtiOutputGenerationEnable                 = 0x01C0,
            DtiFault                                  = 0x01C4,
            DtiFaultClear                             = 0x01C8,
            DtiConfigLock                             = 0x01CC,
            // Set registers
            IpVersion_Set                             = 0x1000,
            Config_Set                                = 0x1004,
            Control_Set                               = 0x1008,
            Command_Set                               = 0x100C,
            Status_Set                                = 0x1010,
            Status2_Set                               = 0x1014,
            InterruptFlags_Set                        = 0x1018,
            InterruptEnable_Set                       = 0x101C,
            Top_Set                                   = 0x1020,
            TopBuffer_Set                             = 0x1024,
            Counter_Set                               = 0x1028,
            Lock_Set                                  = 0x102C,
            Enable_Set                                = 0x1030,
            Channel0Config_Set                        = 0x1060,
            Channel0Control_Set                       = 0x1064,
            Channel0Phase_Set                         = 0x1068,
            Channel0PhaseBuffer_Set                   = 0x106C,
            Channel0OutputCompare_Set                 = 0x1070,
            Channel0OutputCompareBuffer_Set           = 0x1074,
            Channel0Dither_Set                        = 0x1078,
            Channel0DitherBuffer_Set                  = 0x107C,
            Channel0InputCapture_Set                  = 0x1084,
            Channel0InputCaptureOverflow_Set          = 0x1088,
            Channel1Config_Set                        = 0x1090,
            Channel1Control_Set                       = 0x1094,
            Channel1Phase_Set                         = 0x1098,
            Channel1PhaseBuffer_Set                   = 0x109C,
            Channel1OutputCompare_Set                 = 0x10A0,
            Channel1OutputCompareBuffer_Set           = 0x10A4,
            Channel1Dither_Set                        = 0x10A8,
            Channel1DitherBuffer_Set                  = 0x10AC,
            Channel1InputCapture_Set                  = 0x10B4,
            Channel1InputCaptureOverflow_Set          = 0x10B8,
            Channel2Config_Set                        = 0x10C0,
            Channel2Control_Set                       = 0x10C4,
            Channel2Phase_Set                         = 0x10C8,
            Channel2PhaseBuffer_Set                   = 0x10CC,
            Channel2OutputCompare_Set                 = 0x10D0,
            Channel2OutputCompareBuffer_Set           = 0x10D4,
            Channel2Dither_Set                        = 0x10D8,
            Channel2DitherBuffer_Set                  = 0x10DC,
            Channel2InputCapture_Set                  = 0x10E4,
            Channel2InputCaptureOverflow_Set          = 0x10E8,
            Channel3Config_Set                        = 0x10F0,
            Channel3Control_Set                       = 0x10F4,
            Channel3Phase_Set                         = 0x10F8,
            Channel3PhaseBuffer_Set                   = 0x10FC,
            Channel3OutputCompare_Set                 = 0x1100,
            Channel3OutputCompareBuffer_Set           = 0x1104,
            Channel3Dither_Set                        = 0x1108,
            Channel3DitherBuffer_Set                  = 0x110C,
            Channel3InputCapture_Set                  = 0x1114,
            Channel3InputCaptureOverflow_Set          = 0x1118,
            Channel4Config_Set                        = 0x1120,
            Channel4Control_Set                       = 0x1124,
            Channel4Phase_Set                         = 0x1128,
            Channel4PhaseBuffer_Set                   = 0x112C,
            Channel4OutputCompare_Set                 = 0x1130,
            Channel4OutputCompareBuffer_Set           = 0x1134,
            Channel4Dither_Set                        = 0x1138,
            Channel4DitherBuffer_Set                  = 0x113C,
            Channel4InputCapture_Set                  = 0x1144,
            Channel4InputCaptureOverflow_Set          = 0x1148,
            Channel5Config_Set                        = 0x1150,
            Channel5Control_Set                       = 0x1154,
            Channel5Phase_Set                         = 0x1158,
            Channel5PhaseBuffer_Set                   = 0x115C,
            Channel5OutputCompare_Set                 = 0x1160,
            Channel5OutputCompareBuffer_Set           = 0x1164,
            Channel5Dither_Set                        = 0x1168,
            Channel5DitherBuffer_Set                  = 0x116C,
            Channel5InputCapture_Set                  = 0x1174,
            Channel5InputCaptureOverflow_Set          = 0x1178,
            Channel6Config_Set                        = 0x1180,
            Channel6Control_Set                       = 0x1184,
            Channel6Phase_Set                         = 0x1188,
            Channel6PhaseBuffer_Set                   = 0x118C,
            Channel6OutputCompare_Set                 = 0x1190,
            Channel6OutputCompareBuffer_Set           = 0x1194,
            Channel6Dither_Set                        = 0x1198,
            Channel6DitherBuffer_Set                  = 0x119C,
            Channel6InputCapture_Set                  = 0x11A4,
            Channel6InputCaptureOverflow_Set          = 0x11A8,
            DtiConfig_Set                             = 0x11B0,
            DtiTimeConfig_Set                         = 0x11B4,
            DtiFaultConfig_Set                        = 0x11B8,
            DtiControl_Set                            = 0x11BC,
            DtiOutputGenerationEnable_Set             = 0x11C0,
            DtiFault_Set                              = 0x11C4,
            DtiFaultClear_Set                         = 0x11C8,
            DtiConfigLock_Set                         = 0x11CC,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            Config_Clr                                = 0x2004,
            Control_Clr                               = 0x2008,
            Command_Clr                               = 0x200C,
            Status_Clr                                = 0x2010,
            Status2_Clr                               = 0x2014,
            InterruptFlags_Clr                        = 0x2018,
            InterruptEnable_Clr                       = 0x201C,
            Top_Clr                                   = 0x2020,
            TopBuffer_Clr                             = 0x2024,
            Counter_Clr                               = 0x2028,
            Lock_Clr                                  = 0x202C,
            Enable_Clr                                = 0x2030,
            Channel0Config_Clr                        = 0x2060,
            Channel0Control_Clr                       = 0x2064,
            Channel0Phase_Clr                         = 0x2068,
            Channel0PhaseBuffer_Clr                   = 0x206C,
            Channel0OutputCompare_Clr                 = 0x2070,
            Channel0OutputCompareBuffer_Clr           = 0x2074,
            Channel0Dither_Clr                        = 0x2078,
            Channel0DitherBuffer_Clr                  = 0x207C,
            Channel0InputCapture_Clr                  = 0x2084,
            Channel0InputCaptureOverflow_Clr          = 0x2088,
            Channel1Config_Clr                        = 0x2090,
            Channel1Control_Clr                       = 0x2094,
            Channel1Phase_Clr                         = 0x2098,
            Channel1PhaseBuffer_Clr                   = 0x209C,
            Channel1OutputCompare_Clr                 = 0x20A0,
            Channel1OutputCompareBuffer_Clr           = 0x20A4,
            Channel1Dither_Clr                        = 0x20A8,
            Channel1DitherBuffer_Clr                  = 0x20AC,
            Channel1InputCapture_Clr                  = 0x20B4,
            Channel1InputCaptureOverflow_Clr          = 0x20B8,
            Channel2Config_Clr                        = 0x20C0,
            Channel2Control_Clr                       = 0x20C4,
            Channel2Phase_Clr                         = 0x20C8,
            Channel2PhaseBuffer_Clr                   = 0x20CC,
            Channel2OutputCompare_Clr                 = 0x20D0,
            Channel2OutputCompareBuffer_Clr           = 0x20D4,
            Channel2Dither_Clr                        = 0x20D8,
            Channel2DitherBuffer_Clr                  = 0x20DC,
            Channel2InputCapture_Clr                  = 0x20E4,
            Channel2InputCaptureOverflow_Clr          = 0x20E8,
            Channel3Config_Clr                        = 0x20F0,
            Channel3Control_Clr                       = 0x20F4,
            Channel3Phase_Clr                         = 0x20F8,
            Channel3PhaseBuffer_Clr                   = 0x20FC,
            Channel3OutputCompare_Clr                 = 0x2100,
            Channel3OutputCompareBuffer_Clr           = 0x2104,
            Channel3Dither_Clr                        = 0x2108,
            Channel3DitherBuffer_Clr                  = 0x210C,
            Channel3InputCapture_Clr                  = 0x2114,
            Channel3InputCaptureOverflow_Clr          = 0x2118,
            Channel4Config_Clr                        = 0x2120,
            Channel4Control_Clr                       = 0x2124,
            Channel4Phase_Clr                         = 0x2128,
            Channel4PhaseBuffer_Clr                   = 0x212C,
            Channel4OutputCompare_Clr                 = 0x2130,
            Channel4OutputCompareBuffer_Clr           = 0x2134,
            Channel4Dither_Clr                        = 0x2138,
            Channel4DitherBuffer_Clr                  = 0x213C,
            Channel4InputCapture_Clr                  = 0x2144,
            Channel4InputCaptureOverflow_Clr          = 0x2148,
            Channel5Config_Clr                        = 0x2150,
            Channel5Control_Clr                       = 0x2154,
            Channel5Phase_Clr                         = 0x2158,
            Channel5PhaseBuffer_Clr                   = 0x215C,
            Channel5OutputCompare_Clr                 = 0x2160,
            Channel5OutputCompareBuffer_Clr           = 0x2164,
            Channel5Dither_Clr                        = 0x2168,
            Channel5DitherBuffer_Clr                  = 0x216C,
            Channel5InputCapture_Clr                  = 0x2174,
            Channel5InputCaptureOverflow_Clr          = 0x2178,
            Channel6Config_Clr                        = 0x2180,
            Channel6Control_Clr                       = 0x2184,
            Channel6Phase_Clr                         = 0x2188,
            Channel6PhaseBuffer_Clr                   = 0x218C,
            Channel6OutputCompare_Clr                 = 0x2190,
            Channel6OutputCompareBuffer_Clr           = 0x2194,
            Channel6Dither_Clr                        = 0x2198,
            Channel6DitherBuffer_Clr                  = 0x219C,
            Channel6InputCapture_Clr                  = 0x21A4,
            Channel6InputCaptureOverflow_Clr          = 0x21A8,
            DtiConfig_Clr                             = 0x21B0,
            DtiTimeConfig_Clr                         = 0x21B4,
            DtiFaultConfig_Clr                        = 0x21B8,
            DtiControl_Clr                            = 0x21BC,
            DtiOutputGenerationEnable_Clr             = 0x21C0,
            DtiFault_Clr                              = 0x21C4,
            DtiFaultClear_Clr                         = 0x21C8,
            DtiConfigLock_Clr                         = 0x21CC,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            Config_Tgl                                = 0x3004,
            Control_Tgl                               = 0x3008,
            Command_Tgl                               = 0x300C,
            Status_Tgl                                = 0x3010,
            Status2_Tgl                               = 0x3014,
            InterruptFlags_Tgl                        = 0x3018,
            InterruptEnable_Tgl                       = 0x301C,
            Top_Tgl                                   = 0x3020,
            TopBuffer_Tgl                             = 0x3024,
            Counter_Tgl                               = 0x3028,
            Lock_Tgl                                  = 0x302C,
            Enable_Tgl                                = 0x3030,
            Channel0Config_Tgl                        = 0x3060,
            Channel0Control_Tgl                       = 0x3064,
            Channel0Phase_Tgl                         = 0x3068,
            Channel0PhaseBuffer_Tgl                   = 0x306C,
            Channel0OutputCompare_Tgl                 = 0x3070,
            Channel0OutputCompareBuffer_Tgl           = 0x3074,
            Channel0Dither_Tgl                        = 0x3078,
            Channel0DitherBuffer_Tgl                  = 0x307C,
            Channel0InputCapture_Tgl                  = 0x3084,
            Channel0InputCaptureOverflow_Tgl          = 0x3088,
            Channel1Config_Tgl                        = 0x3090,
            Channel1Control_Tgl                       = 0x3094,
            Channel1Phase_Tgl                         = 0x3098,
            Channel1PhaseBuffer_Tgl                   = 0x309C,
            Channel1OutputCompare_Tgl                 = 0x30A0,
            Channel1OutputCompareBuffer_Tgl           = 0x30A4,
            Channel1Dither_Tgl                        = 0x30A8,
            Channel1DitherBuffer_Tgl                  = 0x30AC,
            Channel1InputCapture_Tgl                  = 0x30B4,
            Channel1InputCaptureOverflow_Tgl          = 0x30B8,
            Channel2Config_Tgl                        = 0x30C0,
            Channel2Control_Tgl                       = 0x30C4,
            Channel2Phase_Tgl                         = 0x30C8,
            Channel2PhaseBuffer_Tgl                   = 0x30CC,
            Channel2OutputCompare_Tgl                 = 0x30D0,
            Channel2OutputCompareBuffer_Tgl           = 0x30D4,
            Channel2Dither_Tgl                        = 0x30D8,
            Channel2DitherBuffer_Tgl                  = 0x30DC,
            Channel2InputCapture_Tgl                  = 0x30E4,
            Channel2InputCaptureOverflow_Tgl          = 0x30E8,
            Channel3Config_Tgl                        = 0x30F0,
            Channel3Control_Tgl                       = 0x30F4,
            Channel3Phase_Tgl                         = 0x30F8,
            Channel3PhaseBuffer_Tgl                   = 0x30FC,
            Channel3OutputCompare_Tgl                 = 0x3100,
            Channel3OutputCompareBuffer_Tgl           = 0x3104,
            Channel3Dither_Tgl                        = 0x3108,
            Channel3DitherBuffer_Tgl                  = 0x310C,
            Channel3InputCapture_Tgl                  = 0x3114,
            Channel3InputCaptureOverflow_Tgl          = 0x3118,
            Channel4Config_Tgl                        = 0x3120,
            Channel4Control_Tgl                       = 0x3124,
            Channel4Phase_Tgl                         = 0x3128,
            Channel4PhaseBuffer_Tgl                   = 0x312C,
            Channel4OutputCompare_Tgl                 = 0x3130,
            Channel4OutputCompareBuffer_Tgl           = 0x3134,
            Channel4Dither_Tgl                        = 0x3138,
            Channel4DitherBuffer_Tgl                  = 0x313C,
            Channel4InputCapture_Tgl                  = 0x3144,
            Channel4InputCaptureOverflow_Tgl          = 0x3148,
            Channel5Config_Tgl                        = 0x3150,
            Channel5Control_Tgl                       = 0x3154,
            Channel5Phase_Tgl                         = 0x3158,
            Channel5PhaseBuffer_Tgl                   = 0x315C,
            Channel5OutputCompare_Tgl                 = 0x3160,
            Channel5OutputCompareBuffer_Tgl           = 0x3164,
            Channel5Dither_Tgl                        = 0x3168,
            Channel5DitherBuffer_Tgl                  = 0x316C,
            Channel5InputCapture_Tgl                  = 0x3174,
            Channel5InputCaptureOverflow_Tgl          = 0x3178,
            Channel6Config_Tgl                        = 0x3180,
            Channel6Control_Tgl                       = 0x3184,
            Channel6Phase_Tgl                         = 0x3188,
            Channel6PhaseBuffer_Tgl                   = 0x318C,
            Channel6OutputCompare_Tgl                 = 0x3190,
            Channel6OutputCompareBuffer_Tgl           = 0x3194,
            Channel6Dither_Tgl                        = 0x3198,
            Channel6DitherBuffer_Tgl                  = 0x319C,
            Channel6InputCapture_Tgl                  = 0x31A4,
            Channel6InputCaptureOverflow_Tgl          = 0x31A8,
            DtiConfig_Tgl                             = 0x31B0,
            DtiTimeConfig_Tgl                         = 0x31B4,
            DtiFaultConfig_Tgl                        = 0x31B8,
            DtiControl_Tgl                            = 0x31BC,
            DtiOutputGenerationEnable_Tgl             = 0x31C0,
            DtiFault_Tgl                              = 0x31C4,
            DtiFaultClear_Tgl                         = 0x31C8,
            DtiConfigLock_Tgl                         = 0x31CC,
        }
#endregion
    }
}