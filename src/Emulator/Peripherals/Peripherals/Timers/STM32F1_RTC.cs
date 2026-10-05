// Made from scratch by Gissio (C)-2025:
// * Implements STM32F1 RTC peripheral
// * Second and alarm interrupts
// * Prescaler handling

using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32F1_RTC : IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public STM32F1_RTC(IMachine machine, long frequency = DefaultFrequency) : base()
        {
            IRQ = new GPIO();

            eventTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(eventTimer),
                1, direction: Direction.Ascending, enabled: true, eventEnabled: true, divider: 0x4000);
            eventTimer.LimitReached += UpdateTimer;

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ControlHigh, new DoubleWordRegister(this)
                    .WithFlag(0, out secondsInterruptEnable, FieldMode.Read | FieldMode.Write, name: "SECIE")
                    .WithFlag(1, out alarmInterruptEnable, FieldMode.Read | FieldMode.Write, name: "ALRIE")
                    .WithFlag(2, name: "OWIE")
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.ControlLow, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear,
                        writeCallback: (_, value) =>
                        {
                            secondsInterruptFlag = value;
                            UpdateInterruptFlags();
                        }, valueProviderCallback: _ => secondsInterruptFlag, name: "SECF")
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear,
                        writeCallback: (_, value) =>
                        {
                            alarmInterruptFlag = false;
                            UpdateInterruptFlags();
                        }, valueProviderCallback: _ => alarmInterruptFlag, name: "ALRF")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "OWF")
                    .WithFlag(3, FieldMode.Read,
                        valueProviderCallback: _ => true, name: "RSF")
                    .WithFlag(4, name: "CNF")
                    .WithFlag(5, FieldMode.Read,
                        valueProviderCallback: _ => true, name: "RTOFF")
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.PrescalerLoadHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 4,
                        writeCallback: (_, value) =>
                        {
                            prescalerDivider = (prescalerDivider & 0x0ffff) | ((uint)value << 16);
                            eventTimer.Divider = (int)(prescalerDivider + 1);
                        }, valueProviderCallback: _ => ((prescalerDivider & 0xf0000) >> 16), name: "PRLH")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.PrescalerLoadLow, new DoubleWordRegister(this, 0x8000)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            prescalerDivider = (prescalerDivider & 0xf0000) | ((uint)value << 0);
                            eventTimer.Divider = (int)(prescalerDivider + 1);
                        },
                        valueProviderCallback: _ => (prescalerDivider & 0x0ffff) >> 0, name: "PRLL")
                },
                {(long)Registers.PrescalerDividerHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 4,
                        writeCallback: (_, value) =>
                        {
                            prescalerCounter = (prescalerCounter & 0x0ffff) | ((uint)value << 16);
                        },
                        valueProviderCallback: _ => (prescalerCounter & 0xf0000) >> 16, name: "DIVH")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.PrescalerDividerLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            prescalerCounter = (prescalerCounter & 0xf0000) | ((uint)value << 0);
                        },
                        valueProviderCallback: _ => (prescalerCounter & 0x0ffff) >> 0, name: "DIVL")
                },
                {(long)Registers.CounterHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            counter = (counter & 0x0000ffff) | ((uint)value << 16);
                        },
                        valueProviderCallback: _ => (counter & 0xffff0000) >> 16, name: "CNTH")
                },
                {(long)Registers.CounterLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            counter = (counter & 0xffff0000) | ((uint)value << 0);
                        },
                        valueProviderCallback: _ => (counter & 0x0000ffff) >> 0, name: "CNTL")
                },
                {(long)Registers.AlarmHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            alarm = (alarm & 0x0000ffff) | ((uint)value << 16);
                        },
                        valueProviderCallback: _ => (alarm & 0xffff0000) >> 16, name: "ALRH")
                },
                {(long)Registers.AlarmLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) =>
                        {
                            alarm = (alarm & 0xffff0000) | ((uint)value << 0);
                        },
                        valueProviderCallback: _ => (alarm & 0x0000ffff) >> 0, name: "ALRL")
                },
            };

            RegistersCollection = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            eventTimer.Reset();
            IRQ.Unset();
        }

        private void UpdateTimer()
        {
            counter++;
            secondsInterruptFlag = true;

            if (counter == alarm)
            {
                alarmInterruptFlag = true;
            }

            UpdateInterruptFlags();
        }

        private void UpdateInterruptFlags()
        {
            var state = false;

            state |= secondsInterruptFlag && secondsInterruptEnable.Value;
            state |= alarmInterruptFlag && alarmInterruptEnable.Value;

            IRQ.Set(state);
        }

        public long Size => 0x400;

        private const long DefaultFrequency = 32768;
        private const int DefaultPrescalerDivider = 0x8000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private readonly LimitTimer eventTimer;

        public GPIO IRQ { get; }

        private IFlagRegisterField secondsInterruptEnable;
        private bool secondsInterruptFlag;
        private IFlagRegisterField alarmInterruptEnable;
        private bool alarmInterruptFlag;
        private uint prescalerDivider;
        private uint prescalerCounter;
        private uint counter;
        private uint alarm;

        private enum Registers
        {
            ControlHigh = 0x0,
            ControlLow = 0x4,
            PrescalerLoadHigh = 0x8,
            PrescalerLoadLow = 0xC,
            PrescalerDividerHigh = 0x10,
            PrescalerDividerLow = 0x14,
            CounterHigh = 0x18,
            CounterLow = 0x1C,
            AlarmHigh = 0x20,
            AlarmLow = 0x24,
        }
    }
}
