//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ARM_GlobalTimer : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize
    {
        public ARM_GlobalTimer(IMachine machine, long frequency, IARMCPUsConnectionsProvider irqController)
            : base(machine)
        {
            BuildRegisters();
            globalTimer = new LimitTimer(machine.ClockSource, frequency, this, "coreTimer", direction: Direction.Ascending);
            connections = new Dictionary<int, IGPIO>();
            comparators = new Dictionary<ICPU, PrivateComparator>();
            lock(locker)
            {
                irqController.CPUAttached += AddCPU;
                foreach(var cpu in irqController.AttachedCPUs)
                {
                    AddCPU(cpu);
                }
            }
        }

        public long Size => 0x100;

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
            get
            {
                lock(locker)
                {
                    connectionsLocked = true;
                    return connections;
                }
            }
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                foreach(var cmp in comparators.Values)
                {
                    cmp.Reset();
                }
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            lock(locker)
            {
                return base.ReadDoubleWord(offset);
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            lock(locker)
            {
                base.WriteDoubleWord(offset, value);
            }
        }

        public uint ReadDoubleWord(long offset, ICPU cpu)
        {
            lock(locker)
            {
                providedCpu = cpu;
                var value = base.ReadDoubleWord(offset);
                providedCpu = null;
                return value;
            }
        }

        public void WriteDoubleWord(long offset, uint value, ICPU cpu)
        {
            lock(locker)
            {
                providedCpu = cpu;
                base.WriteDoubleWord(offset, value);
                providedCpu = null;
            }
        }

        private void BuildRegisters()
        {
            Registers.CounterLow.Define(this)
                .WithValueField(0, 32, name: "Counter [0:31]",
                    writeCallback: (_, value) => CounterLow = value,
                    valueProviderCallback: (_) => CounterLow
                )
            ;
            Registers.CounterHigh.Define(this)
                .WithValueField(0, 32, name: "Counter [32:63]",
                    writeCallback: (_, value) => CounterHigh = value,
                    valueProviderCallback: (_) => CounterHigh
                )
            ;
            Registers.Control.Define(this)
                .WithFlag(0, name: "Timer enable",
                    writeCallback: (_, value) => Enabled = value,
                    valueProviderCallback: (_) => Enabled
                )
                .WithFlag(1, name: "Comp enable",
                    writeCallback: (_, value) => GetComparator().ComparatorEnabled = value,
                    valueProviderCallback: (_) => GetComparator().ComparatorEnabled
                )
                .WithFlag(2, name: "IRQ enable",
                    writeCallback: (_, value) => GetComparator().IrqEnabled = value,
                    valueProviderCallback: (_) => GetComparator().IrqEnabled
                )
                .WithFlag(3, name: "Auto-increment",
                    writeCallback: (_, value) => GetComparator().AutoIncrementEnabled = value,
                    valueProviderCallback: (_) => GetComparator().AutoIncrementEnabled
                )
                .WithReservedBits(4, 4)
                .WithValueField(8, 8, name: "Prescaler",
                    writeCallback: (_, value) => Prescaler = value,
                    valueProviderCallback: (_) => Prescaler
                )
                .WithReservedBits(16, 16)
            ;
            Registers.InterruptStatus.Define(this)
                .WithFlag(0, name: "Event",
                    writeCallback: (_, value) => GetComparator().EventFlag = value,
                    valueProviderCallback: (_) => GetComparator().EventFlag
                )
                .WithReservedBits(1, 31)
            ;
            Registers.CompareValueLow.Define(this)
                .WithValueField(0, 32, name: "Comparator Value [0:31]",
                    writeCallback: (_, value) => GetComparator().CompareValueLow = value,
                    valueProviderCallback: (_) => GetComparator().CompareValueLow
                )
            ;
            Registers.CompareValueHigh.Define(this)
                .WithValueField(0, 32, name: "Comparator Value [32:63]",
                    writeCallback: (_, value) => GetComparator().CompareValueHigh = value,
                    valueProviderCallback: (_) => GetComparator().CompareValueHigh
                )
            ;
            Registers.AutoIncrement.Define(this)
                .WithValueField(0, 32, name: "Auto-increment",
                    writeCallback: (_, value) => GetComparator().AutoIncrement = value,
                    valueProviderCallback: (_) => GetComparator().AutoIncrement
                )
            ;
        }

        private void AddCPU(ICPU cpu)
        {
            lock(locker)
            {
                if(connectionsLocked)
                {
                    throw new RecoverableException($"CPU (connection #{cpu.MultiprocessingId}) attached to IRQ Controller after Global Timer's GPIO initialization");
                }
                var comparator = new PrivateComparator(machine.ClockSource, globalTimer, this, $"{cpu.MultiprocessingId}");
                connections.Add((int)cpu.MultiprocessingId, comparator.IRQ);
                comparators.Add(cpu, comparator);
            }
        }

        private PrivateComparator GetComparator(ICPU cpu = null)
        {
            cpu = cpu ?? providedCpu;
            if(cpu == null && !sysbus.TryGetCurrentCPU(out cpu))
            {
                throw new RecoverableException("Attempted to access a core specific feature, but no CPU is selected nor detected");
            }
            if(comparators.TryGetValue(cpu, out var cmp))
            {
                return cmp;
            }
            throw new RecoverableException($"Detected CPU {machine.GetLocalName(cpu)} is not connected to this peripheral");
        }

        private ulong CounterLow
        {
            get => (uint)Counter;
            set => Counter = BitHelper.SetMaskedValue(Counter, value, 0, 32);
        }

        private ulong CounterHigh
        {
            get => (uint)(Counter >> 32);
            set => Counter = BitHelper.SetMaskedValue(Counter, value, 32, 32);
        }

        private ulong Counter
        {
            get
            {
                var cpu = providedCpu;
                if(cpu != null || sysbus.TryGetCurrentCPU(out cpu))
                {
                    cpu.SyncTime();
                }
                return globalTimer.Value;
            }
            set
            {
                if(globalTimer.Value == value)
                {
                    return;
                }
                globalTimer.Value = value;
                foreach(var cmp in comparators.Values)
                {
                    cmp.Value = value;
                }
            }
        }

        private bool Enabled
        {
            get => globalTimer.Enabled;
            set
            {
                if(globalTimer.Enabled == value)
                {
                    return;
                }
                globalTimer.Enabled = value;
                foreach(var cmp in comparators.Values)
                {
                    cmp.Enabled = value;
                }
            }
        }

        private ulong Prescaler
        {
            get => (ulong)globalTimer.Divider;
            set
            {
                if((ulong)globalTimer.Divider == value + 1)
                {
                    return;
                }
                globalTimer.Divider = (int)value + 1;
                foreach(var cmp in comparators.Values)
                {
                    cmp.Divider = (uint)value + 1;
                }
            }
        }

        private bool connectionsLocked;
        private ICPU providedCpu;
        private readonly LimitTimer globalTimer;
        private readonly Dictionary<ICPU, PrivateComparator> comparators;
        private readonly Dictionary<int, IGPIO> connections;
        private readonly object locker = new Object();

        public enum Registers
        {
            CounterLow       = 0x00,
            CounterHigh      = 0x04,
            Control          = 0x08,
            InterruptStatus  = 0x0C,
            CompareValueLow  = 0x10,
            CompareValueHigh = 0x14,
            AutoIncrement    = 0x18,
        }

        private class PrivateComparator
        {
            public PrivateComparator(IClockSource clockSource, LimitTimer coreTimer, IPeripheral owner, string coreName)
            {
                innerTimer = new ComparingTimer(clockSource, coreTimer.Frequency, owner, $"compareTimer-{coreName}", direction: Direction.Ascending, compare: 0, workMode: WorkMode.Periodic);
                innerTimer.Value = coreTimer.Value;
                innerTimer.Enabled = coreTimer.Enabled;
                innerTimer.Divider = (uint)coreTimer.Divider;
                innerTimer.CompareReached += HandleCompareEvent;
                IRQ = new GPIO();
            }

            public void Reset()
            {
                innerTimer.Reset();
                eventFlag = false;
                irqEnabled = false;
                UpdateInterrupt();
            }

            public IGPIO IRQ { get; }

            public ulong CompareValueLow
            {
                get => (uint)innerTimer.Compare;
                set
                {
                    innerTimer.Compare = BitHelper.SetMaskedValue(innerTimer.Compare, value, 0, 32);
                    UpdateEventFlag();
                    UpdateInterrupt();
                }
            }

            public ulong CompareValueHigh
            {
                get => (uint)(innerTimer.Compare >> 32);
                set
                {
                    innerTimer.Compare = BitHelper.SetMaskedValue(innerTimer.Compare, value, 32, 32);
                    UpdateEventFlag();
                    UpdateInterrupt();
                }
            }

            public ulong AutoIncrement { get; set; }

            public bool EventFlag
            {
                get => eventFlag;
                set
                {
                    if(!value)
                    {
                        return;
                    }
                    eventFlag = false;
                    UpdateEventFlag();
                    UpdateInterrupt();
                }
            }

            public bool Enabled
            {
                set => innerTimer.Enabled = value;
            }

            public bool ComparatorEnabled
            {
                get => innerTimer.EventEnabled;
                set
                {
                    innerTimer.EventEnabled = value;
                    UpdateEventFlag();
                    UpdateInterrupt();
                }
            }

            public bool IrqEnabled
            {
                get => irqEnabled;
                set
                {
                    if(irqEnabled == value)
                    {
                        return;
                    }
                    irqEnabled = value;
                    UpdateInterrupt();
                }
            }

            public bool AutoIncrementEnabled { get; set; }

            public ulong Value
            {
                set
                {
                    innerTimer.Value = value;
                    UpdateEventFlag();
                    UpdateInterrupt();
                }
            }

            public uint Divider
            {
                set => innerTimer.Divider = value;
            }

            private void HandleCompareEvent()
            {
                eventFlag = true;
                if(AutoIncrementEnabled)
                {
                    innerTimer.Compare += AutoIncrement;
                }
                UpdateInterrupt();
            }

            private void UpdateEventFlag()
            {
                eventFlag |= ComparatorEnabled && innerTimer.Value >= innerTimer.Compare;
            }

            private void UpdateInterrupt()
            {
                IRQ.Set(IrqEnabled && EventFlag);
            }

            private bool eventFlag;
            private bool irqEnabled;
            private readonly ComparingTimer innerTimer;
        }
    }
}
