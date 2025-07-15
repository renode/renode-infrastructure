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
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public abstract class SiLabs_SequencerAccelerator : IDoubleWordPeripheral, IKnownSize
    {
        protected SiLabs_SequencerAccelerator(Machine machine, long frequency, SiLabs_IProtocolTimer protocolTimer = null)
        {
            this.machine = machine;
            this.protocolTimer = protocolTimer;

            protocolTimer.PreCountOverflowsEvent += PreCountOverflowsEventCallback;
            protocolTimer.BaseCountOverflowsEvent += BaseCountOverflowsEventCallback;
            protocolTimer.WrapCountOverflowsEvent += WrapCountOverflowsEventCallback;
            protocolTimer.CaptureCompareEvent += CaptureCompareEventCallback;

            timer = new LimitTimer(machine.ClockSource, frequency, this, "seqacc_timer", 0xFFFFUL, direction: Direction.Ascending,
                                         enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            sequence = new Sequence[NumberOfSequenceConfigurations];
            for(var idx = 0; idx < NumberOfSequenceConfigurations; ++idx)
            {
                var i = idx;
                sequence[i] = new Sequence(this, (uint)i);
            }
            baseAddress = new IValueRegisterField[NumberOfBaseAddresses];


            HostIRQ = new GPIO();
            SequencerIRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        protected abstract uint ReadRegister(long offset, bool internal_read = false);
        protected abstract void WriteRegister(long offset, uint value, bool internal_write = false);        
        protected abstract DoubleWordRegisterCollection BuildRegistersCollection();
        protected abstract void UpdateInterrupts();

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        public long Size => 0x4000;
        public GPIO HostIRQ { get; }
        public GPIO SequencerIRQ { get; }
        protected readonly Machine machine;
        public readonly SiLabs_IProtocolTimer protocolTimer;
        protected readonly DoubleWordRegisterCollection registersCollection;
        protected const uint SetRegisterOffset = 0x1000;
        protected const uint ClearRegisterOffset = 0x2000;
        protected const uint ToggleRegisterOffset = 0x3000;
        public bool LogSequenceAcceleratorActivityAsWarning = false;
        protected const long MicrosecondFrequency = 1000000L;
        protected const uint NumberOfBaseAddresses = 16;
        protected const uint NumberOfSequenceConfigurations = 8;
        protected const uint NumberOfTriggerOutputSignals = 4;
        protected const uint NumberOfWaitStartSignals = 32;
        protected readonly LimitTimer timer;
        protected uint absoluteDelayCounter = 0;
        protected uint signalBitmask = 0;
        protected uint triggerBitmask = 0;
        TimerStartReason timerStartReason = TimerStartReason.Invalid;
        protected uint timerSequenceIndex = 0;
        protected Sequence[] sequence;
        
#region register fields
        protected IFlagRegisterField enable;
        public IValueRegisterField[] baseAddress;
        public IEnumRegisterField<TimeBase> timeBase;
        public IValueRegisterField baseAddressPosition;
        protected IValueRegisterField lastBusErrorAddress;
        public IValueRegisterField equalMaskCondition0;
        public IValueRegisterField equalMaskCondition1;
        protected IValueRegisterField spare0;
        // Interrupt fields
        protected IFlagRegisterField sequenceAbortedInterrupt;
        protected IFlagRegisterField sequenceErrorInterrupt;
        protected IFlagRegisterField busErrorInterrupt;
        protected IFlagRegisterField sequenceAbortedInterruptEnable;
        protected IFlagRegisterField sequenceErrorInterruptEnable;
        protected IFlagRegisterField busErrorInterruptEnable;
        protected IFlagRegisterField seqSequenceAbortedInterrupt;
        protected IFlagRegisterField seqSequenceErrorInterrupt;
        protected IFlagRegisterField seqBusErrorInterrupt;
        protected IFlagRegisterField seqSequenceAbortedInterruptEnable;
        protected IFlagRegisterField seqSequenceErrorInterruptEnable;
        protected IFlagRegisterField seqBusErrorInterruptEnable;
#endregion

#region methods
        protected TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        protected bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }
        
        public uint AbsoluteDelayCounter
        {
            get
            {
                if (timeBase.Value == TimeBase.Clock)
                {
                    throw new Exception("SEQACC: clock timebase not supported");
                }

                var ret = absoluteDelayCounter;
                
                if (timeBase.Value == TimeBase.PreCountOverflow)
                {
                    ret += protocolTimer.GetCurrentPreCountOverflows();
                }

                return ret;
            }
        }
        public uint SignalBitmask
        {
            set
            {
                signalBitmask = value;
            }
            get
            {
                return signalBitmask;
            }
        }

        public uint TriggerBitmask
        {
            set
            {
                triggerBitmask = value;
            }
            get
            {
                return triggerBitmask;
            }
        }

        public void PreCountOverflowsEventCallback(uint overflowCount)
        {
            if (enable.Value && timeBase.Value == TimeBase.PreCountOverflow)
            {
                absoluteDelayCounter += overflowCount;                
            }
        }

        public void BaseCountOverflowsEventCallback(uint overflowCount)
        {
            if (enable.Value && timeBase.Value == TimeBase.BaseCountOverflow)
            {
                absoluteDelayCounter += overflowCount;                
            }
        }

        public void WrapCountOverflowsEventCallback(uint overflowCount)
        {
            if (enable.Value && timeBase.Value == TimeBase.WrapCountOverflow)
            {
                absoluteDelayCounter += overflowCount;                
            }
        }

        public void CaptureCompareEventCallback(uint captureCompareIndex)
        {
            // CC9, CC10 and CC11 can raise a hardware event that can trigger a SEQACC sequence.
            if (captureCompareIndex >= 9 && captureCompareIndex <= 11)
            {
                HardwareStartSelect evt = (HardwareStartSelect)((uint)HardwareStartSelect.ProtimerCaptureCompare9 + (captureCompareIndex - 9));
                TriggerHardwareStartEvent(evt);
            }
        }

        public void StartTimer(uint sequenceIndex, TimerStartReason reason)
        {
            timerStartReason = reason;
            timerSequenceIndex = sequenceIndex;
            timer.Enabled = false;
            timer.Value = 0;

            switch(reason)
            {
                case TimerStartReason.DeferredStart:
                    // No point of trying to go faster than a single PRECNT overflow tick.
                    timer.Frequency = protocolTimer.GetPreCountOverflowFrequency();
                    timer.Limit = 1;  
                break;
                case TimerStartReason.Delay:
                    if (timeBase.Value != TimeBase.PreCountOverflow)
                    {
                        throw new Exception("StartTimer: timeBase not supported");
                    }

                    timer.Frequency = protocolTimer.GetPreCountOverflowFrequency();
                    timer.Limit = sequence[sequenceIndex].RemainingDelayTicks();
                break;
                case TimerStartReason.WaitForReg:
                    timer.Frequency = MicrosecondFrequency;
                    timer.Limit = 1;
                break;
                default:
                    throw new Exception("StartTimer: invalid start reason");
            }

            timer.Enabled = true;
        }
        
        public void TimerLimitReached()
        {
            timer.Enabled = false;
            switch(timerStartReason)
            {
                case TimerStartReason.DeferredStart:
                    sequence[timerSequenceIndex].StartAfterDefer();
                break;
                case TimerStartReason.WaitForReg:
                    sequence[timerSequenceIndex].ResumeExecution();
                break;
                case TimerStartReason.Delay:
                    sequence[timerSequenceIndex].DelayCompleted();
                break;
                default:
                    throw new Exception("TimerLimitReached: invalid start reason");
            }
        }

        public void ResetAbsoluteDelayCounter()
        {
            // Flush out current outstanding protimer ticks and then reset the absolute delay counter.
            protocolTimer.FlushCurrentPreCountOverflows();
            absoluteDelayCounter = 0;

            // If there is a sequence currently running, notify the sequence after the reset.
            for(uint i = 0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsRunning)
                {
                    sequence[i].AbsoluteDelayCounterHasReset();
                }
            }
        }

        // RENODE-188: we need to call this wherever needed to raise the related signal
        // TODO: when we start using this API, we will need to assess if it is always safe to be called
        //       or if we should at time defer via timer.
        protected void SetSignal(Signal signal, bool value)
        {
            if (value)
            {
                SignalBitmask |= (uint)(1 << (int)signal);
                
                for(uint i = 0; i<NumberOfSequenceConfigurations; i++)
                {
                    if (sequence[i].WaitingForSignal)
                    {
                        sequence[i].ResumeExecution();
                        break;
                    }
                }
            }
            else
            {
                SignalBitmask &= ~((uint)(1 << (int)signal));
            }
        }

        protected void SetTrigger(uint bitmask, OpCodeExtended action)
        {
            switch(action)
            {
                case OpCodeExtended.Write:
                {
                    TriggerBitmask = bitmask;
                    break;
                }
                case OpCodeExtended.Set:
                {
                    TriggerBitmask |= bitmask;
                    break;
                }
                case OpCodeExtended.Clear:
                {
                    TriggerBitmask &= ~bitmask;
                    break;
                }
                case OpCodeExtended.Toggle:
                {
                    TriggerBitmask ^= bitmask;
                    break;
                }
                default:
                    throw new Exception("SetTrigger: invalid action");
            }
            
            // RENODE-188: handle trigger bitmask change
        }

        protected void SequenceStart(uint sequenceBitmask, bool deferStart)
        {
            // Starting multiple sequences simultaneously (either by register write or by HW event) results on 
            // only a single sequence to start running and the others become pending to run afterwards. 
            // A hybrid arbitration scheme exists to select which sequence from the pending list has to run. 
            // A priority arbitration mechanism is first use to run the sequences with the highest priority, 
            // and a round-robin arbitration scheme is applied on the remaining sequences.
            
            // TODO: for now we don't support the hybrid arbitration scheme, we execute sequences one at the time.

            bool sequenceRunning = false;
            int sequenceToStartIndex = (int)NumberOfSequenceConfigurations;

            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsRunning)
                {
                    sequenceRunning = true;
                    break;
                }
            }

            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if ((sequenceBitmask & ((uint)(1 << i))) > 0)
                {
                    if (!sequenceRunning)
                    {
                        sequenceRunning = true;
                        sequenceToStartIndex = i;
                    }
                    else
                    {
                        sequence[i].IsPending = true;
                    }
                }
            }

            if (sequenceToStartIndex < NumberOfSequenceConfigurations)
            {
                sequence[sequenceToStartIndex].Start(deferStart);
            }
        }

        public void SequenceCompleted(uint index)
        {
            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsPending)
                {
                    sequence[i].Start(true);
                    break;
                }
            }
        }

        protected void SequenceAbort(uint sequenceBitmask)
        {
            // To abort a running or a pending sequence, the CTRL.ABORT register bitfield is written and the 
            // SEQABORT interrupt flag is set. A bus error also aborts all of the running and pending sequences.

            bool sequenceAborted = false;

            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if ((sequenceBitmask & ((uint)(1 << i))) > 0)
                {
                    if (sequence[i].Abort())
                    {
                        sequenceAborted = true;
                    }
                }
            }

            if (sequenceAborted)
            {
                sequenceAbortedInterrupt.Value = true;
                seqSequenceAbortedInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        protected uint GetPendingSequencesBitmask()
        {
            uint bitmask = 0;
            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsPending)
                {
                    bitmask |= (uint)(1 << i);
                }
            }
            return bitmask;
        }

        protected uint GetRunningSequencesBitmask()
        {
            uint bitmask = 0;
            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsRunning)
                {
                    bitmask |= (uint)(1 << i);
                }
            }
            return bitmask;
        }

        protected uint GetRunningSequenceCurrentAddress()
        {
            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (sequence[i].IsRunning)
                {
                    return sequence[i].CurrentAddress;
                }
            }
            return 0;
        }

        protected void TriggerHardwareStartEvent(HardwareStartSelect startEvent)
        {
            for(int i=0; i<NumberOfSequenceConfigurations; i++)
            {
                if (!sequence[i].IsRunning 
                    && !sequence[i].IsPending 
                    && sequence[i].hardwareTriggerMode.Value != HardwareTriggerMode.Disabled 
                    && sequence[i].hardwareStartSelect.Value == startEvent)
                {
                    SequenceStart((1U << i), true);
                }
            }
        }
#endregion

#region enums
        public enum TimerStartReason
        {
            DeferredStart = 0,
            Delay         = 1,
            WaitForReg    = 2,
            Invalid       = 3,
        }

        public enum TimeBase
        {
            Clock             = 0,
            PreCountOverflow  = 1,
            BaseCountOverflow = 2,
            WrapCountOverflow = 3,
        }

        protected enum OpCode
        {
            Write          = 0x0,
            And            = 0x1,
            Xor            = 0x2,
            Or             = 0x3,
            Delay          = 0x4,
            Mov            = 0x5,
            MovNoInc       = 0x6,
            WaitForReg     = 0x7,
            WaitForSig     = 0x8,
            WriteNoInc     = 0x9,
            Jump           = 0xA,
            SetTrig        = 0xB,
            MoveBlock      = 0xC,
            SkipCond       = 0xD,
            Reserved       = 0xE,
            EndSeqOrNop    = 0xF,
        }

        protected enum OpCodeExtended
        {
            // Delay/Jump OpCode
            Relative      = 0x0,
            Absolute      = 0x1,
            // WaitForReg/WaitForSig OpCode
            All           = 0x0,
            Any           = 0x1,
            NegAll        = 0x2,
            NegAny        = 0x3,
            Eq0           = 0x4,
            Neq0          = 0x5,
            Eq1           = 0x6,
            Neq1          = 0x7,
            // SetTrig OpCode
            Write         = 0x0,
            Set           = 0x1,
            Clear         = 0x2,
            Toggle        = 0x3,
            // SkipCond OpCode
            RegAll        = 0x0,
            RegAny        = 0x1,
            RegNegAll     = 0x2,
            RegNegAny     = 0x3,
            RegEq0        = 0x4,
            RegNeq0       = 0x5,
            RegEq1        = 0x6,
            RegNeq1       = 0x7,
            SigAll        = 0x8,
            SigAny        = 0x9,
            SigNegAll     = 0xA,
            SigNegAny     = 0xB,
            SigEq0        = 0xC,
            SigNeq0       = 0xD,
            SigEq1        = 0xE,
            SigNeq1       = 0xF,
            // EndSeqOrNop OpCode
            Nop           = 0x0,
            EndSeq        = 0x1,
        }

        protected enum Signal
        {
            PrsConsumerWait0              = 0,
            PrsConsumerWait1              = 1,
            PrsConsumerWait2              = 2,
            PrsConsumerWait3              = 3,
            PrsSourceAgcCca               = 4,
            PrsSourceModemAnt1            = 5,
            PrsSourceModemFrameSent       = 6,
            PrsSourceModemFrameDetect     = 7,
            PrsSourceModemPreambleDetect  = 8,
            PrsSourceModemEof             = 9,
            PrsSourceProtimerLbt          = 10,
            ProtimerCaptureCompare9If     = 11,
            ProtimerCaptureCompare10If    = 12,
            ProtimerCaptureCompare11If    = 13,
            PrsSourceProtimerTout0Match   = 14,
            PrsSourceProtimerTout1Match   = 15,
            PrsSourceRacTx                = 16,
            PrsSourceRacRx                = 17,
            PrsSourceRacPaEnable          = 18,
            PrsSourceRacLnaEnable         = 19,
            PrsSourceRacActive            = 20,
            PrsSourceRfTimerMatch0        = 21,
            PrsSourceRfTimerMatch1        = 22,
            PrsSourceRfTimerOverflow      = 23,
            ModemNoiseDetect              = 24,
            ModemHoppingEvent             = 25,
            RacDcDone                     = 26,
            FastSwModemInterrupt          = 27,
            FastSwSynthInterrupt          = 28,
            FastSwFrcInterrupt            = 29,
            FastSwAgcInterrupt            = 30,
            FastSwRacInterrupt            = 31,
        }

        protected enum Trigger
        {
            Trigger0 = 0,
            Trigger1 = 1,
            Trigger2 = 2,
            Trigger3 = 3,
        }

        protected enum HardwareTriggerMode
        {
            Disabled = 0,
            Low      = 2,
            High     = 3,
            Falling  = 4,
            Rising   = 5,
            Edge     = 6,
        }

        protected enum HardwareStartSelect
        {
            PrsWait0                 = 0,
            PrsWait1                 = 1,
            PrsWait2                 = 2,
            PrsWait3                 = 3,
            AgcCca                   = 4,
            ModemAnt1                = 5,
            ModemFrameSent           = 6,
            ModemFrameDetect         = 7,
            ModemPreambleDetect      = 8,
            ModemEof                 = 9,
            ProtimerLbts             = 10,
            ProtimerCaptureCompare9  = 11,
            ProtimerCaptureCompare10 = 12,
            ProtimerCaptureCompare11 = 13,
            ProtimerTimeout0Match    = 14,
            ProtimerTimeout1Match    = 15,
            RacTx                    = 16,
            RacRx                    = 17,
            RacPaEnable              = 18,
            RacLnaEnable             = 19,
            RacActive                = 20,
            RfTimerMatch0            = 21,
            RfTimerMatch1            = 22,
            RfTimerOverflow          = 23,
            ModemNoiseEdge           = 24,
            ModemHoppingEdge         = 25,
            RacDcDone                = 26,
            ModemFswInterrupt        = 27,
            SynthFswInterrupt        = 28,
            FrcFswInterrupt          = 29,
            AgcFswInterrupt          = 30,
            RacFswInterrupt          = 31,
        }
#endregion

        protected class Sequence
        {
            public Sequence(SiLabs_SequencerAccelerator parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            protected SiLabs_SequencerAccelerator parent;
            protected uint index;
            protected uint currentAddress;
            protected bool isPending = false;
            protected bool isRunning = false;
            protected bool inDelay = false;
            protected bool waitingForSignal = false;
            protected uint delayRemainingTicksOrTopValue = 0;
            protected bool delayIsRelative = false;
            public IFlagRegisterField sequenceDoneInterrupt;
            public IFlagRegisterField sequenceDoneInterruptEnable;
            public IFlagRegisterField seqSequenceDoneInterrupt;
            public IFlagRegisterField seqSequenceDoneInterruptEnable;
            public IValueRegisterField startAddress;
            // Config fields
            public IValueRegisterField continuousWritePosition;
            public IEnumRegisterField<HardwareStartSelect> hardwareStartSelect;
            public IEnumRegisterField<HardwareTriggerMode> hardwareTriggerMode;
            public IFlagRegisterField disableAbsoluteDelayReset;
            public IFlagRegisterField movSwapAddresses;

            public bool Interrupt => (sequenceDoneInterrupt.Value && sequenceDoneInterruptEnable.Value);
            public bool SeqInterrupt => (seqSequenceDoneInterrupt.Value && seqSequenceDoneInterruptEnable.Value);
            
            // Methods
            public bool IsRunning => isRunning;
            public bool InDelay => inDelay;
            public bool WaitingForSignal => waitingForSignal;
            protected LogLevel LocalLogLevel => parent.LogSequenceAcceleratorActivityAsWarning ? LogLevel.Warning : LogLevel.Noisy;
            public bool IsPending 
            {
                set
                {
                    isPending = value;
                }
                get
                {
                    return isPending;
                }
            }
            public uint CurrentAddress => currentAddress;
            public void Start(bool deferStart)
            {
                isPending = false;
                isRunning = true;
                waitingForSignal = false;
                inDelay = false;

                currentAddress = (uint)startAddress.Value;

                if (!disableAbsoluteDelayReset.Value)
                {
                    parent.ResetAbsoluteDelayCounter();
                }

                string symbol;
                bool symbolFound = (parent.machine.SystemBus.TryFindSymbolAt(currentAddress, out symbol, out var _)
                                    && !symbol.Contains("guessed"));
                parent.Log(LocalLogLevel, "SEQUENCE-#{0}: Starting at {1}: address=0x{2:X} counter={3} symbol={4}", 
                            index, parent.GetTime(), currentAddress, parent.AbsoluteDelayCounter, (symbolFound ? symbol : "not available"));
                
                if (deferStart)
                {
                    parent.StartTimer(index, TimerStartReason.DeferredStart);
                }
                else
                {
                    // The DELAYABS instruction pauses the execution of a sequence until a timestamp is reached 
                    // (i.e., it is equal or exceeds) which is specified in the Data word. It uses an internal absolute 
                    // delay counter that is reset at the start of a sequence by default. This automatic reset can be 
                    // prevented by setting to 1 the bit SEQCFG[x].DISABSRST. 
                    if (!disableAbsoluteDelayReset.Value)
                    {
                        parent.ResetAbsoluteDelayCounter();
                    }

                    ResumeExecution();
                }
            }

            public void StartAfterDefer()
            {
                if (!disableAbsoluteDelayReset.Value)
                {
                    parent.ResetAbsoluteDelayCounter();
                }

                ResumeExecution();
            }

            public bool Abort()
            {
                if (isPending || isRunning)
                {
                    isPending = false;
                    isRunning = false;
                    waitingForSignal = false;
                    inDelay = false;
                    
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void ResumeExecution()
            {
                if (!isRunning)
                {
                    throw new Exception("SEQACC: ResumeExecution isRunning=false");
                }
                if (isPending)
                {
                    throw new Exception("SEQACC: ResumeExecution isPending=true");
                }
                if (inDelay)
                {
                    throw new Exception("SEQACC: ResumeExecution inDelay=true");
                }

                parent.Log(LocalLogLevel, "SEQUENCE-#{0}: Resuming at {1}: address=0x{2:X} counter={3} BASECNT={4}", 
                           index, parent.GetTime(), currentAddress, parent.AbsoluteDelayCounter, parent.protocolTimer.GetBaseCountValue());

                ExecuteResult result;
                do
                {
                    result = ExecuteSingleInstruction();
                } 
                while(result == ExecuteResult.Done);

                if (result == ExecuteResult.EndOfSequence)
                {
                    isRunning = false;
                    sequenceDoneInterrupt.Value = true;
                    seqSequenceDoneInterrupt.Value = true;
                    parent.UpdateInterrupts();

                    parent.Log(LocalLogLevel, "SEQUENCE-#{0}: Completed, counter={1} BASECNT={2}", 
                               index, parent.AbsoluteDelayCounter, parent.protocolTimer.GetBaseCountValue()); 

                    parent.SequenceCompleted(index);
                }
            }

            public uint RemainingDelayTicks()
            {
                if (delayIsRelative)
                {
                    if (delayRemainingTicksOrTopValue == 0)
                    {
                        parent.Log(LogLevel.Error, "SEQACC: REMAINING_TICKS == 0 {0}", delayRemainingTicksOrTopValue);
                        throw new Exception("SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE");
                    }

                    return delayRemainingTicksOrTopValue;
                }
                else
                {
                    if (parent.AbsoluteDelayCounter >= delayRemainingTicksOrTopValue)
                    {
                        parent.Log(LogLevel.Error, "SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE {0} {1}", parent.AbsoluteDelayCounter, delayRemainingTicksOrTopValue);
                        throw new Exception("SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE");
                    }

                    return delayRemainingTicksOrTopValue - parent.AbsoluteDelayCounter;
                }
            }

            public void AbsoluteDelayCounterHasReset()
            {
                if (InDelay)
                {
                    parent.Log(LocalLogLevel, "Counter has reset while in delay at {0}: remainingTicks={1} BASECNT={2} counter={3}", 
                               parent.GetTime(), RemainingDelayTicks(), parent.protocolTimer.GetBaseCountValue(), parent.AbsoluteDelayCounter);

                    parent.StartTimer(index, TimerStartReason.Delay);
                }                
            }

            public void DelayCompleted()
            {
                if (!inDelay)
                {
                    parent.Log(LogLevel.Error, "SEQACC: DelayCompleted() while not in delay");
                    throw new Exception("SEQACC: DelayCompleted() while not in delay");
                }

                inDelay = false;
                ResumeExecution();
            }

            // Executes a single instruction and updates the current address.
            protected ExecuteResult ExecuteSingleInstruction()
            {
                // O the width of OFFSET bitfield (O = CONT_WRITE_POSITION)
                int O = (int)continuousWritePosition.Value;
                // L the width of the LENGTH/OPCODE_EXTENSION bitfield (L = BASE_POSITION - CONT_WRITE_POSITION)
                int L = (int)parent.baseAddressPosition.Value - (int)continuousWritePosition.Value;
                // B the width of the BASE bitfield (B = 28 - BASE_POSITION)
                int B = 28 - (int)parent.baseAddressPosition.Value;

                uint word = parent.machine.SystemBus.ReadDoubleWord(currentAddress);   
                uint offset = word & GetMask(O, 0);
                uint lengthOrOpExt = (word & GetMask(L, O)) >> O;
                uint baseAddressIndex = (word & GetMask(B, O+L)) >> (O+L);
                OpCode opCode = (OpCode)((word & 0xF0000000) >> 28);
                
                // The target address of an operation (e.g., a write) is computed from the OFFSET field present in the argument word, 
                // added to a base address set into an APB register.
                uint targetAddress = (uint)parent.baseAddress[baseAddressIndex].Value + offset;

                ExecuteResult ret = ExecuteResult.Done;
                uint newCurrentAddress = 0;

                parent.Log(LocalLogLevel, "SEQUENCE-#{0}: Instruction: currentAddress={1:X} opCode={2} O={3} L={4} B={5} baseIndex={6} Length/OpExt={7} Offset={8:X} baseAddr={9:X} targetAddr={10:X} ARGWORD={11:X}", 
                            index, currentAddress, opCode, O, L, B, baseAddressIndex, lengthOrOpExt, offset, parent.baseAddress[baseAddressIndex].Value, targetAddress, word);
                
                switch(opCode)
                {
                    case OpCode.Write:
                    case OpCode.WriteNoInc:
                    {
                        bool moveDestination = (opCode == OpCode.Write);
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(targetAddress + (moveDestination ? i*4 : 0), dataWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.Mov:
                    {
                        uint srcBaseAddress;
                        uint destBaseAddress;

                        if (movSwapAddresses.Value)
                        {
                            srcBaseAddress = targetAddress;
                            destBaseAddress = currentAddress + 4;
                        }
                        else
                        {
                            srcBaseAddress = currentAddress + 4;
                            destBaseAddress = targetAddress;
                        }
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(destBaseAddress + i*4, dataWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.MovNoInc:
                    {
                        // TODO: it is unclear what is the behavior when MOVSWAP is set for a MovNoInc instruction,
                        // so for now we ignore it.
                        uint srcBaseAddress = currentAddress + 4;
                        uint destBaseAddress = targetAddress;

                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(destBaseAddress, dataWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.MoveBlock:
                    {
                        uint srcBaseAddress;
                        uint destBaseAddress;

                        if (movSwapAddresses.Value)
                        {
                            srcBaseAddress = targetAddress;
                            destBaseAddress = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        }
                        else
                        {
                            srcBaseAddress = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4); 
                            destBaseAddress = targetAddress;
                        }
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(destBaseAddress + i*4, dataWord);
                        }
                        newCurrentAddress = currentAddress + 8;
                        break;
                    }
                    case OpCode.And:
                    {
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                            uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(targetAddress + i*4, dataWord & targetWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.Xor:
                    {
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                            uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(targetAddress + i*4, dataWord ^ targetWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.Or:
                    {
                        for(uint i=0; i<lengthOrOpExt; i++)
                        {
                            uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                            uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                            parent.machine.SystemBus.WriteDoubleWord(targetAddress + i*4, dataWord | targetWord);
                        }
                        newCurrentAddress = currentAddress + 4 + lengthOrOpExt*4;
                        break;
                    }
                    case OpCode.Delay:
                    {
                        uint time = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);

                        if (parent.timeBase.Value == TimeBase.Clock)
                        {
                            throw new Exception("SEQACC: Clock timebase not supported");
                        }
                        
                        inDelay = true;
                        delayIsRelative = (OpCodeExtended)lengthOrOpExt != OpCodeExtended.Absolute;
                        delayRemainingTicksOrTopValue = time;

                        parent.StartTimer(index, TimerStartReason.Delay);
                        
                        newCurrentAddress = currentAddress + 8;
                        ret = ExecuteResult.Wait;
                        break;
                    }
                    case OpCode.WaitForReg:
                    {
                        uint regValue = parent.machine.SystemBus.ReadDoubleWord(targetAddress);
                        uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        bool conditionPassed = false;
                        
                        switch((OpCodeExtended)lengthOrOpExt)
                        {
                            case OpCodeExtended.All:
                                conditionPassed = ((regValue & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.Any:
                                conditionPassed = ((regValue & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.NegAll:
                                conditionPassed = ((~regValue & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.NegAny:
                                conditionPassed = ((~regValue & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.Eq0:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition0.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.Neq0:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition0.Value) != dataOrMask);
                                break;
                            case OpCodeExtended.Eq1:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition1.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.Neq1:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition1.Value) != dataOrMask);
                                break;
                            default:
                                throw new Exception("SEQACC: WAITFORREG invalid OpCodeExtended");
                        }

                        if (conditionPassed)
                        {
                            newCurrentAddress = currentAddress + 8;
                        }
                        else
                        {
                            newCurrentAddress = currentAddress;
                            parent.StartTimer(index, TimerStartReason.WaitForReg);
                            ret = ExecuteResult.Wait;
                        }
                        break;
                    }
                    case OpCode.WaitForSig:
                    {
                        uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        bool conditionPassed = false;

                        switch((OpCodeExtended)lengthOrOpExt)
                        {
                            case OpCodeExtended.All:
                                conditionPassed = ((parent.SignalBitmask & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.Any:
                                conditionPassed = ((parent.SignalBitmask & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.NegAll:
                                conditionPassed = ((~parent.SignalBitmask & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.NegAny:
                                conditionPassed = ((~parent.SignalBitmask & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.Eq0:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition0.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.Neq0:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition0.Value) != dataOrMask);
                                break;
                            case OpCodeExtended.Eq1:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition1.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.Neq1:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition1.Value) != dataOrMask);
                                break;
                            default:
                                throw new Exception("SEQACC: WAITFORSIG invalid OpCodeExtended");
                        }

                        if (conditionPassed)
                        {
                            newCurrentAddress = currentAddress + 8;
                            waitingForSignal = false;
                        }
                        else
                        {
                            newCurrentAddress = currentAddress;
                            waitingForSignal = true;
                            ret = ExecuteResult.Wait;
                        }
                        break;
                    }
                    case OpCode.Jump:
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        // If the Data word is set to 0, this instruction has no effect and simply proceeds to the next instruction.
                        if (dataWord == 0)
                        {
                            newCurrentAddress = currentAddress + 8;
                        }
                        else
                        {
                            switch((OpCodeExtended)lengthOrOpExt)
                            {
                                case OpCodeExtended.Relative:
                                {
                                    newCurrentAddress = currentAddress + 8 + dataWord;
                                    break;
                                }
                                case OpCodeExtended.Absolute:
                                {
                                    newCurrentAddress = dataWord;
                                    break;
                                }
                                default:
                                    throw new Exception("SEQACC: JUMP invalid OpCodeExtended");
                            }
                        }
                        break;
                    }
                    case OpCode.SetTrig:
                    {
                        if ((OpCodeExtended)lengthOrOpExt > OpCodeExtended.Toggle)
                        {
                            throw new Exception("SEQACC: SETTRIG invalid OpCodeExtended");
                        }

                        uint mask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        parent.SetTrigger(mask, (OpCodeExtended)lengthOrOpExt);
                        newCurrentAddress = currentAddress + 8;
                        break;
                    }
                    case OpCode.SkipCond:
                    {
                        uint regValue = parent.machine.SystemBus.ReadDoubleWord(targetAddress);
                        uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        bool conditionPassed = false;

                        switch((OpCodeExtended)lengthOrOpExt)
                        {
                            case OpCodeExtended.RegAll:
                                conditionPassed = ((regValue & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.RegAny:
                                conditionPassed = ((regValue & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.RegNegAll:
                                conditionPassed = ((~regValue & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.RegNegAny:
                                conditionPassed = ((~regValue & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.RegEq0:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition0.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.RegNeq0:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition0.Value) != dataOrMask);
                                break;
                            case OpCodeExtended.RegEq1:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition1.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.RegNeq1:
                                conditionPassed = ((regValue & (uint)parent.equalMaskCondition1.Value) != dataOrMask);
                                break;
                            case OpCodeExtended.SigAll:
                                conditionPassed = ((parent.SignalBitmask & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.SigAny:
                                conditionPassed = ((parent.SignalBitmask & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.SigNegAll:
                                conditionPassed = ((~parent.SignalBitmask & dataOrMask) == dataOrMask);
                                break;
                            case OpCodeExtended.SigNegAny:
                                conditionPassed = ((~parent.SignalBitmask & dataOrMask) != 0);
                                break;
                            case OpCodeExtended.SigEq0:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition0.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.SigNeq0:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition0.Value) != dataOrMask);
                                break;
                            case OpCodeExtended.SigEq1:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition1.Value) == dataOrMask);
                                break;
                            case OpCodeExtended.SigNeq1:
                                conditionPassed = ((parent.SignalBitmask & (uint)parent.equalMaskCondition1.Value) != dataOrMask);
                                break;
                            default:
                                throw new Exception("SEQACC: SKIPCOND invalid OpCodeExtended");
                        }

                        // Skip the next instruction if condition is verified
                        newCurrentAddress = currentAddress + (conditionPassed ? 16U : 8U);
                        break;
                    }
                    case OpCode.EndSeqOrNop:
                    {
                        newCurrentAddress = currentAddress + 4;
                        if (lengthOrOpExt > 0)
                        {
                            ret = ExecuteResult.EndOfSequence;
                        }
                        break;
                    }                    
                    case OpCode.Reserved:
                    {
                        throw new Exception("SEQACC: Reserved OpCode");
                    }
                    default:
                    {
                        throw new Exception("SEQACC: Unexpected OpCode");
                    }
                }

                currentAddress = newCurrentAddress;
                return ret;
            } 

            protected uint GetMask(int size, int shift)
            {
                uint ret = 0;
                for(int i=shift; i<size+shift; i++)
                {
                    ret |= (uint)(1 << i);
                }
                return ret;
            }

            protected enum ExecuteResult
            {
                EndOfSequence = 0,
                Wait          = 1,
                Done          = 2,
            }
        }
    }
}