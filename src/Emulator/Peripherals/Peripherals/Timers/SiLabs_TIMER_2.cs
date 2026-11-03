//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
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
    public class SiLabs_TIMER_2 : SiLabsPeripheral, ISiLabs_Retention
    {
        public SiLabs_TIMER_2(Machine machine) : base(machine, false)
        {
            timer = new LimitTimer(machine.ClockSource, 1000000, this, "timer_2", 0xFFFFFFFFUL, direction: Direction.Ascending,
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

        public void PowerDown()
        {
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            timerIsRunning = false;
            timer.Enabled = false;
            topValue = TopValueInitValue;
            topBufferValue = 0;
            topBufferValid = false;

            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channel[i].Reset();
            }
        }

        public override bool Enabled => enabled.Value;

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
                    frequency = (double)HfxoFrequency / (double)(prescaler.Value + 1);
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
                {(long)Registers.IpVersion, new DoubleWordRegister(this, 0x2)
                    .WithValueField(0, 32, name: "IPVERSION")
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, TimerMode>(0, 2, out timerMode, name: "MODE", changeCallback: (_, value) =>
                    {
                        if(value != TimerMode.Up && value != TimerMode.Down)
                        {
                            this.Log(LogLevel.Error, "MODE {0} is not supported, the timer will not run", value);
                        }
                    })
                    .WithReservedBits(2, 1)
                    .WithFlag(3, name: "SYNC")
                    .WithFlag(4, out oneShotMode, name: "OSMEN")
                    .WithFlag(5, name: "QDM")
                    .WithFlag(6, name: "DEBUGRUN")
                    .WithFlag(7, name: "DMACLRACT")
                    .WithEnumField<DoubleWordRegister, ClockSource>(8, 2, out clockSource, name: "CLKSEL", changeCallback: (_, value) =>
                    {
                        if(value != ClockSource.Prescaled)
                        {
                            this.Log(LogLevel.Error, "CLKSEL {0} is not supported, the timer will not run", value);
                        }
                    })
                    .WithFlag(10, name: "RETIMEEN")
                    .WithFlag(11, name: "DISSYNCOUT")
                    .WithFlag(12, name: "RETIMESEL")
                    .WithFlag(13, name: "UPDATEMODE")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, name: "ATI")
                    .WithFlag(17, name: "RSSCOIST")
                    .WithValueField(18, 10, out prescaler, name: "PRESC")
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "RISEA")
                    .WithValueField(2, 2, name: "FALLA")
                    .WithFlag(4, name: "X2CNT")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartCommand(); } }, name: "START")
                  .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StopCommand(); } }, name: "STOP")
                  .WithFlag(2, name: "UPDATECMD")
                  .WithReservedBits(3, 29)
                },
                {(long)Registers.Top, new DoubleWordRegister(this, TopValueInitValue)
                    .WithValueField(0, 32, valueProviderCallback: _ => topValue, writeCallback: (_, value) => topValue = (uint)value, name: "TOP")
                    .WithChangeCallback((_, __) => RestartTimer(false))
                },
                {(long)Registers.TopBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => topBufferValue,
                        writeCallback: (_, value) => { topBufferValue = (uint)value; topBufferValid = true; },
                        name: "TOPB")
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => (timer.Direction == Direction.Descending), name: "DIR")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => topBufferValid, name: "TOPBV")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "TIMERLOCKSTATUS")
                    .WithFlag(5, name: "DTILOCKSTATUS")
                    .WithFlag(6, name: "SYNCBUSY")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => channel[0].OutputCompareBufferValid, name: "OCBV0")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => channel[1].OutputCompareBufferValid, name: "OCBV1")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => channel[2].OutputCompareBufferValid, name: "OCBV2")
                    .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => channel[3].OutputCompareBufferValid, name: "OCBV3")
                    .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => channel[4].OutputCompareBufferValid, name: "OCBV4")
                    .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => channel[5].OutputCompareBufferValid, name: "OCBV5")
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => channel[6].OutputCompareBufferValid, name: "OCBV6")
                    .WithReservedBits(15, 1)
                    .WithFlag(16, name: "ICFEMPTY0")
                    .WithFlag(17, name: "ICFEMPTY1")
                    .WithFlag(18, name: "ICFEMPTY2")
                    .WithFlag(19, name: "ICFEMPTY3")
                    .WithFlag(20, name: "ICFEMPTY4")
                    .WithFlag(21, name: "ICFEMPTY5")
                    .WithFlag(22, name: "ICFEMPTY6")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, name: "CCPOL0")
                    .WithFlag(25, name: "CCPOL1")
                    .WithFlag(26, name: "CCPOL2")
                    .WithFlag(27, name: "CCPOL3")
                    .WithFlag(28, name: "CCPOL4")
                    .WithFlag(29, name: "CCPOL5")
                    .WithFlag(30, name: "CCPOL6")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.Status2, new DoubleWordRegister(this)
                    .WithFlag(0, name: "PHASEBV0")
                    .WithFlag(1, name: "PHASEBV1")
                    .WithFlag(2, name: "PHASEBV2")
                    .WithFlag(3, name: "PHASEBV3")
                    .WithFlag(4, name: "PHASEBV4")
                    .WithFlag(5, name: "PHASEBV5")
                    .WithFlag(6, name: "PHASEBV6")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, name: "DITHERBV0")
                    .WithFlag(9, name: "DITHERBV1")
                    .WithFlag(10, name: "DITHERBV2")
                    .WithFlag(11, name: "DITHERBV3")
                    .WithFlag(12, name: "DITHERBV4")
                    .WithFlag(13, name: "DITHERBV5")
                    .WithFlag(14, name: "DITHERBV6")
                    .WithReservedBits(15, 1)
                    .WithFlag(16, name: "UPDATEBUSY")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterrupt, name: "OFIF")
                    .WithFlag(1, out underflowInterrupt, name: "UFIF")
                    .WithFlag(2, name: "DIRCHGIF")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].CaptureCompareInterrupt, name: "CCIF0")
                    .WithFlag(5, out channel[1].CaptureCompareInterrupt, name: "CCIF1")
                    .WithFlag(6, out channel[2].CaptureCompareInterrupt, name: "CCIF2")
                    .WithFlag(7, out channel[3].CaptureCompareInterrupt, name: "CCIF3")
                    .WithFlag(8, out channel[4].CaptureCompareInterrupt, name: "CCIF4")
                    .WithFlag(9, out channel[5].CaptureCompareInterrupt, name: "CCIF5")
                    .WithFlag(10, out channel[6].CaptureCompareInterrupt, name: "CCIF6")
                    .WithFlag(11, name: "ICFWLFULLIF0")
                    .WithFlag(12, name: "ICFWLFULLIF1")
                    .WithFlag(13, name: "ICFWLFULLIF2")
                    .WithFlag(14, name: "ICFWLFULLIF3")
                    .WithFlag(15, name: "ICFWLFULLIF4")
                    .WithFlag(16, name: "ICFWLFULLIF5")
                    .WithFlag(17, name: "ICFWLFULLIF6")
                    .WithFlag(18, name: "ICFOFIF0")
                    .WithFlag(19, name: "ICFOFIF1")
                    .WithFlag(20, name: "ICFOFIF2")
                    .WithFlag(21, name: "ICFOFIF3")
                    .WithFlag(22, name: "ICFOFIF4")
                    .WithFlag(23, name: "ICFOFIF5")
                    .WithFlag(24, name: "ICFOFIF6")
                    .WithFlag(25, name: "ICFUFIF0")
                    .WithFlag(26, name: "ICFUFIF1")
                    .WithFlag(27, name: "ICFUFIF2")
                    .WithFlag(28, name: "ICFUFIF3")
                    .WithFlag(29, name: "ICFUFIF4")
                    .WithFlag(30, name: "ICFUFIF5")
                    .WithFlag(31, name: "ICFUFIF6")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterruptEnable, name: "OFIEN")
                    .WithFlag(1, out underflowInterruptEnable, name: "UFIEN")
                    .WithFlag(2, name: "DIRCHGIEN")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out channel[0].CaptureCompareInterruptEnable, name: "CCIEN0")
                    .WithFlag(5, out channel[1].CaptureCompareInterruptEnable, name: "CCIEN1")
                    .WithFlag(6, out channel[2].CaptureCompareInterruptEnable, name: "CCIEN2")
                    .WithFlag(7, out channel[3].CaptureCompareInterruptEnable, name: "CCIEN3")
                    .WithFlag(8, out channel[4].CaptureCompareInterruptEnable, name: "CCIEN4")
                    .WithFlag(9, out channel[5].CaptureCompareInterruptEnable, name: "CCIEN5")
                    .WithFlag(10, out channel[6].CaptureCompareInterruptEnable, name: "CCIEN6")
                    .WithFlag(11, name: "ICFWLFULLIEN0")
                    .WithFlag(12, name: "ICFWLFULLIEN1")
                    .WithFlag(13, name: "ICFWLFULLIEN2")
                    .WithFlag(14, name: "ICFWLFULLIEN3")
                    .WithFlag(15, name: "ICFWLFULLIEN4")
                    .WithFlag(16, name: "ICFWLFULLIEN5")
                    .WithFlag(17, name: "ICFWLFULLIEN6")
                    .WithFlag(18, name: "ICFOFIEN0")
                    .WithFlag(19, name: "ICFOFIEN1")
                    .WithFlag(20, name: "ICFOFIEN2")
                    .WithFlag(21, name: "ICFOFIEN3")
                    .WithFlag(22, name: "ICFOFIEN4")
                    .WithFlag(23, name: "ICFOFIEN5")
                    .WithFlag(24, name: "ICFOFIEN6")
                    .WithFlag(25, name: "ICFUFIEN0")
                    .WithFlag(26, name: "ICFUFIEN1")
                    .WithFlag(27, name: "ICFUFIEN2")
                    .WithFlag(28, name: "ICFUFIEN3")
                    .WithFlag(29, name: "ICFUFIEN4")
                    .WithFlag(30, name: "ICFUFIEN5")
                    .WithFlag(31, name: "ICFUFIEN6")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, name: "LOCKKEY")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enabled, name: "EN")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "DISABLING")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.DtiConfig, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DTEN")
                    .WithFlag(1, name: "DTDAS")
                    .WithReservedBits(2, 7)
                    .WithFlag(9, name: "DTAR")
                    .WithFlag(10, name: "DTFATS")
                    .WithFlag(11, name: "DTPRSEN")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.DtiTimeConfig, new DoubleWordRegister(this)
                    .WithValueField(0, 10, name: "DTPRESC")
                    .WithValueField(10, 6, name: "DTRISET")
                    .WithValueField(16, 6, name: "DTFALLT")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.DtiFaultConfig, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithValueField(16, 2, name: "DTFA")
                    .WithReservedBits(18, 6)
                    .WithFlag(24, name: "DTPRS0FEN")
                    .WithFlag(25, name: "DTPRS1FEN")
                    .WithFlag(26, name: "DTDBGFEN")
                    .WithFlag(27, name: "DTLOCKUPFEN")
                    .WithFlag(28, name: "DTEM23FEN")
                    .WithReservedBits(29, 3)
                },
                {(long)Registers.DtiControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DTCINV")
                    .WithFlag(1, name: "DTIPOL")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.DtiOutputGenerationEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DTOGCC0EN")
                    .WithFlag(1, name: "DTOGCC1EN")
                    .WithFlag(2, name: "DTOGCC2EN")
                    .WithFlag(3, name: "DTOGCDTI0EN")
                    .WithFlag(4, name: "DTOGCDTI1EN")
                    .WithFlag(5, name: "DTOGCDTI2EN")
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.DtiFault, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DTPRS0F")
                    .WithFlag(1, name: "DTPRS1F")
                    .WithFlag(2, name: "DTDBGF")
                    .WithFlag(3, name: "DTLOCKUPF")
                    .WithFlag(4, name: "DTEM23F")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.DtiFaultClear, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DTPRS0FC")
                    .WithFlag(1, name: "DTPRS1FC")
                    .WithFlag(2, name: "DTDBGFC")
                    .WithFlag(3, name: "DTLOCKUPFC")
                    .WithFlag(4, name: "DTEM23FC")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.DtiConfigLock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, name: "DTILOCKKEY")
                    .WithReservedBits(16, 16)
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
                registerDictionary.Add(startOffset + blockSize * i + configOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, ChannelMode>(0, 2, out channel[i].Mode, name: "MODE")
                        .WithReservedBits(2, 2)
                        .WithFlag(4, out channel[i].CompareOutputInitialState, name: "COIST")
                        .WithReservedBits(5, 12)
                        .WithValueField(17, 2, name: "INSEL")
                        .WithFlag(19, name: "PRSCONF")
                        .WithFlag(20, name: "FILT")
                        .WithFlag(21, name: "ICFWL")
                        .WithReservedBits(22, 10)
                        .WithChangeCallback((_, __) => RestartTimer(false))
                );
                // Channel_n_Control
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithReservedBits(0, 2)
                        .WithFlag(2, name: "OUTINV")
                        .WithReservedBits(3, 5)
                        .WithValueField(8, 2, name: "CMOA")
                        .WithValueField(10, 2, name: "COFOA")
                        .WithValueField(12, 2, name: "CUFOA")
                        .WithReservedBits(14, 10)
                        .WithValueField(24, 2, name: "ICEDGE")
                        .WithValueField(26, 2, name: "ICEVCTRL")
                        .WithReservedBits(28, 4)
                );
                // Channel_n_Phase
                registerDictionary.Add(startOffset + blockSize * i + phaseOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: "PHASE")
                );
                // Channel_n_PhaseBuffer
                registerDictionary.Add(startOffset + blockSize * i + phaseBufferOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: "PHASEB")
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
                        .WithValueField(0, 32, valueProviderCallback: _ => channel[i].OutputCompareBufferValue,
                            writeCallback: (_, value) => { channel[i].OutputCompareBufferValue = (uint)value; channel[i].OutputCompareBufferValid = true; },
                            name: "OCB")
                );
                // Channel_n_Dither
                registerDictionary.Add(startOffset + blockSize * i + ditherOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 4, name: "DITHER")
                        .WithReservedBits(4, 28)
                );
                // Channel_n_DitherBuffer
                registerDictionary.Add(startOffset + blockSize * i + ditherBufferOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 4, name: "DITHERB")
                        .WithReservedBits(4, 28)
                );
                // Channel_n_InputCaptureFifo
                registerDictionary.Add(startOffset + blockSize * i + inputCaptureFifoOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: "ICF")
                );
                // Channel_n_InputCaptureOverflow
                registerDictionary.Add(startOffset + blockSize * i + inputCaptureOverflowOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: "ICOF")
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
                this.Log(LogLevel.Error, "Cannot start the timer, MODE {0} is not supported", timerMode.Value);
                return;
            }

            if(clockSource.Value != ClockSource.Prescaled)
            {
                this.Log(LogLevel.Error, "Cannot start the timer, CLKSEL {0} is not supported", clockSource.Value);
                return;
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

                if(topBufferValid)
                {
                    topValue = topBufferValue;
                    topBufferValid = false;
                }

                for(uint i = 0; i < NumberOfChannels; i++)
                {
                    channel[i].ApplyBufferedValues();
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

        // Interrupt fields
        private IFlagRegisterField overflowInterrupt;
        private IFlagRegisterField underflowInterruptEnable;
        private IValueRegisterField prescaler;
        private IEnumRegisterField<ClockSource> clockSource;
        private IFlagRegisterField oneShotMode;
        private IEnumRegisterField<TimerMode> timerMode;
        private IFlagRegisterField enabled;
        private uint topValue = TopValueInitValue;
        private uint topBufferValue;
        private bool topBufferValid;
        private bool timerIsRunning = false;
        private IFlagRegisterField underflowInterrupt;
        private IFlagRegisterField overflowInterruptEnable;
        private readonly LimitTimer timer;
        private readonly Channel[] channel;
        private const uint NumberOfChannels = 7;
        private const uint TopValueInitValue = 0xFFFF;

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

            public void TimerRestartedCallback(uint value, uint limit, Direction direction)
            {
                if(Mode.Value == ChannelMode.OutputCompare)
                {
                    if(OutputCompareValue > limit)
                    {
                        parent.Log(LogLevel.Error, "CC{0}: OC 0x{1:X} is greater than TOP 0x{2:X}, compare will not match", index, OutputCompareValue, limit);
                        return;
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
                OutputCompareBufferValue = 0;
                OutputCompareBufferValid = false;
            }

            public void ApplyBufferedValues()
            {
                if(OutputCompareBufferValid)
                {
                    OutputCompareValue = OutputCompareBufferValue;
                    OutputCompareBufferValid = false;
                }
            }

            public bool Interrupt => (CaptureCompareInterrupt.Value && CaptureCompareInterruptEnable.Value);

            public uint OutputCompareValue;
            public uint OutputCompareBufferValue;
            public bool OutputCompareBufferValid;
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

            private readonly SiLabs_TIMER_2 parent;
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
    }
}
