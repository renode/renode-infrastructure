//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public abstract class SiLabs_SequencerAccelerator : SiLabsPeripheral
    {
        public SiLabs_SequencerAccelerator(Machine machine, long frequency, SiLabs_IProtocolTimer protocolTimer = null) : base(machine, false)
        {
            ProtocolTimer = protocolTimer;
            ProtocolTimer.PreCountOverflowsEvent += PreCountOverflowsEventCallback;
            ProtocolTimer.BaseCountOverflowsEvent += BaseCountOverflowsEventCallback;
            ProtocolTimer.WrapCountOverflowsEvent += WrapCountOverflowsEventCallback;
            ProtocolTimer.CaptureCompareEvent += CaptureCompareEventCallback;

            timer = new LimitTimer(machine.ClockSource, frequency, this, "seqacc_timer", 0xFFFFUL, direction: Direction.Ascending,
                                         enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += TimerLimitReached;

            sequence = new Sequence[NumberOfSequenceConfigurations];
            for(var idx = 0; idx < NumberOfSequenceConfigurations; ++idx)
            {
                var i = idx;
                sequence[i] = new Sequence(this, (uint)i);
            }
            Field_1 = new IValueRegisterField[NumberOfBaseAddresses];

            HostIRQ = new GPIO();
            SequencerIRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public override void Reset()
        {
            base.Reset();

            absoluteDelayCounter = 0;
            signalBitmask = 0;
            triggerBitmask = 0;
            timerStartReason = TimerStartReason.Invalid;
            timerSequenceIndex = 0;
            timer.Enabled = false;

            for(var idx = 0; idx < NumberOfSequenceConfigurations; ++idx)
            {
                sequence[idx].Abort();
            }
        }

        public void PreCountOverflowsEventCallback(uint overflowCount)
        {
            if(field_18.Value && Field_15.Value == TimeBase.PreCountOverflow)
            {
                absoluteDelayCounter += overflowCount;
            }
        }

        public void BaseCountOverflowsEventCallback(uint overflowCount)
        {
            if(field_18.Value && Field_15.Value == TimeBase.BaseCountOverflow)
            {
                absoluteDelayCounter += overflowCount;
            }
        }

        public void WrapCountOverflowsEventCallback(uint overflowCount)
        {
            if(field_18.Value && Field_15.Value == TimeBase.WrapCountOverflow)
            {
                absoluteDelayCounter += overflowCount;
            }
        }

        public void CaptureCompareEventCallback(uint captureCompareIndex)
        {
            if(captureCompareIndex >= 9 && captureCompareIndex <= 11)
            {
                Enumeration_B evt = (Enumeration_B)((uint)Enumeration_B.EnumerationBValue11 + (captureCompareIndex - 9));
                TriggerHardwareStartEvent(evt);
            }
        }

        public void SequenceCompleted(uint index)
        {
            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsPending)
                {
                    sequence[i].Start(true);
                    break;
                }
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
                timer.Frequency = ProtocolTimer.GetPreCountOverflowFrequency();
                timer.Limit = 1;
                break;
            case TimerStartReason.Delay:
                if(Field_15.Value != TimeBase.PreCountOverflow)
                {
                    throw new Exception("StartTimer: timeBase not supported");
                }

                timer.Frequency = ProtocolTimer.GetPreCountOverflowFrequency();
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
            ProtocolTimer.FlushCurrentPreCountOverflows();
            absoluteDelayCounter = 0;

            for(uint i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsRunning)
                {
                    sequence[i].AbsoluteDelayCounterHasReset();
                }
            }
        }

        public GPIO HostIRQ { get; }

        public GPIO SequencerIRQ { get; }

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

        public uint AbsoluteDelayCounter
        {
            get
            {
                if(Field_15.Value == TimeBase.Clock)
                {
                    throw new Exception("SEQACC: clock timebase not supported");
                }

                var ret = absoluteDelayCounter;

                if(Field_15.Value == TimeBase.PreCountOverflow)
                {
                    ret += ProtocolTimer.GetCurrentPreCountOverflows();
                }

                return ret;
            }
        }

        public IValueRegisterField Field_6;
        public IValueRegisterField Field_5;
        public IValueRegisterField Field_2;
        public IEnumRegisterField<TimeBase> Field_15;
        public IValueRegisterField[] Field_1;
        public bool LogSequenceAcceleratorActivityAsWarning = false;
        public readonly SiLabs_IProtocolTimer ProtocolTimer;

        protected void SetTrigger(uint bitmask, Enumeration_E action)
        {
            switch(action)
            {
            case Enumeration_E.EnumerationEValue10:
            {
                TriggerBitmask = bitmask;
                break;
            }
            case Enumeration_E.EnumerationEValue11:
            {
                TriggerBitmask |= bitmask;
                break;
            }
            case Enumeration_E.EnumerationEValue12:
            {
                TriggerBitmask &= ~bitmask;
                break;
            }
            case Enumeration_E.EnumerationEValue13:
            {
                TriggerBitmask ^= bitmask;
                break;
            }
            default:
                throw new Exception("SetTrigger: invalid action");
            }

        }

        protected void SequenceStart(uint sequenceBitmask, bool deferStart)
        {


            bool sequenceRunning = false;
            int sequenceToStartIndex = (int)NumberOfSequenceConfigurations;

            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsRunning)
                {
                    sequenceRunning = true;
                    break;
                }
            }

            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if((sequenceBitmask & ((uint)(1 << i))) > 0)
                {
                    if(!sequenceRunning)
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

            if(sequenceToStartIndex < NumberOfSequenceConfigurations)
            {
                sequence[sequenceToStartIndex].Start(deferStart);
            }
        }

        protected void SequenceAbort(uint sequenceBitmask)
        {

            bool sequenceAborted = false;

            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if((sequenceBitmask & ((uint)(1 << i))) > 0)
                {
                    if(sequence[i].Abort())
                    {
                        sequenceAborted = true;
                    }
                }
            }

            if(sequenceAborted)
            {
                field_26.Value = true;
                field_22.Value = true;
                UpdateInterrupts();
            }
        }

        protected uint GetPendingSequencesBitmask()
        {
            uint bitmask = 0;
            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsPending)
                {
                    bitmask |= (uint)(1 << i);
                }
            }
            return bitmask;
        }

        protected uint GetRunningSequencesBitmask()
        {
            uint bitmask = 0;
            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsRunning)
                {
                    bitmask |= (uint)(1 << i);
                }
            }
            return bitmask;
        }

        protected uint GetRunningSequenceCurrentAddress()
        {
            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(sequence[i].IsRunning)
                {
                    return sequence[i].CurrentAddress;
                }
            }
            return 0;
        }

        protected void TriggerHardwareStartEvent(Enumeration_B startEvent)
        {
            for(int i = 0; i < NumberOfSequenceConfigurations; i++)
            {
                if(!sequence[i].IsRunning
                    && !sequence[i].IsPending
                    && sequence[i].Field_8.Value != Enumeration_C.EnumerationCValue0
                    && sequence[i].Field_7.Value == startEvent)
                {
                    SequenceStart((1U << i), true);
                }
            }
        }

        protected void SetSignal(Enumeration_F signal, bool value)
        {
            if(value)
            {
                SignalBitmask |= (uint)(1 << (int)signal);

                for(uint i = 0; i < NumberOfSequenceConfigurations; i++)
                {
                    if(sequence[i].WaitingForSignal)
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

        protected IFlagRegisterField field_21;
        protected IFlagRegisterField field_22;
        protected IFlagRegisterField field_23;
        protected uint absoluteDelayCounter = 0;
        protected uint signalBitmask = 0;
        protected uint triggerBitmask = 0;
        protected uint timerSequenceIndex = 0;
        protected Sequence[] sequence;
        protected IFlagRegisterField field_18;
        protected IValueRegisterField field_19;
        protected IFlagRegisterField field_25;
        protected IValueRegisterField field_30;
        protected IFlagRegisterField field_28;
        protected IFlagRegisterField field_16;
        protected IFlagRegisterField field_27;
        protected IFlagRegisterField field_29;
        protected IFlagRegisterField field_17;
        protected IFlagRegisterField field_24;
        protected IFlagRegisterField field_20;
        protected IFlagRegisterField field_26;
        protected readonly LimitTimer timer;
        protected const long MicrosecondFrequency = 1000000L;
        protected const uint NumberOfBaseAddresses = 16;
        protected const uint NumberOfSequenceConfigurations = 8;
        protected const uint NumberOfTriggerOutputSignals = 4;
        protected const uint NumberOfWaitStartSignals = 32;
        TimerStartReason timerStartReason = TimerStartReason.Invalid;

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

        protected class Sequence
        {
            public Sequence(SiLabs_SequencerAccelerator parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void DelayCompleted()
            {
                if(!inDelay)
                {
                    parent.Log(LogLevel.Error, "SEQACC: DelayCompleted() while not in delay");
                    throw new Exception("SEQACC: DelayCompleted() while not in delay");
                }

                inDelay = false;
                ResumeExecution();
            }

            public void AbsoluteDelayCounterHasReset()
            {
                if(InDelay)
                {

                    parent.StartTimer(index, TimerStartReason.Delay);
                }
            }

            public uint RemainingDelayTicks()
            {
                if(delayIsRelative)
                {
                    if(delayRemainingTicksOrTopValue == 0)
                    {
                        parent.Log(LogLevel.Error, "SEQACC: REMAINING_TICKS == 0 {0}", delayRemainingTicksOrTopValue);
                        throw new Exception("SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE");
                    }

                    return delayRemainingTicksOrTopValue;
                }
                else
                {
                    if(parent.AbsoluteDelayCounter >= delayRemainingTicksOrTopValue)
                    {
                        parent.Log(LogLevel.Error, "SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE {0} {1}", parent.AbsoluteDelayCounter, delayRemainingTicksOrTopValue);
                        throw new Exception("SEQACC: ABS_DELAY_COUNTER >= DELAY_TOP_VALUE");
                    }

                    return delayRemainingTicksOrTopValue - parent.AbsoluteDelayCounter;
                }
            }

            public void ResumeExecution()
            {
                if(!isRunning)
                {
                    throw new Exception("SEQACC: ResumeExecution isRunning=false");
                }
                if(isPending)
                {
                    throw new Exception("SEQACC: ResumeExecution isPending=true");
                }
                if(inDelay)
                {
                    throw new Exception("SEQACC: ResumeExecution inDelay=true");
                }


                Enumeration_A result;
                do
                {
                    result = ExecuteSingleInstruction();
                }
                while(result == Enumeration_A.EnumerationAValue2);

                if(result == Enumeration_A.EnumerationAValue0)
                {
                    isRunning = false;
                    Field_12.Value = true;
                    Field_10.Value = true;
                    parent.UpdateInterrupts();


                    parent.SequenceCompleted(index);
                }
            }

            public bool Abort()
            {
                if(isPending || isRunning)
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

            public void StartAfterDefer()
            {
                if(!Field_4.Value)
                {
                    parent.ResetAbsoluteDelayCounter();
                }

                ResumeExecution();
            }

            public void Start(bool deferStart)
            {
                isPending = false;
                isRunning = true;
                waitingForSignal = false;
                inDelay = false;

                currentAddress = (uint)Field_14.Value;

                if(!Field_4.Value)
                {
                    parent.ResetAbsoluteDelayCounter();
                }

                string symbol;
                bool symbolFound = (parent.machine.SystemBus.TryFindSymbolAt(currentAddress, out symbol, out var _)
                                    && !symbol.Contains("guessed"));

                if(deferStart)
                {
                    parent.StartTimer(index, TimerStartReason.DeferredStart);
                }
                else
                {
                    if(!Field_4.Value)
                    {
                        parent.ResetAbsoluteDelayCounter();
                    }

                    ResumeExecution();
                }
            }

            public bool InDelay => inDelay;

            public uint CurrentAddress => currentAddress;

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

            public bool Interrupt => (Field_12.Value && Field_13.Value);

            public bool SeqInterrupt => (Field_10.Value && Field_11.Value);

            public bool IsRunning => isRunning;

            public bool WaitingForSignal => waitingForSignal;

            public IFlagRegisterField Field_12;
            public IFlagRegisterField Field_9;
            public IFlagRegisterField Field_4;
            public IEnumRegisterField<Enumeration_C> Field_8;
            public IEnumRegisterField<Enumeration_B> Field_7;
            public IValueRegisterField Field_3;
            public IValueRegisterField Field_14;
            public IFlagRegisterField Field_11;
            public IFlagRegisterField Field_10;
            public IFlagRegisterField Field_13;

            protected Enumeration_A ExecuteSingleInstruction()
            {
                var o = (int)Field_3.Value;
                var l = (int)parent.Field_2.Value - (int)Field_3.Value;
                var b = 28 - (int)parent.Field_2.Value;

                uint word = parent.machine.SystemBus.ReadDoubleWord(currentAddress);
                uint offset = word & GetMask(o, 0);
                uint lengthOrOpExt = (word & GetMask(l, o)) >> o;
                uint baseAddressIndex = (word & GetMask(b, o+l)) >> (o+l);
                Enumeration_D opCode = (Enumeration_D)((word & 0xF0000000) >> 28);

                uint targetAddress = (uint)parent.Field_1[baseAddressIndex].Value + offset;

                Enumeration_A ret = Enumeration_A.EnumerationAValue2;
                uint newCurrentAddress = 0;


                switch(opCode)
                {
                case Enumeration_D.EnumerationDValue0:
                case Enumeration_D.EnumerationDValue9:
                {
                    bool moveDestination = (opCode == Enumeration_D.EnumerationDValue0);
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(targetAddress + (moveDestination ? i * 4 : 0), dataWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue5:
                {
                    uint srcBaseAddress;
                    uint destBaseAddress;

                    if(Field_9.Value)
                    {
                        srcBaseAddress = targetAddress;
                        destBaseAddress = currentAddress + 4;
                    }
                    else
                    {
                        srcBaseAddress = currentAddress + 4;
                        destBaseAddress = targetAddress;
                    }
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(destBaseAddress + i * 4, dataWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue6:
                {
                    uint srcBaseAddress = currentAddress + 4;
                    uint destBaseAddress = targetAddress;

                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(destBaseAddress, dataWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue12:
                {
                    uint srcBaseAddress;
                    uint destBaseAddress;

                    if(Field_9.Value)
                    {
                        srcBaseAddress = targetAddress;
                        destBaseAddress = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    }
                    else
                    {
                        srcBaseAddress = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                        destBaseAddress = targetAddress;
                    }
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(srcBaseAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(destBaseAddress + i * 4, dataWord);
                    }
                    newCurrentAddress = currentAddress + 8;
                    break;
                }
                case Enumeration_D.EnumerationDValue1:
                {
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                        uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(targetAddress + i * 4, dataWord & targetWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue2:
                {
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                        uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(targetAddress + i * 4, dataWord ^ targetWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue3:
                {
                    for(uint i = 0; i < lengthOrOpExt; i++)
                    {
                        uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4 + i*4);
                        uint targetWord = parent.machine.SystemBus.ReadDoubleWord(targetAddress + i*4);
                        parent.machine.SystemBus.WriteDoubleWord(targetAddress + i * 4, dataWord | targetWord);
                    }
                    newCurrentAddress = currentAddress + 4 + lengthOrOpExt * 4;
                    break;
                }
                case Enumeration_D.EnumerationDValue4:
                {
                    uint time = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);

                    if(parent.Field_15.Value == TimeBase.Clock)
                    {
                        throw new Exception("SEQACC: Clock timebase not supported");
                    }

                    inDelay = true;
                    delayIsRelative = (Enumeration_E)lengthOrOpExt != Enumeration_E.EnumerationEValue1;
                    delayRemainingTicksOrTopValue = time;

                    parent.StartTimer(index, TimerStartReason.Delay);

                    newCurrentAddress = currentAddress + 8;
                    ret = Enumeration_A.EnumerationAValue1;
                    break;
                }
                case Enumeration_D.EnumerationDValue7:
                {
                    uint regValue = parent.machine.SystemBus.ReadDoubleWord(targetAddress);
                    uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    bool conditionPassed = false;

                    switch((Enumeration_E)lengthOrOpExt)
                    {
                    case Enumeration_E.EnumerationEValue2:
                        conditionPassed = ((regValue & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue3:
                        conditionPassed = ((regValue & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue4:
                        conditionPassed = ((~regValue & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue5:
                        conditionPassed = ((~regValue & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue6:
                        conditionPassed = ((regValue & (uint)parent.Field_5.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue7:
                        conditionPassed = ((regValue & (uint)parent.Field_5.Value) != dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue8:
                        conditionPassed = ((regValue & (uint)parent.Field_6.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue9:
                        conditionPassed = ((regValue & (uint)parent.Field_6.Value) != dataOrMask);
                        break;
                    default:
                        throw new Exception("SEQACC: WAITFORREG invalid Enumeration_E");
                    }

                    if(conditionPassed)
                    {
                        newCurrentAddress = currentAddress + 8;
                    }
                    else
                    {
                        newCurrentAddress = currentAddress;
                        parent.StartTimer(index, TimerStartReason.WaitForReg);
                        ret = Enumeration_A.EnumerationAValue1;
                    }
                    break;
                }
                case Enumeration_D.EnumerationDValue8:
                {
                    uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    bool conditionPassed = false;

                    switch((Enumeration_E)lengthOrOpExt)
                    {
                    case Enumeration_E.EnumerationEValue2:
                        conditionPassed = ((parent.SignalBitmask & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue3:
                        conditionPassed = ((parent.SignalBitmask & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue4:
                        conditionPassed = ((~parent.SignalBitmask & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue5:
                        conditionPassed = ((~parent.SignalBitmask & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue6:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_5.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue7:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_5.Value) != dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue8:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_6.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue9:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_6.Value) != dataOrMask);
                        break;
                    default:
                        throw new Exception("SEQACC: WAITFORSIG invalid Enumeration_E");
                    }

                    if(conditionPassed)
                    {
                        newCurrentAddress = currentAddress + 8;
                        waitingForSignal = false;
                    }
                    else
                    {
                        newCurrentAddress = currentAddress;
                        waitingForSignal = true;
                        ret = Enumeration_A.EnumerationAValue1;
                    }
                    break;
                }
                case Enumeration_D.EnumerationDValue10:
                {
                    uint dataWord = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    if(dataWord == 0)
                    {
                        newCurrentAddress = currentAddress + 8;
                    }
                    else
                    {
                        switch((Enumeration_E)lengthOrOpExt)
                        {
                        case Enumeration_E.EnumerationEValue0:
                        {
                            newCurrentAddress = currentAddress + 8 + dataWord;
                            break;
                        }
                        case Enumeration_E.EnumerationEValue1:
                        {
                            newCurrentAddress = dataWord;
                            break;
                        }
                        default:
                            throw new Exception("SEQACC: JUMP invalid Enumeration_E");
                        }
                    }
                    break;
                }
                case Enumeration_D.EnumerationDValue11:
                {
                    if((Enumeration_E)lengthOrOpExt > Enumeration_E.EnumerationEValue13)
                    {
                        throw new Exception("SEQACC: SETTRIG invalid Enumeration_E");
                    }

                    uint mask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    parent.SetTrigger(mask, (Enumeration_E)lengthOrOpExt);
                    newCurrentAddress = currentAddress + 8;
                    break;
                }
                case Enumeration_D.EnumerationDValue13:
                {
                    uint regValue = parent.machine.SystemBus.ReadDoubleWord(targetAddress);
                    uint dataOrMask = parent.machine.SystemBus.ReadDoubleWord(currentAddress + 4);
                    bool conditionPassed = false;

                    switch((Enumeration_E)lengthOrOpExt)
                    {
                    case Enumeration_E.EnumerationEValue14:
                        conditionPassed = ((regValue & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue15:
                        conditionPassed = ((regValue & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue16:
                        conditionPassed = ((~regValue & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue17:
                        conditionPassed = ((~regValue & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue18:
                        conditionPassed = ((regValue & (uint)parent.Field_5.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue19:
                        conditionPassed = ((regValue & (uint)parent.Field_5.Value) != dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue20:
                        conditionPassed = ((regValue & (uint)parent.Field_6.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue21:
                        conditionPassed = ((regValue & (uint)parent.Field_6.Value) != dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue22:
                        conditionPassed = ((parent.SignalBitmask & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue23:
                        conditionPassed = ((parent.SignalBitmask & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue24:
                        conditionPassed = ((~parent.SignalBitmask & dataOrMask) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue25:
                        conditionPassed = ((~parent.SignalBitmask & dataOrMask) != 0);
                        break;
                    case Enumeration_E.EnumerationEValue26:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_5.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue27:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_5.Value) != dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue28:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_6.Value) == dataOrMask);
                        break;
                    case Enumeration_E.EnumerationEValue29:
                        conditionPassed = ((parent.SignalBitmask & (uint)parent.Field_6.Value) != dataOrMask);
                        break;
                    default:
                        throw new Exception("SEQACC: SKIPCOND invalid Enumeration_E");
                    }

                    newCurrentAddress = currentAddress + (conditionPassed ? 16U : 8U);
                    break;
                }
                case Enumeration_D.EnumerationDValue15:
                {
                    newCurrentAddress = currentAddress + 4;
                    if(lengthOrOpExt > 0)
                    {
                        ret = Enumeration_A.EnumerationAValue0;
                    }
                    break;
                }
                case Enumeration_D.EnumerationDValue14:
                {
                    throw new Exception("SEQACC: Reserved Enumeration_D");
                }
                default:
                {
                    throw new Exception("SEQACC: Unexpected Enumeration_D");
                }
                }

                currentAddress = newCurrentAddress;
                return ret;
            }

            protected uint GetMask(int size, int shift)
            {
                uint ret = 0;
                for(int i = shift; i < size + shift; i++)
                {
                    ret |= (uint)(1 << i);
                }
                return ret;
            }

            protected LogLevel LocalLogLevel => parent.LogSequenceAcceleratorActivityAsWarning ? LogLevel.Warning : LogLevel.Noisy;

            protected SiLabs_SequencerAccelerator parent;
            protected uint index;
            protected uint currentAddress;
            protected bool isPending = false;
            protected bool isRunning = false;
            protected bool inDelay = false;
            protected bool waitingForSignal = false;
            protected uint delayRemainingTicksOrTopValue = 0;
            protected bool delayIsRelative = false;

            protected enum Enumeration_A
            {
                EnumerationAValue0 = 0,
                EnumerationAValue1 = 1,
                EnumerationAValue2 = 2,
            }
        }

        protected enum Enumeration_D
        {
            EnumerationDValue0    = 0x0,
            EnumerationDValue1    = 0x1,
            EnumerationDValue2    = 0x2,
            EnumerationDValue3    = 0x3,
            EnumerationDValue4    = 0x4,
            EnumerationDValue5    = 0x5,
            EnumerationDValue6    = 0x6,
            EnumerationDValue7    = 0x7,
            EnumerationDValue8    = 0x8,
            EnumerationDValue9    = 0x9,
            EnumerationDValue10   = 0xA,
            EnumerationDValue11   = 0xB,
            EnumerationDValue12   = 0xC,
            EnumerationDValue13   = 0xD,
            EnumerationDValue14   = 0xE,
            EnumerationDValue15   = 0xF,
        }

        protected enum Enumeration_E
        {
            EnumerationEValue0    = 0x0,
            EnumerationEValue1    = 0x1,
            EnumerationEValue2    = 0x0,
            EnumerationEValue3    = 0x1,
            EnumerationEValue4    = 0x2,
            EnumerationEValue5    = 0x3,
            EnumerationEValue6    = 0x4,
            EnumerationEValue7    = 0x5,
            EnumerationEValue8    = 0x6,
            EnumerationEValue9    = 0x7,
            EnumerationEValue10   = 0x0,
            EnumerationEValue11   = 0x1,
            EnumerationEValue12   = 0x2,
            EnumerationEValue13   = 0x3,
            EnumerationEValue14   = 0x0,
            EnumerationEValue15   = 0x1,
            EnumerationEValue16   = 0x2,
            EnumerationEValue17   = 0x3,
            EnumerationEValue18   = 0x4,
            EnumerationEValue19   = 0x5,
            EnumerationEValue20   = 0x6,
            EnumerationEValue21   = 0x7,
            EnumerationEValue22   = 0x8,
            EnumerationEValue23   = 0x9,
            EnumerationEValue24   = 0xA,
            EnumerationEValue25   = 0xB,
            EnumerationEValue26   = 0xC,
            EnumerationEValue27   = 0xD,
            EnumerationEValue28   = 0xE,
            EnumerationEValue29   = 0xF,
            EnumerationEValue30   = 0x0,
            EnumerationEValue31   = 0x1,
        }

        protected enum Enumeration_F
        {
            EnumerationFValue0    = 0,
            EnumerationFValue1    = 1,
            EnumerationFValue2    = 2,
            EnumerationFValue3    = 3,
            EnumerationFValue4    = 4,
            EnumerationFValue5    = 5,
            EnumerationFValue6    = 6,
            EnumerationFValue7    = 7,
            EnumerationFValue8    = 8,
            EnumerationFValue9    = 9,
            EnumerationFValue10   = 10,
            EnumerationFValue11   = 11,
            EnumerationFValue12   = 12,
            EnumerationFValue13   = 13,
            EnumerationFValue14   = 14,
            EnumerationFValue15   = 15,
            EnumerationFValue16   = 16,
            EnumerationFValue17   = 17,
            EnumerationFValue18   = 18,
            EnumerationFValue19   = 19,
            EnumerationFValue20   = 20,
            EnumerationFValue21   = 21,
            EnumerationFValue22   = 22,
            EnumerationFValue23   = 23,
            EnumerationFValue24   = 24,
            EnumerationFValue25   = 25,
            EnumerationFValue26   = 26,
            EnumerationFValue27   = 27,
            EnumerationFValue28   = 28,
            EnumerationFValue29   = 29,
            EnumerationFValue30   = 30,
            EnumerationFValue31   = 31,
        }

        protected enum Enumeration_G
        {
            EnumerationGValue0    = 0,
            EnumerationGValue1    = 1,
            EnumerationGValue2    = 2,
            EnumerationGValue3    = 3,
        }

        protected enum Enumeration_C
        {
            EnumerationCValue0    = 0,
            EnumerationCValue1    = 2,
            EnumerationCValue2    = 3,
            EnumerationCValue3    = 4,
            EnumerationCValue4    = 5,
            EnumerationCValue5    = 6,
        }

        protected enum Enumeration_B
        {
            EnumerationBValue0    = 0,
            EnumerationBValue1    = 1,
            EnumerationBValue2    = 2,
            EnumerationBValue3    = 3,
            EnumerationBValue4    = 4,
            EnumerationBValue5    = 5,
            EnumerationBValue6    = 6,
            EnumerationBValue7    = 7,
            EnumerationBValue8    = 8,
            EnumerationBValue9    = 9,
            EnumerationBValue10   = 10,
            EnumerationBValue11   = 11,
            EnumerationBValue12   = 12,
            EnumerationBValue13   = 13,
            EnumerationBValue14   = 14,
            EnumerationBValue15   = 15,
            EnumerationBValue16   = 16,
            EnumerationBValue17   = 17,
            EnumerationBValue18   = 18,
            EnumerationBValue19   = 19,
            EnumerationBValue20   = 20,
            EnumerationBValue21   = 21,
            EnumerationBValue22   = 22,
            EnumerationBValue23   = 23,
            EnumerationBValue24   = 24,
            EnumerationBValue25   = 25,
            EnumerationBValue26   = 26,
            EnumerationBValue27   = 27,
            EnumerationBValue28   = 28,
            EnumerationBValue29   = 29,
            EnumerationBValue30   = 30,
            EnumerationBValue31   = 31,
        }
    }
}