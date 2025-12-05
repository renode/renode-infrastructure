//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SiLabs_TIMER_0 : SiLabsPeripheral
    {
        public SiLabs_TIMER_0(Machine machine, uint frequency) : base(machine, false)
        {
            this.timerFrequency = frequency;

            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "timer", 0xFFFF, direction: Direction.Ascending,
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

        public override void Reset()
        {
            base.Reset();

            timerIsRunning = false;
            timer.Enabled = false;
            topValue = TopValueInitValue;

            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channel[i].Reset();
            }
        }

        public GPIO IRQ { get; }

        public uint TimerCounter
        {
            get
            {
                if(timerIsRunning)
                {
                    if(timer.Enabled)
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

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = (overflowInterruptEnable.Value && overflowInterrupt.Value)
                          || (underflowInterruptEnable.Value && underflowInterrupt.Value);
                Array.ForEach(channel, x => irq |= x.Interrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out interruptFlag);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Noisy, "{0}: IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                IRQ.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
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
                    .WithReservedBits(13, 3)
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
                  .WithReservedBits(2, 30)
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
                    .WithReservedBits(11, 5)
                    .WithTaggedFlag("ICFEMPTY0", 16)
                    .WithTaggedFlag("ICFEMPTY1", 17)
                    .WithTaggedFlag("ICFEMPTY2", 18)
                    .WithReservedBits(19, 5)
                    .WithTaggedFlag("CCPOL0", 24)
                    .WithTaggedFlag("CCPOL1", 25)
                    .WithTaggedFlag("CCPOL2", 26)
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterrupt, name: "OFIF")
                    .WithFlag(1, out underflowInterrupt, name: "UFIF")
                    .WithTaggedFlag("DIRCHGIF", 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].CaptureCompareInterrupt, name: "CCIF0")
                    .WithFlag(5, out channel[1].CaptureCompareInterrupt, name: "CCIF1")
                    .WithFlag(6, out channel[2].CaptureCompareInterrupt, name: "CCIF2")
                    .WithReservedBits(7, 9)
                    .WithTaggedFlag("ICFWLFULLIF0", 16)
                    .WithTaggedFlag("ICFWLFULLIF1", 17)
                    .WithTaggedFlag("ICFWLFULLIF2", 18)
                    .WithReservedBits(19, 1)
                    .WithTaggedFlag("ICFOFIF0", 20)
                    .WithTaggedFlag("ICFOFIF1", 21)
                    .WithTaggedFlag("ICFOFIF2", 22)
                    .WithReservedBits(23, 1)
                    .WithTaggedFlag("ICFUFIF0", 24)
                    .WithTaggedFlag("ICFUFIF1", 25)
                    .WithTaggedFlag("ICFUFIF2", 26)
                    .WithReservedBits(27, 5)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterruptEnable, name: "OFIEN")
                    .WithFlag(1, out underflowInterruptEnable, name: "UFIEN")
                    .WithTaggedFlag("DIRCHGIEN", 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].CaptureCompareInterruptEnable, name: "CCIEN0")
                    .WithFlag(5, out channel[1].CaptureCompareInterruptEnable, name: "CCIEN1")
                    .WithFlag(6, out channel[2].CaptureCompareInterruptEnable, name: "CCIEN2")
                    .WithReservedBits(7, 9)
                    .WithTaggedFlag("ICFWLFULLIEN0", 16)
                    .WithTaggedFlag("ICFWLFULLIEN1", 17)
                    .WithTaggedFlag("ICFWLFULLIEN2", 18)
                    .WithReservedBits(19, 1)
                    .WithTaggedFlag("ICFOFIEN0", 20)
                    .WithTaggedFlag("ICFOFIEN1", 21)
                    .WithTaggedFlag("ICFOFIEN2", 22)
                    .WithReservedBits(23, 1)
                    .WithTaggedFlag("ICFUFIEN0", 24)
                    .WithTaggedFlag("ICFUFIEN1", 25)
                    .WithTaggedFlag("ICFUFIEN2", 26)
                    .WithReservedBits(27, 5)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                    .WithReservedBits(16, 16)
                },
            };

            var startOffset = (long)Registers.Channel0Config;
            var configOffset = (long)Registers.Channel0Config - startOffset;
            var controlOffset = (long)Registers.Channel0Control - startOffset;
            var outputCompareOffset = (long)Registers.Channel0OutputCompare - startOffset;
            var outputCompareBufferOffset = (long)Registers.Channel0OutputCompareBuffer - startOffset;
            var inputCaptureFifoOffset = (long)Registers.Channel0InputCapture - startOffset;
            var inputCaptureOverflowOffset = (long)Registers.Channel0InputCaptureOverflow - startOffset;
            var blockSize = (long)Registers.Channel1Config - (long)Registers.Channel0Config;
            for(var index = 0; index < NumberOfChannels; index++)
            {
                var i = index;
                // Channel_n_Config
                registerDictionary.Add(startOffset + blockSize * i + configOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, ChannelMode>(0, 2, out channel[i].Mode, name: "MODE")
                        .WithReservedBits(2, 2)
                        .WithFlag(4, out channel[i].CompareOutputInitialState, name: "COIST")
                        .WithReservedBits(5, 12)
                        .WithTag("INSEL", 17, 2)
                        .WithTaggedFlag("PRSCONF", 19)
                        .WithTaggedFlag("FILT", 20)
                        .WithTaggedFlag("ICFWL", 21)
                        .WithReservedBits(22, 10)
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_Control
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
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
                // Channel_n_OutputCompare
                registerDictionary.Add(startOffset + blockSize * i + outputCompareOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => channel[i].OutputCompareValue, writeCallback: (_, value) => channel[i].OutputCompareValue = (uint)value, name: "OC")
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_OutputCompareBuffer
                registerDictionary.Add(startOffset + blockSize * i + outputCompareBufferOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => channel[i].OutputCompareValue, writeCallback: (_, value) => channel[i].OutputCompareValue = (uint)value, name: "OCB")
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_InputCaptureFifo
                registerDictionary.Add(startOffset + blockSize * i + inputCaptureFifoOffset,
                    new DoubleWordRegister(this)
                        .WithTag("ICF", 0, 32)
                );
                // Channel_n_InputCaptureOverflow
                registerDictionary.Add(startOffset + blockSize * i + inputCaptureOverflowOffset,
                    new DoubleWordRegister(this)
                        .WithTag("ICOF", 0, 32)
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private void StopCommand()
        {
            timerIsRunning = false;
            timer.Enabled = false;
        }

        private void StartCommand()
        {
            timerIsRunning = true;
            RestartTimer(true);
        }

        private void RestartTimer(bool restartFromInitialValue = true)
        {
            if(!timerIsRunning)
            {
                return;
            }

            if(timerMode.Value != TimerMode.Up && timerMode.Value != TimerMode.Down)
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

            if(timer.Value == ((timerMode.Value == TimerMode.Up) ? 0 : topValue))
            {
                // Timer overflowed/underflowed
                topValueReached = true;

                if(timerMode.Value == TimerMode.Up)
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
                if(channel[i].Mode.Value == ChannelMode.OutputCompare
                    && timer.Limit == channel[i].OutputCompareValue)
                {
                    channel[i].CaptureCompareInterrupt.Value = true;
                }
            }

            UpdateInterrupts();

            if(oneShotMode.Value)
            {
                timerIsRunning = false;
            }
            else
            {
                RestartTimer(topValueReached);
            }
        }

        private IFlagRegisterField overflowInterrupt;
        private IFlagRegisterField underflowInterruptEnable;
        private IValueRegisterField prescaler;
        private IEnumRegisterField<ClockSource> clockSource;
        private IFlagRegisterField oneShotMode;
        private IEnumRegisterField<TimerMode> timerMode;
        private uint topValue = TopValueInitValue;
        private bool timerIsRunning = false;
        private IFlagRegisterField underflowInterrupt;
        private IFlagRegisterField overflowInterruptEnable;
        private readonly LimitTimer timer;
        private readonly uint timerFrequency;
        private readonly Channel[] channel;
        private const uint NumberOfChannels = 3;
        private const uint TopValueInitValue = 0xFFFF;

        private class Channel
        {
            public Channel(Machine machine, SiLabs_TIMER_0 parent, uint index)
            {
                this.parent = parent;
                this.index = index;
                this.timer = new LimitTimer(machine.ClockSource, 1000000, parent, $"timer-2-cc{index}", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                            enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
                this.timer.LimitReached += TimerLimitReached;
            }

            public void TimerRestartedCallback(uint value, uint limit, Direction direction)
            {
                if(Mode.Value == ChannelMode.OutputCompare)
                {
                    if(OutputCompareValue > limit)
                    {
                        throw new Exception("OC > TOP");
                    }

                    if(direction == Direction.Ascending
                        && OutputCompareValue > value)
                    {
                        RestartTimer(OutputCompareValue - value);
                    }
                    else if(direction == Direction.Descending
                             && OutputCompareValue < value)
                    {
                        RestartTimer(value - OutputCompareValue);
                    }
                }
            }

            public void Reset()
            {
                timer.Enabled = false;
            }

            public bool Interrupt => (CaptureCompareInterrupt.Value && CaptureCompareInterruptEnable.Value);

            public uint OutputCompareValue;
            public IEnumRegisterField<ChannelMode> Mode;
            public IFlagRegisterField CompareOutputInitialState;
            // Interrupts
            public IFlagRegisterField CaptureCompareInterrupt;
            public IFlagRegisterField CaptureCompareInterruptEnable;

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
                CaptureCompareInterrupt.Value = true;
                parent.UpdateInterrupts();
            }

            private readonly SiLabs_TIMER_0 parent;
            private readonly uint index;
            private readonly LimitTimer timer;
        }

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
            InterruptFlags                            = 0x0014,
            InterruptEnable                           = 0x0018,
            Top                                       = 0x001C,
            TopBuffer                                 = 0x0020,
            Counter                                   = 0x0024,
            Lock                                      = 0x002C,
            Enable                                    = 0x0030,
            Channel0Config                            = 0x0060,
            Channel0Control                           = 0x0064,
            Channel0OutputCompare                     = 0x0068,
            Channel0OutputCompareBuffer               = 0x0070,
            Channel0InputCapture                      = 0x0074,
            Channel0InputCaptureOverflow              = 0x0078,
            Channel1Config                            = 0x0080,
            Channel1Control                           = 0x0084,
            Channel1OutputCompare                     = 0x0088,
            Channel1OutputCompareBuffer               = 0x0090,
            Channel1InputCapture                      = 0x0094,
            Channel1InputCaptureOverflow              = 0x0098,
            Channel2Config                            = 0x00A0,
            Channel2Control                           = 0x00A4,
            Channel2OutputCompare                     = 0x00A8,
            Channel2OutputCompareBuffer               = 0x00B0,
            Channel2InputCapture                      = 0x00B4,
            Channel2InputCaptureOverflow              = 0x00B8,
            DtiConfig                                 = 0x00E0,
            DtiTimeConfig                             = 0x00E4,
            DtiFaultConfig                            = 0x00E8,
            DtiControl                                = 0x00EC,
            DtiOutputGenerationEnable                 = 0x00F0,
            DtiFault                                  = 0x00F4,
            DtiFaultClear                             = 0x00F8,
            DtiConfigLock                             = 0x00CF,
            // Set registers
            IpVersion_Set                             = 0x1000,
            Config_Set                                = 0x1004,
            Control_Set                               = 0x1008,
            Command_Set                               = 0x100C,
            Status_Set                                = 0x1010,
            InterruptFlags_Set                        = 0x1014,
            InterruptEnable_Set                       = 0x1018,
            Top_Set                                   = 0x101C,
            TopBuffer_Set                             = 0x1020,
            Counter_Set                               = 0x1024,
            Lock_Set                                  = 0x102C,
            Enable_Set                                = 0x1030,
            Channel0Config_Set                        = 0x1060,
            Channel0Control_Set                       = 0x1064,
            Channel0OutputCompare_Set                 = 0x1068,
            Channel0OutputCompareBuffer_Set           = 0x1070,
            Channel0InputCapture_Set                  = 0x1074,
            Channel0InputCaptureOverflow_Set          = 0x1078,
            Channel1Config_Set                        = 0x1080,
            Channel1Control_Set                       = 0x1084,
            Channel1OutputCompare_Set                 = 0x1088,
            Channel1OutputCompareBuffer_Set           = 0x1090,
            Channel1InputCapture_Set                  = 0x1094,
            Channel1InputCaptureOverflow_Set          = 0x1098,
            Channel2Config_Set                        = 0x10A0,
            Channel2Control_Set                       = 0x10A4,
            Channel2OutputCompare_Set                 = 0x10A8,
            Channel2OutputCompareBuffer_Set           = 0x10B0,
            Channel2InputCapture_Set                  = 0x10B4,
            Channel2InputCaptureOverflow_Set          = 0x10B8,
            DtiConfig_Set                             = 0x10E0,
            DtiTimeConfig_Set                         = 0x10E4,
            DtiFaultConfig_Set                        = 0x10E8,
            DtiControl_Set                            = 0x10EC,
            DtiOutputGenerationEnable_Set             = 0x10F0,
            DtiFault_Set                              = 0x10F4,
            DtiFaultClear_Set                         = 0x10F8,
            DtiConfigLock_Set                         = 0x10CF,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            Config_Clr                                = 0x2004,
            Control_Clr                               = 0x2008,
            Command_Clr                               = 0x200C,
            Status_Clr                                = 0x2010,
            InterruptFlags_Clr                        = 0x2014,
            InterruptEnable_Clr                       = 0x2018,
            Top_Clr                                   = 0x201C,
            TopBuffer_Clr                             = 0x2020,
            Counter_Clr                               = 0x2024,
            Lock_Clr                                  = 0x202C,
            Enable_Clr                                = 0x2030,
            Channel0Config_Clr                        = 0x2060,
            Channel0Control_Clr                       = 0x2064,
            Channel0OutputCompare_Clr                 = 0x2068,
            Channel0OutputCompareBuffer_Clr           = 0x2070,
            Channel0InputCapture_Clr                  = 0x2074,
            Channel0InputCaptureOverflow_Clr          = 0x2078,
            Channel1Config_Clr                        = 0x2080,
            Channel1Control_Clr                       = 0x2084,
            Channel1OutputCompare_Clr                 = 0x2088,
            Channel1OutputCompareBuffer_Clr           = 0x2090,
            Channel1InputCapture_Clr                  = 0x2094,
            Channel1InputCaptureOverflow_Clr          = 0x2098,
            Channel2Config_Clr                        = 0x20A0,
            Channel2Control_Clr                       = 0x20A4,
            Channel2OutputCompare_Clr                 = 0x20A8,
            Channel2OutputCompareBuffer_Clr           = 0x20B0,
            Channel2InputCapture_Clr                  = 0x20B4,
            Channel2InputCaptureOverflow_Clr          = 0x20B8,
            DtiConfig_Clr                             = 0x20E0,
            DtiTimeConfig_Clr                         = 0x20E4,
            DtiFaultConfig_Clr                        = 0x20E8,
            DtiControl_Clr                            = 0x20EC,
            DtiOutputGenerationEnable_Clr             = 0x20F0,
            DtiFault_Clr                              = 0x20F4,
            DtiFaultClear_Clr                         = 0x20F8,
            DtiConfigLock_Clr                         = 0x20CF,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            Config_Tgl                                = 0x3004,
            Control_Tgl                               = 0x3008,
            Command_Tgl                               = 0x300C,
            Status_Tgl                                = 0x3010,
            InterruptFlags_Tgl                        = 0x3014,
            InterruptEnable_Tgl                       = 0x3018,
            Top_Tgl                                   = 0x301C,
            TopBuffer_Tgl                             = 0x3020,
            Counter_Tgl                               = 0x3024,
            Lock_Tgl                                  = 0x302C,
            Enable_Tgl                                = 0x3030,
            Channel0Config_Tgl                        = 0x3060,
            Channel0Control_Tgl                       = 0x3064,
            Channel0OutputCompare_Tgl                 = 0x3068,
            Channel0OutputCompareBuffer_Tgl           = 0x3070,
            Channel0InputCapture_Tgl                  = 0x3074,
            Channel0InputCaptureOverflow_Tgl          = 0x3078,
            Channel1Config_Tgl                        = 0x3080,
            Channel1Control_Tgl                       = 0x3084,
            Channel1OutputCompare_Tgl                 = 0x3088,
            Channel1OutputCompareBuffer_Tgl           = 0x3090,
            Channel1InputCapture_Tgl                  = 0x3094,
            Channel1InputCaptureOverflow_Tgl          = 0x3098,
            Channel2Config_Tgl                        = 0x30A0,
            Channel2Control_Tgl                       = 0x30A4,
            Channel2OutputCompare_Tgl                 = 0x30A8,
            Channel2OutputCompareBuffer_Tgl           = 0x30B0,
            Channel2InputCapture_Tgl                  = 0x30B4,
            Channel2InputCaptureOverflow_Tgl          = 0x30B8,
            DtiConfig_Tgl                             = 0x30E0,
            DtiTimeConfig_Tgl                         = 0x30E4,
            DtiFaultConfig_Tgl                        = 0x30E8,
            DtiControl_Tgl                            = 0x30EC,
            DtiOutputGenerationEnable_Tgl             = 0x30F0,
            DtiFault_Tgl                              = 0x30F4,
            DtiFaultClear_Tgl                         = 0x30F8,
            DtiConfigLock_Tgl                         = 0x30CF,
        }
    }
}