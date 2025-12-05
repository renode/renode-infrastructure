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
    public class SiLabs_RTCC_1 : SiLabsPeripheral
    {
        public SiLabs_RTCC_1(Machine machine, uint frequency) : base(machine, false)
        {
            this.timerFrequency = frequency;

            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i] = new Channel(i);
                CompareMatchChannel[i] = () => { };
            }

            timer = new LimitTimer(machine.ClockSource, timerFrequency, this, "rtcctimer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            IRQ = new GPIO();
            SeqIRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public override void Reset()
        {
            base.Reset();

            timerIsRunning = false;
            locked = false;
            lastStopCounterValue = 0;
            timer.Enabled = false;

            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i].Reset();
            }
        }

        public void CaptureChannel(int index)
        {
            this.Log(LogLevel.Debug, "Capturing RTCC Channel {0}", index);
            channels[index].InputCaptureValue.Value = TimerCounter;
            channels[index].InterruptFlag.Value = true;
            channels[index].SeqInterruptFlag.Value = true;
        }

        public GPIO IRQ { get; }

        public GPIO SeqIRQ { get; }

        public uint TimerPreCounter
        {
            get
            {
                this.Log(LogLevel.Error, "RTCC_1: TimerPreCounter value not supported");
                return 0;
            }

            set
            {
                this.Log(LogLevel.Error, "RTCC_1: TimerPreCounter value not supported");
            }
        }

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
                        return lastStopCounterValue;
                    }
                }
                else
                {
                    return lastStopCounterValue;
                }
            }

            set
            {
                if(timer.Enabled)
                {
                    timer.Value = value;
                }
                else
                {
                    lastStopCounterValue = value;
                }
                RestartTimer();
            }
        }

        public readonly Action[] CompareMatchChannel = new Action[NumberOfChannels];

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = ((overflowInterrupt.Value && overflowInterruptEnable.Value)
                           || (mainCounterTickInterrupt.Value && mainCounterTickInterruptEnable.Value)
                           || (channels[0].InterruptFlag.Value && channels[0].InterruptEnable.Value)
                           || (channels[1].InterruptFlag.Value && channels[1].InterruptEnable.Value)
                           || (channels[2].InterruptFlag.Value && channels[2].InterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out interruptFlag);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Noisy, "{0}: IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                IRQ.Set(irq);

                irq = ((seqOverflowInterrupt.Value && seqOverflowInterruptEnable.Value)
                        || (seqMainCounterTickInterrupt.Value && seqMainCounterTickInterruptEnable.Value)
                        || (channels[0].SeqInterruptFlag.Value && channels[0].SeqInterruptEnable.Value)
                        || (channels[1].SeqInterruptFlag.Value && channels[1].SeqInterruptEnable.Value)
                        || (channels[2].SeqInterruptFlag.Value && channels[2].SeqInterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out interruptFlag);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Noisy, "{0}: SEQ_IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqIRQ.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enable, name: "EN")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithTaggedFlag("DEBUGRUN", 0)
                    .WithFlag(1, out preCounterCc0TopValueEnable, name: "PRECNTCCV0TOP")
                    .WithFlag(2, out counterCc1TopValueEnable, name: "CNTCCV1TOP")
                    .WithEnumField<DoubleWordRegister, CounterPrescalerMode>(3, 1, out counterPrescalerMode, name: "CNTTICK")
                    .WithEnumField<DoubleWordRegister, CounterPrescalerValue>(4, 4, out counterPrescalerValue, name: "CNTPRESC")
                    .WithChangeCallback((_, __) => RestartTimer())
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartCommand(); } }, name: "START")
                  .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StopCommand(); } }, name: "STOP")
                  .WithReservedBits(2, 30)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => timerIsRunning, name: "RUNNING")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => locked, name: "RTCCLOCKSTATUS")
                    .WithReservedBits(2, 30)
                },
                { (long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterrupt, name: "OFIF")
                    .WithFlag(1, out mainCounterTickInterrupt, name: "CNTTICKIF")
                    .WithFlag(2, out seqOverflowInterrupt, name: "OFSEQIF")
                    .WithFlag(3, out seqMainCounterTickInterrupt, name: "CNTTICKSEQIF")
                    .WithFlag(4, out channels[0].InterruptFlag, name: "CCIF0")
                    .WithFlag(5, out channels[0].SeqInterruptFlag, name: "CCSEQIF0")
                    .WithFlag(6, out channels[1].InterruptFlag, name: "CCIF1")
                    .WithFlag(7, out channels[1].SeqInterruptFlag, name: "CCSEQIF1")
                    .WithFlag(8, out channels[2].InterruptFlag, name: "CCIF2")
                    .WithFlag(9, out channels[2].SeqInterruptFlag, name: "CCSEQIF2")
                    .WithReservedBits(10, 22)
                    .WithChangeCallback((_, __) => { UpdateInterrupts(); CheckMainCounterTickInterrupt(); })
                },
                { (long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out overflowInterruptEnable, name: "OFIEN")
                    .WithFlag(1, out mainCounterTickInterruptEnable, name: "CNTTICKIEN")
                    .WithFlag(2, out seqOverflowInterruptEnable, name: "OFSEQIEN")
                    .WithFlag(3, out seqMainCounterTickInterruptEnable, name: "CNTTICKSEQIEN")
                    .WithFlag(4, out channels[0].InterruptEnable, name: "CCIEN0")
                    .WithFlag(5, out channels[0].SeqInterruptEnable, name: "CCSEQIEN0")
                    .WithFlag(6, out channels[1].InterruptEnable, name: "CCIEN1")
                    .WithFlag(7, out channels[1].SeqInterruptEnable, name: "CCSEQIEN1")
                    .WithFlag(8, out channels[2].InterruptEnable, name: "CCIEN2")
                    .WithFlag(9, out channels[2].SeqInterruptEnable, name: "CCSEQIEN2")
                    .WithReservedBits(10, 22)
                    .WithChangeCallback((_, __) => { UpdateInterrupts(); CheckMainCounterTickInterrupt(); })
                },
                {(long)Registers.PreCounter, new DoubleWordRegister(this)
                    .WithValueField(0, 15, valueProviderCallback: _ =>  TimerPreCounter, writeCallback: (_, value) => TimerPreCounter = (uint)value, name: "PRECNT")
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  TimerCounter, writeCallback: (_, value) => TimerCounter = (uint)value, name: "CNT")
                },
                {(long)Registers.CombinedPreCounterAndCounter, new DoubleWordRegister(this)
                    .WithValueField(0, 15, FieldMode.Read, valueProviderCallback: _ =>  TimerPreCounter, name: "PRECNT")
                    .WithValueField(15, 17, FieldMode.Read, valueProviderCallback: _ =>  (TimerCounter & 0x1FFFF), name: "CNTLSB")
                },
                {(long)Registers.SyncBusy, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "START")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "STOP")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "PRECNT")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "CNT")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) => {locked = (value != UnlockValue); }, name: "UNLOCK")
                    .WithReservedBits(16, 16)
                },
            };

            var startOffset = (long)Registers.Channel0Control;
            var controlOffset = (long)Registers.Channel0Control - startOffset;
            var ouputCompareOffset = (long)Registers.Channel0OutputCompare - startOffset;
            var inputCaptureOffset = (long)Registers.Channel0InputCapture - startOffset;
            var blockSize = (long)Registers.Channel1Control - (long)Registers.Channel0Control;
            for(var index = 0; index < NumberOfChannels; index++)
            {
                var i = index;

                // CCx Control
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, CaptureCompareChannelMode>(0, 2, out channels[i].Mode, name: "CC_MODE")
                        .WithEnumField<DoubleWordRegister, CompareMatchOutputAction>(2, 2, out channels[i].OutputAction, name: "CC_CMOA")
                        .WithEnumField<DoubleWordRegister, CaptureCompareChannelComparisonBase>(4, 1, out channels[i].ComparisonBase, name: "CC_COMPBASE")
                        .WithTag("CC_IDEDGE", 5, 2)
                        .WithTaggedFlag("CC_EM1PWUEN", 7)
                        .WithReservedBits(8, 24)
                        .WithChangeCallback((_, __) => RestartTimer())
                );
                // CCx Output Compare Value
                registerDictionary.Add(startOffset + blockSize * i + ouputCompareOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out channels[i].OutputCompareValue, name: "CC_OC")
                        .WithChangeCallback((_, __) => RestartTimer())
                );
                // CCx Input Capture Value
                registerDictionary.Add(startOffset + blockSize * i + inputCaptureOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out channels[i].InputCaptureValue, FieldMode.Read, name: "CC_IC")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private void TimerLimitReached()
        {
            bool resetCounter = false;

            mainCounterTickInterrupt.Value = true;
            seqMainCounterTickInterrupt.Value = true;

            if(timer.Limit == 0xFFFFFFFF)
            {
                overflowInterrupt.Value = true;
                seqOverflowInterrupt.Value = true;
                resetCounter = true;
            }

            for(int i = 0; i < NumberOfChannels; i++)
            {
                if(channels[i].Mode.Value == CaptureCompareChannelMode.OutputCompare
                    && timer.Limit == channels[i].OutputCompareValue.Value)
                {
                    CompareMatchChannel[i]?.Invoke();
                    channels[i].InterruptFlag.Value = true;
                    channels[i].SeqInterruptFlag.Value = true;

                    if(i == 1 && counterCc1TopValueEnable.Value)
                    {
                        overflowInterrupt.Value = true;
                        seqOverflowInterrupt.Value = true;
                        resetCounter = true;
                    }
                }
            }

            lastStopCounterValue = (resetCounter) ? 0 : (uint)timer.Limit;
            UpdateInterrupts();
            RestartTimer(resetCounter);
        }

        private void CheckMainCounterTickInterrupt()
        {
            if(!timerIsRunning || !timer.Enabled)
            {
                return;
            }

            if((mainCounterTickInterruptEnable.Value && !mainCounterTickInterrupt.Value)
                || (seqMainCounterTickInterruptEnable.Value && !seqMainCounterTickInterrupt.Value))
            {
                RestartTimer();
            }
        }

        private void StartCommand()
        {
            timerIsRunning = true;
            RestartTimer();
        }

        private void StopCommand()
        {
            lastStopCounterValue = TimerCounter;
            timerIsRunning = false;
            timer.Enabled = false;
        }

        private void RestartTimer(bool restartFromZero = false)
        {
            if(!timerIsRunning)
            {
                return;
            }

            if(timer.Enabled)
            {
                lastStopCounterValue = (uint)timer.Value;
            }

            uint currentValue = restartFromZero ? 0 : lastStopCounterValue;
            uint limit = 0xFFFFFFFF;
            timer.Enabled = false;

            // For now we don't implement PRECNT, which always reads 0.
            // The timer value represent the CNT value.

            if(counterPrescalerMode.Value == CounterPrescalerMode.Channel0Match)
            {
                // TODO: this could be implemented even if we don't support PRECNT.
                // This is basically a custom non-integer divider.
                this.Log(LogLevel.Error, "Prescaler mode CCV0MATCH not supported");
                return;
            }

            for(int i = 0; i < NumberOfChannels; i++)
            {
                if(channels[i].Mode.Value != CaptureCompareChannelMode.Off)
                {
                    if(channels[i].ComparisonBase.Value == CaptureCompareChannelComparisonBase.PreCounter)
                    {
                        this.Log(LogLevel.Error, "Channel CC comparison PRECNT not supported");
                        return;
                    }
                    if(channels[i].Mode.Value == CaptureCompareChannelMode.OutputCompare
                        && channels[i].OutputCompareValue.Value > currentValue
                        && channels[i].OutputCompareValue.Value < limit)
                    {
                        limit = (uint)channels[i].OutputCompareValue.Value;
                    }

                    // TODO: implement Capture mode
                }
            }

            if((mainCounterTickInterruptEnable.Value && !mainCounterTickInterrupt.Value)
                || (seqMainCounterTickInterruptEnable.Value && !seqMainCounterTickInterrupt.Value))
            {
                limit = currentValue + 1;
            }

            timer.Frequency = timerFrequency;
            timer.Limit = limit;
            timer.Divider = (int)Math.Pow(2, (int)counterPrescalerValue.Value);
            timer.Value = currentValue;

            this.Log(LogLevel.Noisy, "Restarting timer at {0}: fromZero={1} startValue={2} divider={3} limit={4}",
                     GetTime(), restartFromZero, currentValue, timer.Divider, limit);

            timer.Enabled = true;
        }

        private IFlagRegisterField seqMainCounterTickInterruptEnable;
        private IFlagRegisterField seqOverflowInterruptEnable;
        private IFlagRegisterField mainCounterTickInterruptEnable;
        private IFlagRegisterField overflowInterruptEnable;
        private IFlagRegisterField seqMainCounterTickInterrupt;
        private IFlagRegisterField mainCounterTickInterrupt;
        // Interrupt fields
        private IFlagRegisterField overflowInterrupt;
        private IEnumRegisterField<CounterPrescalerValue>counterPrescalerValue;
        private IEnumRegisterField<CounterPrescalerMode>counterPrescalerMode;
        private IFlagRegisterField counterCc1TopValueEnable;
        private IFlagRegisterField preCounterCc0TopValueEnable;
        private IFlagRegisterField enable;
        private uint lastStopCounterValue = 0;
        private bool locked = false;
        private bool timerIsRunning = false;
        private IFlagRegisterField seqOverflowInterrupt;
        private readonly LimitTimer timer;
        private readonly uint timerFrequency;
        private readonly Channel[] channels = new Channel[NumberOfChannels];

        private const uint UnlockValue = 0xAEE8;
        private const uint NumberOfChannels = 3;

        private class Channel
        {
            public Channel(int index)
            {
                Index = index;
            }

            public void Reset()
            {
            }

            public int Index { get; }

            public IEnumRegisterField<CaptureCompareChannelMode> Mode;
            public IEnumRegisterField<CompareMatchOutputAction> OutputAction;
            public IEnumRegisterField<CaptureCompareChannelComparisonBase> ComparisonBase;
            public IValueRegisterField OutputCompareValue;
            public IValueRegisterField InputCaptureValue;
            public IFlagRegisterField InterruptFlag;
            public IFlagRegisterField InterruptEnable;
            public IFlagRegisterField SeqInterruptFlag;
            public IFlagRegisterField SeqInterruptEnable;
        }

        private enum CounterPrescalerValue
        {
            DivideBy1 = 0,
            DivideBy2 = 1,
            DivideBy4 = 2,
            DivideBy8 = 3,
            DivideBy16 = 4,
            DivideBy32 = 5,
            DivideBy64 = 6,
            DivideBy128 = 7,
            DivideBy256 = 8,
            DivideBy512 = 9,
            DivideBy1024 = 10,
            DivideBy2048 = 11,
            DivideBy4096 = 12,
            DivideBy8192 = 13,
            DivideBy16384 = 14,
            DivideBy32768 = 15
        }

        private enum CounterPrescalerMode
        {
            Prescaler = 0,
            Channel0Match = 1,
        }

        private enum CaptureCompareChannelMode
        {
            Off = 0,
            InputCapture = 1,
            OutputCompare = 2,
        }

        private enum CompareMatchOutputAction
        {
            Pulse = 0,
            Toggle = 1,
            Clear = 2,
            Set = 3,
        }

        private enum CaptureCompareChannelComparisonBase
        {
            Counter = 0,
            PreCounter = 1,
        }

        private enum Registers
        {
            IpVersion                                 = 0x0000,
            Enable                                    = 0x0004,
            Config                                    = 0x0008,
            Command                                   = 0x000C,
            Status                                    = 0x0010,
            InterruptFlags                            = 0x0014,
            InterruptEnable                           = 0x0018,
            PreCounter                                = 0x001C,
            Counter                                   = 0x0020,
            CombinedPreCounterAndCounter              = 0x0024,
            SyncBusy                                  = 0x0028,
            Lock                                      = 0x002C,
            Channel0Control                           = 0x0030,
            Channel0OutputCompare                     = 0x0034,
            Channel0InputCapture                      = 0x0038,
            Channel1Control                           = 0x003C,
            Channel1OutputCompare                     = 0x0040,
            Channel1InputCapture                      = 0x0044,
            Channel2Control                           = 0x0048,
            Channel2OutputCompare                     = 0x004C,
            Channel2InputCapture                      = 0x0050,
            // Set registers
            IpVersion_Set                             = 0x1000,
            Enable_Set                                = 0x1004,
            Config_Set                                = 0x1008,
            Command_Set                               = 0x100C,
            Status_Set                                = 0x1010,
            InterruptFlags_Set                        = 0x1014,
            InterruptEnable_Set                       = 0x1018,
            PreCounter_Set                            = 0x101C,
            Counter_Set                               = 0x1020,
            CombinedPreCounterAndCounter_Set          = 0x1024,
            SyncBusy_Set                              = 0x1028,
            Lock_Set                                  = 0x102C,
            Channel0Control_Set                       = 0x1030,
            Channel0OutputCompare_Set                 = 0x1034,
            Channel0InputCapture_Set                  = 0x1038,
            Channel1Control_Set                       = 0x103C,
            Channel1OutputCompare_Set                 = 0x1040,
            Channel1InputCapture_Set                  = 0x1044,
            Channel2Control_Set                       = 0x1048,
            Channel2OutputCompare_Set                 = 0x104C,
            Channel2InputCapture_Set                  = 0x1050,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            Enable_Clr                                = 0x2004,
            Config_Clr                                = 0x2008,
            Command_Clr                               = 0x200C,
            Status_Clr                                = 0x2010,
            InterruptFlags_Clr                        = 0x2014,
            InterruptEnable_Clr                       = 0x2018,
            PreCounter_Clr                            = 0x201C,
            Counter_Clr                               = 0x2020,
            CombinedPreCounterAndCounter_Clr          = 0x2024,
            SyncBusy_Clr                              = 0x2028,
            Lock_Clr                                  = 0x202C,
            Channel0Control_Clr                       = 0x2030,
            Channel0OutputCompare_Clr                 = 0x2034,
            Channel0InputCapture_Clr                  = 0x2038,
            Channel1Control_Clr                       = 0x203C,
            Channel1OutputCompare_Clr                 = 0x2040,
            Channel1InputCapture_Clr                  = 0x2044,
            Channel2Control_Clr                       = 0x2048,
            Channel2OutputCompare_Clr                 = 0x204C,
            Channel2InputCapture_Clr                  = 0x2050,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            Enable_Tgl                                = 0x3004,
            Config_Tgl                                = 0x3008,
            Command_Tgl                               = 0x300C,
            Status_Tgl                                = 0x3010,
            InterruptFlags_Tgl                        = 0x3014,
            InterruptEnable_Tgl                       = 0x3018,
            PreCounter_Tgl                            = 0x301C,
            Counter_Tgl                               = 0x3020,
            CombinedPreCounterAndCounter_Tgl          = 0x3024,
            SyncBusy_Tgl                              = 0x3028,
            Lock_Tgl                                  = 0x302C,
            Channel0Control_Tgl                       = 0x3030,
            Channel0OutputCompare_Tgl                 = 0x3034,
            Channel0InputCapture_Tgl                  = 0x3038,
            Channel1Control_Tgl                       = 0x303C,
            Channel1OutputCompare_Tgl                 = 0x3040,
            Channel1InputCapture_Tgl                  = 0x3044,
            Channel2Control_Tgl                       = 0x3048,
            Channel2OutputCompare_Tgl                 = 0x304C,
            Channel2InputCapture_Tgl                  = 0x3050,
        }
    }
}