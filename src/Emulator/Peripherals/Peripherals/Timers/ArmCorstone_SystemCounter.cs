//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ArmCorstone_SystemCounter : IBusPeripheral, IHasMappedRegisters
    {
        public ArmCorstone_SystemCounter(IMachine machine, ulong frequency, bool counterScalingEnabled = true, bool clockSwitchingEnabled = true, bool overrideCounterEnabled = false)
        {
            this.machine = machine;
            this.counterScalingEnabled = counterScalingEnabled;
            this.clockSwitchingEnabled = clockSwitchingEnabled;
            this.overrideCounterEnabled = overrideCounterEnabled;
            ReadRegistersCollection = new DoubleWordRegisterCollection(this);
            ControlRegistersCollection = new DoubleWordRegisterCollection(this);
            readMapper = new RegisterMapper(typeof(ArmCorstone_SystemCounter), ReadRegion);
            controlMapper = new RegisterMapper(typeof(ArmCorstone_SystemCounter), ControlRegion);
            mapperProvider = new MapperProvider(null);

            counter = new InnerCounter(this, frequency);
            counter.LimitReached += HandleLimitReached;

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            counter.Reset();
            ControlRegistersCollection.Reset();
            ReadRegistersCollection.Reset();
            UpdateInterrupts();
        }

        [ConnectionRegion(ReadRegion)]
        public void WriteDoubleWordFromReadBase(long offset, uint value)
        {
            using(mapperProvider.WithCurrent(readMapper))
            {
                this.LogUnhandledWrite(offset, (ulong)value);
            }
        }

        [ConnectionRegion(ReadRegion)]
        public uint ReadDoubleWordFromReadBase(long offset)
        {
            using(mapperProvider.WithCurrent(readMapper))
            {
                return ReadRegistersCollection.Read(offset);
            }
        }

        [ConnectionRegion(ControlRegion)]
        public void WriteDoubleWordFromControlBase(long offset, uint value)
        {
            using(mapperProvider.WithCurrent(controlMapper))
            {
                ControlRegistersCollection.Write(offset, value);
            }
        }

        [ConnectionRegion(ControlRegion)]
        public uint ReadDoubleWordFromControlBase(long offset)
        {
            using(mapperProvider.WithCurrent(controlMapper))
            {
                return ControlRegistersCollection.Read(offset);
            }
        }

        public string OffsetToString(long offset) => mapperProvider.CurrentMapper?.ToString(offset) ?? "<undefined>";

        public ICounter Counter => counter;

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection ReadRegistersCollection { get; private set; }

        public DoubleWordRegisterCollection ControlRegistersCollection { get; private set; }

        public ClockSelection SelectedClock
        {
            get => selectedClock.Value;
            set
            {
                switch(value)
                {
                case ClockSelection.ReferenceClock:
                case ClockSelection.FastClock:
                    if(!clockSwitchingEnabled && value != selectedClock.Value)
                    {
                        throw new RecoverableException("Hardware counter clock switching is not enabled");
                    }
                    selectedClock.Value = value;
                    return;
                default:
                    throw new RecoverableException($"Invalid {nameof(SelectedClock)} value (0x{value:X})");
                }
            }
        }

        private void HandleLimitReached()
        {
            interrupt.Value = true;
            UpdateInterrupts();
        }

        private void UpdateCounterScaling(uint? counterScale0ChangedFrom = null, uint? counterScale1ChangedFrom = null)
        {
            if(scalingEnabled.Value)
            {
                switch(selectedClock.Value)
                {
                case ClockSelection.ReferenceClock:
                    if(!AssertScalingCanChange(counterScale0, counterScale0ChangedFrom, 0))
                    {
                        return;
                    }
                    counter.Step = (uint)counterScale0.Value;
                    return;
                case ClockSelection.FastClock:
                    if(!AssertScalingCanChange(counterScale1, counterScale1ChangedFrom, 1))
                    {
                        return;
                    }
                    counter.Step = (uint)counterScale1.Value;
                    return;
                default:
                    throw new UnreachableException();
                }
            }
            else
            {
                counter.Step = DefaultCounterScaling;
            }
        }

        private bool AssertScalingCanChange(IValueRegisterField counterScale, uint? changedFrom, int index)
        {
            if(!counterScalingEnabled)
            {
                this.WarningLog("Attempted to change scaling value {0}, but counter scaling is disabled", index);
                counterScale.Value = DefaultCounterScaling;
                return false;
            }
            if(changedFrom.HasValue && counter.Enabled)
            {
                if(!overrideCounterEnabled)
                {
                    this.WarningLog("Changing scaling value {0} while counter is enabled is disabled", index);
                    counterScale.Value = changedFrom.Value;
                    return false;
                }
                this.WarningLog("Changing scaling value {0} while counter is enabled causes counter value to become unknown", index);
            }
            return true;
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interrupt.Value && interruptMask.Value);
        }

        private void DefineRegisters()
        {
            ControlRegisters.CounterControl.Define(ControlRegistersCollection)
                .WithFlag(0, name: "EN",
                    valueProviderCallback: _ => counter.Enabled,
                    writeCallback: (_, value) => counter.Enabled = value
                )
                .WithTaggedFlag("HDBG", 1)
                .WithFlag(2, out scalingEnabled, name: "SCEN")
                .WithFlag(3, out interruptMask, name: "INTRMASK")
                .WithTaggedFlag("PSLVERRDIS", 4)
                .WithFlag(5, out interrupt, FieldMode.Read | FieldMode.WriteZeroToClear, name: "INTRCLR")
                .WithReservedBits(6, 26)
                .WithChangeCallback((_, __) =>
                {
                    UpdateCounterScaling();
                    UpdateInterrupts();
                })
            ;

            ControlRegisters.CounterStatus.Define(ControlRegistersCollection)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("DBGH", 1)
                .WithReservedBits(2, 30)
            ;

            new DoubleWordRegister(this)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.CounterCountValueLow)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.CounterCountValue)
                .WithValueField(0, 32, name: "CountValue",
                    valueProviderCallback: _ => counter.ValueLow,
                    writeCallback: (_, value) => counter.ValueLow = (uint)value
                )
            ;

            ReadRegisters.CounterCountValueHigh.Define(ReadRegistersCollection)
                .WithValueField(0, 32, FieldMode.Read, name: "CountValue",
                    valueProviderCallback: _ => counter.ValueHigh
                )
            ;

            new DoubleWordRegister(this, DefaultCounterScaling)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.CounterCounterScale)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.CounterScale0)
                .WithValueField(0, 32, out counterScale0, name: "ScaleVal")
                .WithChangeCallback((oldValue, _) => UpdateCounterScaling(counterScale0ChangedFrom: oldValue))
            ;

            ControlRegisters.CounterId.Define(ControlRegistersCollection, 0x00030001)
                .WithValueField(0, 4, FieldMode.Read, name: "CNTSC",
                    valueProviderCallback: _ => counterScalingEnabled ? 0b0001UL : 0b0000UL
                )
                .WithReservedBits(4, 12)
                .WithFlag(16, FieldMode.Read, name: "CNTCS",
                    valueProviderCallback: _ => clockSwitchingEnabled
                )
                .WithEnumField(17, 2, out selectedClock, FieldMode.Read, name: "CNTSELCLK")
                .WithFlag(19, FieldMode.Read, name: "CNTSCR_OVR",
                    valueProviderCallback: _ => overrideCounterEnabled
                )
                .WithReservedBits(20, 12)
            ;

            ControlRegisters.CounterScale1.Define(ControlRegistersCollection, DefaultCounterScaling)
                .WithValueField(0, 32, out counterScale1, name: "ScaleVal")
                .WithChangeCallback((oldValue, _) => UpdateCounterScaling(counterScale1ChangedFrom: oldValue))
            ;

            new DoubleWordRegister(this, 0x00000004)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.PeripheralIdentification4)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.PeripheralIdentification4)
                .WithReservedBits(0, 32)
            ;

            ControlRegisters.PeripheralIdentification0.Define(ControlRegistersCollection, 0x000000BA)
                .WithReservedBits(0, 32)
            ;

            ReadRegisters.PeripheralIdentification0.Define(ReadRegistersCollection, 0x000000BB)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x000000B0)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.PeripheralIdentification1)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.PeripheralIdentification1)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x0000000B)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.PeripheralIdentification2)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.PeripheralIdentification2)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x00000000)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.PeripheralIdentification3)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.PeripheralIdentification3)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x0000000D)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.ComponentIdentification0)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.ComponentIdentification0)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x000000F0)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.ComponentIdentification1)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.ComponentIdentification1)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x00000005)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.ComponentIdentification2)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.ComponentIdentification2)
                .WithReservedBits(0, 32)
            ;

            new DoubleWordRegister(this, 0x000000B1)
                .DefinedFor(ReadRegistersCollection, ReadRegisters.ComponentIdentification3)
                .DefinedFor(ControlRegistersCollection, ControlRegisters.ComponentIdentification3)
                .WithReservedBits(0, 32)
            ;
        }

        private IFlagRegisterField scalingEnabled;
        private IFlagRegisterField interruptMask;
        private IFlagRegisterField interrupt;
        private IEnumRegisterField<ClockSelection> selectedClock;
        private IValueRegisterField counterScale0;
        private IValueRegisterField counterScale1;

        private readonly bool counterScalingEnabled;
        private readonly bool clockSwitchingEnabled;
        private readonly bool overrideCounterEnabled;

        private readonly RegisterMapper controlMapper;
        private readonly RegisterMapper readMapper;
        private readonly MapperProvider mapperProvider;
        private readonly InnerCounter counter;
        private readonly IMachine machine;

        private const uint DefaultCounterScaling = 0x01000000;
        private const string ReadRegion = "read";
        private const string ControlRegion = "control";

        public enum ClockSelection
        {
            ReferenceClock = 0b01,
            FastClock = 0b10,
        }

        [RegisterMapper.RegistersDescription(ReadRegion)]
        public enum ReadRegisters
        {
            CounterCountValueLow      = 0x000, // CNTCV
            CounterCountValueHigh     = 0x004, // CNTCV
            PeripheralIdentification4 = 0xFD0, // CNTPIDR4
            PeripheralIdentification0 = 0xFE0, // CNTPIDR0
            PeripheralIdentification1 = 0xFE4, // CNTPIDR1
            PeripheralIdentification2 = 0xFE8, // CNTPIDR2
            PeripheralIdentification3 = 0xFEC, // CNTPIDR3
            ComponentIdentification0  = 0xFF0, // CNTCIDR0
            ComponentIdentification1  = 0xFF4, // CNTCIDR1
            ComponentIdentification2  = 0xFF8, // CNTCIDR2
            ComponentIdentification3  = 0xFFC, // CNTCIDR3
        }

        [RegisterMapper.RegistersDescription(ControlRegion)]
        public enum ControlRegisters
        {
            CounterControl            = 0x000, // CNTCR
            CounterStatus             = 0x004, // CNTSR
            CounterCountValue         = 0x008, // CNTCV
            CounterCounterScale       = 0x010, // CNTSCR
            CounterId                 = 0x01C, // CNTID
            CounterScale0             = 0x0D0, // CNTSR0
            CounterScale1             = 0x0D4, // CNTSR1
            PeripheralIdentification4 = 0xFD0, // CNTPIDR4
            PeripheralIdentification0 = 0xFE0, // CNTPIDR0
            PeripheralIdentification1 = 0xFE4, // CNTPDR1
            PeripheralIdentification2 = 0xFE8, // CNTPIDR2
            PeripheralIdentification3 = 0xFEC, // CNTPIDR3
            ComponentIdentification0  = 0xFF0, // CNTCIDR0
            ComponentIdentification1  = 0xFF4, // CNTCIDR1
            ComponentIdentification2  = 0xFF8, // CNTCIDR2
            ComponentIdentification3  = 0xFFC, // CNTCIDR3
        }

        public interface ICounter
        {
            void RegisterComparePoint(Action handle, IEmulationElement parent, string localName);

            // Sets value for compare point and activates it
            void SetComparePoint(Action handle, ulong compareValue);

            void DeactivateComparePoint(Action handle);

            ulong Value { get; }

            uint ValueLow { get; }

            uint ValueHigh { get; }

            event Action<ulong> ValueLoaded;

            event Action ValueOverflown;
        }

        private class InnerCounter : ICounter
        {
            // The counter consists of 88 bits, 64 bits are public and 24 are internal.
            // For this purpose the custom implementation is chosen instead of use of Timer helpers.
            // LowerBits + FractionalBits are handled by clock entry, but UpperBits are handled
            // in this class.
            public InnerCounter(ArmCorstone_SystemCounter parent, ulong frequency)
            {
                this.frequency = frequency;
                this.parent = parent;
                clockSource = parent.machine.ClockSource;

                Reset();
            }

            public void Reset()
            {
                var entry = new ClockEntry(timerLimit, frequency, HandleLimitReached, parent, "counter", enabled: false, step: DefaultCounterScaling);
                clockSource.ExchangeClockEntryWith(HandleLimitReached, x => entry, () => entry);
                UpdateAllComparePoints(entry);
            }

            public void RegisterComparePoint(Action handle, IEmulationElement parent, string localName)
            {
                comparePoints[handle] = new ComparePoint(handle, parent, localName);
            }

            public void SetComparePoint(Action handle, ulong compareValue)
            {
                var comparePoint = comparePoints[handle];
                if(comparePoint.Active && comparePoint.Value == compareValue)
                {
                    return;
                }
                comparePoint.Value = compareValue;

                var counterEntry = clockSource.GetClockEntry(HandleLimitReached);
                // No pre-access synchronization is needed. Blind writes do not depend on the
                // current value, and writes that do are preceded by a read, keeping
                // the synchronized value up to date.
                if(!IsWithinCurrentTimerLimit(compareValue, counterEntry.Value) || !counterEntry.Enabled)
                {
                    DeactivateComparePoint(comparePoint);
                    return;
                }

                ActivateComparePoint(comparePoint, counterEntry);
                TryRequestReturn();
            }

            public void DeactivateComparePoint(Action handle)
            {
                DeactivateComparePoint(comparePoints[handle]);
            }

            public bool Enabled
            {
                get => clockSource.GetClockEntry(HandleLimitReached).Enabled;
                set => SetForClockEntry(enabled: value);
            }

            public uint Step
            {
                set => SetForClockEntry(step: value);
            }

            public ulong Value
            {
                get
                {
                    var lowerValue = ValueLow; // also syncs time
                    return (upperValue << LowerBits) | lowerValue;
                }
            }

            public uint ValueLow
            {
                get
                {
                    TrySyncTime();
                    // assumes LowerBits >= 32
                    return (uint)(clockSource.GetClockEntry(HandleLimitReached).Value >> FractionalBits);
                }

                set => SetForClockEntry(valueLow: value);
            }

            public uint ValueHigh
            {
                get
                {
                    TrySyncTime();
                    var lowerValue = clockSource.GetClockEntry(HandleLimitReached).Value >> FractionalBits;
                    // assumes UpperBits <= 32 <= LowerBits
                    return (uint)((upperValue << (32 - UpperBits)) | (lowerValue >> (LowerBits - 32)));
                }
            }

            public event Action LimitReached;

            public event Action<ulong> ValueLoaded;

            public event Action ValueOverflown;

            private bool IsWithinCurrentTimerLimit(ulong value, ulong? currentClockEntryValue = null)
            {
                if(value < (upperValue << LowerBits) || value >= ((upperValue + 1) << LowerBits))
                {
                    return false;
                }

                value &= lowerValueMask;
                var lowerValue = (currentClockEntryValue ?? clockSource.GetClockEntry(HandleLimitReached).Value) >> FractionalBits;
                return value > lowerValue;
            }

            private void DeactivateComparePoint(ComparePoint comparePoint, ClockEntry? counterEntry = null)
            {
                if(comparePoint.Active)
                {
                    comparePoint.Active = false;
                    clockSource.ExchangeClockEntryWith(comparePoint.HandleComparePointReached,
                        entry =>
                        {
                            TryRequestReturn();
                            return entry.With(enabled: false);
                        },
                        () => comparePoint.CreateClockEntry(counterEntry ?? clockSource.GetClockEntry(HandleLimitReached))
                    );
                }
            }

            private void ActivateComparePoint(ComparePoint comparePoint, ClockEntry counterEntry)
            {
                comparePoint.Active = true;
                UpdateComparePoint(comparePoint, counterEntry);
            }

            private void UpdateComparePoint(ComparePoint comparePoint, ClockEntry counterEntry)
            {
                var entry = comparePoint.CreateClockEntry(counterEntry);
                clockSource.ExchangeClockEntryWith(comparePoint.HandleComparePointReached, _ => entry, () => entry);
            }

            private void UpdateAllComparePoints(ClockEntry counterEntry)
            {
                foreach(var comparePoint in comparePoints.Values)
                {
                    if(!IsWithinCurrentTimerLimit(comparePoint.Value, counterEntry.Value) || !counterEntry.Enabled)
                    {
                        DeactivateComparePoint(comparePoint, counterEntry);
                        continue;
                    }
                    ActivateComparePoint(comparePoint, counterEntry);
                }
            }

            private void SetForClockEntry(uint? valueLow = null, bool? enabled = null, uint? step = null)
            {
                var newValue = (ulong?)null;
                var counterEntry = (ClockEntry?)null;
                clockSource.ExchangeClockEntryWith(HandleLimitReached,
                    entry =>
                    {
                        if(valueLow.HasValue)
                        {
                            // assumes LowerBits >= 32
                            newValue = BitHelper.SetBitsFrom(entry.Value, valueLow.Value, FractionalBits, LowerBits);
                            if(entry.Value == newValue.Value)
                            {
                                return entry;
                            }

                            counterEntry = entry.With(value: newValue.Value);
                        }
                        else if(enabled.HasValue)
                        {
                            if(entry.Enabled == enabled.Value)
                            {
                                return entry;
                            }

                            counterEntry = entry.With(enabled: enabled.Value);
                        }
                        else if(step.HasValue)
                        {
                            if(entry.Step == step.Value)
                            {
                                return entry;
                            }

                            counterEntry = entry.With(step: step.Value);
                        }

                        TryRequestReturn();
                        return counterEntry.Value;
                    },
                    () => throw new UnreachableException()
                );

                if(counterEntry.HasValue)
                {
                    if(newValue.HasValue)
                    {
                        // after value change update all entries
                        UpdateAllComparePoints(counterEntry.Value);
                        ValueLoaded?.Invoke(newValue.Value);
                        return;
                    }

                    // if value doesn't change no need to update non-active entries
                    foreach(var point in comparePoints.Values)
                    {
                        if(point.Active)
                        {
                            UpdateComparePoint(point, counterEntry.Value);
                        }
                    }
                }
            }

            private void HandleLimitReached()
            {
                if(upperValue == upperValueLimit)
                {
                    upperValue = 0;
                    LimitReached?.Invoke();
                    ValueOverflown?.Invoke();
                }
                else
                {
                    upperValue += 1;
                }
                var counterEntry = clockSource.GetClockEntry(HandleLimitReached);
                UpdateAllComparePoints(counterEntry);
            }

            private void TrySyncTime()
            {
                if(parent.machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu?.SyncTime();
                }
            }

            private void TryRequestReturn()
            {
                if(parent.machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    (cpu as IControllableCPU)?.RequestReturn();
                }
            }

            private static readonly ulong timerLimit = BitHelper.CalculateQuadWordMask(FractionalBits + LowerBits, 0);
            private static readonly ulong upperValueLimit = BitHelper.CalculateQuadWordMask(UpperBits, 0);
            private static readonly ulong lowerValueMask = BitHelper.CalculateQuadWordMask(LowerBits, 0);

            private ulong upperValue;

            private readonly ulong frequency;

            private readonly Dictionary<Action, ComparePoint> comparePoints = new Dictionary<Action, ComparePoint>();
            private readonly ArmCorstone_SystemCounter parent;
            private readonly IClockSource clockSource;

            // code assumes LowerBits + UpperBits <= 64
            private const int ValueBits = LowerBits + UpperBits;
            // code assumes UpperBits <= 32 <= LowerBits
            private const int UpperBits = 24;
            private const int LowerBits = 40;
            // code assumes FractionalBits + LowerBits <= 64
            private const int FractionalBits = 24;

            private class ComparePoint
            {
                public ComparePoint(Action handle, IEmulationElement parent, string localName)
                {
                    this.handle = handle;
                    Parent = parent;
                    Name = localName;
                }

                public void HandleComparePointReached()
                {
                    Active = false;
                    handle?.Invoke();
                }

                public ClockEntry CreateClockEntry(ClockEntry main)
                {
                    var entry = new ClockEntry(
                        (Value & lowerValueMask) << FractionalBits,
                        main.Frequency,
                        HandleComparePointReached,
                        Parent,
                        Name,
                        main.Enabled && Active,
                        main.Direction,
                        WorkMode.OneShot,
                        main.Step
                    );

                    entry.Value = main.Value;
                    entry.ValueResiduum = main.ValueResiduum;

                    return entry;
                }

                public override string ToString() => Misc.ToDebugString(this);

                public bool Active { get; set; }

                public ulong Value { get; set; }

                public IEmulationElement Parent { get; }

                public string Name { get; }

                private readonly Action handle;
            }
        }
    }
}
