//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This peripheral contains 4 compare channels. Currently, we are supporting channel 0.
    public class NRF52840_RTC : ComparingTimer, IDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_RTC(Machine machine) : base(machine.ClockSource, InitialFrequency, eventEnabled: true)
        {
            IRQ = new GPIO();
            CompareReached += () =>
            {
                eventCompareEnabled.Value = true;
                UpdateInterrupts();
            };
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            registers.Reset();
            IRQ.Unset();
            base.Reset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.Start, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_START", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            Enabled = true;
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Stop, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STOP", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            Enabled = false;
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.CompareEventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareEnabled, name: "EVENTS_COMPARE[0]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlag(16, out eventCompareInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "COMPARE[0]")
                    .WithTaggedFlag("COMPARE[1]", 17)
                    .WithTaggedFlag("COMPARE[2]", 18)
                    .WithTaggedFlag("COMPARE[3]", 19)
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Register.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlag(16, name: "COMPARE[0]", writeCallback: (_,value) =>
                    {
                        if(value)
                        {
                            eventCompareInterruptEnabled.Value = false;
                            UpdateInterrupts();
                        }
                    })
                    .WithTaggedFlag("COMPARE[1]", 17)
                    .WithTaggedFlag("COMPARE[2]", 18)
                    .WithTaggedFlag("COMPARE[3]", 19)
                },
                {(long)Register.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 24, FieldMode.Read, name: "COUNTER", valueProviderCallback: _ =>
                    {
                        return (uint)Value;
                    })
                    .WithReservedBits(24, 8)
                },
                {(long)Register.Prescaler, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out prescaler, name: "PRESCALER", writeCallback: (_, value) =>
                    {
                        Divider = value + 1;
                    })
                    .WithReservedBits(12, 20)
                },
                {(long)Register.Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: "COMPARE", writeCallback: (_, value) =>
                    {
                        eventCompareEnabled.Value = false;
                        UpdateInterrupts();
                        Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)Compare;
                    })
                    .WithReservedBits(24, 8)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void UpdateInterrupts()
        {
            var eventCompareMask = eventCompareInterruptEnabled.Value && eventCompareEnabled.Value;
            IRQ.Set(eventCompareMask);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField eventCompareEnabled;
        private IFlagRegisterField eventCompareInterruptEnabled;
        private IValueRegisterField prescaler;

        private const int InitialFrequency = 32768;

        private enum Register : long
        {
            Start = 0x000,
            Stop = 0x004,
            CompareEventPending = 0x140,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            Counter = 0x504,
            Prescaler = 0x508,
            Compare = 0x540
        }
    }
}
