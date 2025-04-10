//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NRF54H20_GRTC : IDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF54H20_GRTC(IMachine machine, int numberOfEvents = MaxEventCount)
        {
            IRQ = new GPIO();

            if(numberOfEvents > MaxEventCount)
            {
                throw new ConstructionException($"Cannot create peripheral with {numberOfEvents} events, maximum number is {MaxEventCount}.");
            }
            this.numberOfEvents = numberOfEvents;
            compareReached = new IFlagRegisterField[numberOfEvents];

            compareTimers = new ComparingTimer[numberOfEvents];
            for(var i = 0u; i < compareTimers.Length; i++)
            {
                var j = i;
                compareTimers[i] = new ComparingTimer(machine.ClockSource, InitialFrequency, this, $"compare{j}",
                    limit: MaxValue52Bits, eventEnabled: true, compare: MaxValue52Bits);
                compareTimers[j].CompareReached += () =>
                {
                    this.Log(LogLevel.Noisy, "IRQ #{0} triggered!", j);
                    compareReached[j].Value = true;
                    UpdateInterrupts();
                };
            }

            // The activity of the syscounter is controlled through the CounterModeSelection register and is enabled by default.
            // It does not generate any interrupts and is used only to indicate passing time before the compare timers are started.
            sysCounter = new LimitTimer(machine.ClockSource, InitialFrequency, this, "tick",
                limit: MaxValue52Bits, direction: Time.Direction.Ascending, enabled: true, autoUpdate: true);

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

        public void Reset()
        {
            foreach(var timer in compareTimers)
            {
                timer.Reset();
            }
            sysCounter.Reset();
            registers.Reset();
            IRQ.Unset();
        }

        public event Action<uint> EventTriggered;

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.StartPWM, new DoubleWordRegister(this)
                    .WithTaggedFlag($"TASK_PWMSTART", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.StopPWM, new DoubleWordRegister(this)
                    .WithTaggedFlag($"TASK_PWMSTOP", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.EventSyscounterCompareSync, new DoubleWordRegister(this)
                    .WithTaggedFlag("EVENTS_RTCOMPARESYNC", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.EventPWMPeriodEnd, new DoubleWordRegister(this)
                    .WithTaggedFlag("EVENTS_PWMPERIODEND", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Shortcuts, new DoubleWordRegister(this)
                    .WithTag($"SHORTS", 0, 32)
                },
                {(long)Register.EnableDisableEventRouting, new DoubleWordRegister(this)
                    .WithReservedBits(0, 27)
                    .WithTaggedFlag("PWMPERIODEND", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)Register.EnableEventRouting, new DoubleWordRegister(this)
                    .WithReservedBits(0, 27)
                    .WithTaggedFlag("PWMPERIODEND", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)Register.DisableEventRouting, new DoubleWordRegister(this)
                    .WithReservedBits(0, 27)
                    .WithTaggedFlag("PWMPERIODEND", 27)
                    .WithReservedBits(28, 4)
                },
                {(long)Register.CounterModeSelection, new DoubleWordRegister(this, 0x1)
                    .WithFlag(0, out autoEnable, name: "AUTOEN")
                    .WithFlag(1, out enabled, name: "SYSCOUNTEREN")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) =>
                    {
                        sysCounter.Enabled = enabled.Value ? enabled.Value : autoEnable.Value;
                    })
                },
                {(long)Register.Timeout, new DoubleWordRegister(this)
                    .WithTag("VALUE", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Register.Interval,  new DoubleWordRegister(this)
                    .WithTag("VALUE", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Register.PWMConfiguration,  new DoubleWordRegister(this)
                    .WithTag("COMPAREVALUE", 0, 8)
                    .WithReservedBits(8, 24)
                },
                {(long)Register.ClockOutput, new DoubleWordRegister(this)
                    .WithTaggedFlag("CLKOUT32K", 0)
                    .WithTaggedFlag("CLKOUTFAST", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Register.ClockConfiguration, new DoubleWordRegister(this)
                    .WithTag("CLKFASTDIV", 0, 8)
                    .WithReservedBits(8, 8)
                    .WithTag("CLKSEL", 16, 2)
                    .WithReservedBits(18, 14)
                }
            };

            for(var i = 0; i < numberOfEvents; i++)
            {
                var j = i;
                registersMap.Add((long)Register.CaptureCounterValue + j * 0x4, new DoubleWordRegister(this)
                    .WithTaggedFlag($"TASK_CAPTURE[{j}]", 0)
                    .WithReservedBits(1, 31));

                registersMap.Add((long)Register.SubscribeForTaskCapture + j * 0x4, new DoubleWordRegister(this)
                    .WithTag($"CHIDX[{j}]", 0, 8)
                    .WithReservedBits(8, 23)
                    .WithTaggedFlag("EN", 31));

                registersMap.Add((long)Register.CompareEvent + j * 0x4, new DoubleWordRegister(this)
                    .WithFlag(0, out compareReached[j], name: $"EVENTS_COMPARE[{j}]")
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.PublishConfigurationForEventCompare + j * 0x4, new DoubleWordRegister(this)
                    .WithTag($"CHIDX[{j}]", 0, 8)
                    .WithReservedBits(8, 23)
                    .WithTaggedFlag("EN", 31));

                registersMap.Add((long)Register.CaptureCompareLow + j * 0x10, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"CCL[{j}]",
                        writeCallback: (_, value) =>
                        {
                            compareReached[j].Value = false;
                            UpdateInterrupts();
                            compareTimers[j].Compare = value;
                        },
                        valueProviderCallback: _ => (uint)compareTimers[j].Compare)
                );

                registersMap.Add((long)Register.CaptureCompareHigh + j * 0x10, new DoubleWordRegister(this)
                    .WithValueField(0, 20, name: $"CCH[{j}]",
                        writeCallback: (_, value) =>
                        {
                            compareReached[j].Value = false;
                            UpdateInterrupts();
                            compareTimers[j].Compare |= (value << 32);
                        },
                        valueProviderCallback: _ => ((uint)(compareTimers[j].Compare >> 32)) & 0xFFFFF)
                    .WithReservedBits(20, 12)
                );

                registersMap.Add((long)Register.CaptureCompareAdd + j * 0x10, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: $"CCADD[{j}]",
                        writeCallback: (_, value) =>
                        {
                            compareReached[j].Value = false;
                            UpdateInterrupts();
                            compareTimers[j].Compare += value;
                        },
                        valueProviderCallback: _ => (uint)compareTimers[j].Compare)
                    .WithTag("REFERENCE", 31 ,1)
                );

                registersMap.Add((long)Register.CaptureCompareConfig + j * 0x10, new DoubleWordRegister(this)
                    .WithFlag(0, name: $"CCEN[{j}]",
                        writeCallback: (_, value) => compareTimers[j].Enabled = value,
                        valueProviderCallback: _ => compareTimers[j].Enabled)
                    .WithReservedBits(1, 31)
                );

                registersMap.Add((long)Register.SyscounterLow + j * 0x10, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"SYSCOUNTERL[{j}]",
                        writeCallback: (_, value) => sysCounter.Value = value,
                        valueProviderCallback: _ => (uint)sysCounter.Value)
                );

                registersMap.Add((long)Register.SyscounterHigh + j * 0x10, new DoubleWordRegister(this)
                    .WithValueField(0, 20, name: $"SYSCOUNTERH[{j}]",
                        writeCallback: (_, value) => sysCounter.Value |= (value & 0xFFFFF) << 32,
                        valueProviderCallback: _ => ((uint)(sysCounter.Value >> 32)) & 0xFFFFF)
                    .WithReservedBits(20, 10)
                    .WithTag("SYSCOUNTER_BUSY", 30, 1)
                    .WithTag("SYSCOUNTER_OVERFLOW", 31, 1)
                );

                registersMap.Add((long)Register.SyscounterActive + j * 0x10, new DoubleWordRegister(this)
                    .WithFlag(0, name: $"SYSCOUNTER_ACTIVE[{j}]",
                        writeCallback: (_, value) => sysCounter.Enabled = value,
                        valueProviderCallback: _ => sysCounter.Enabled)
                    .WithReservedBits(1, 31)
                );
            }

            for(var i = 0; i < SyscountersCount; i++)
            {
                var j = i;
                registersMap.Add((long)Register.EnableOrDisableInterrupt0 + j * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, out compareInterruptEnabled, name: $"INTEN[{j}]",
                        writeCallback: (k, _, value) =>
                        {
                            // the documentation is not clear on when should the compare timers be enabled or disabled
                            // so we are enabling/disabling them with the respective interrupts
                            compareTimers[k].Enabled = value;
                            compareInterruptEnabled[k].Value = value;
                        },
                        valueProviderCallback: (k, _) => compareInterruptEnabled[k].Value)
                    .WithReservedBits(16, 9)
                    .WithTaggedFlag("RTCOMPARESYNC", 25)
                    .WithReservedBits(26, 1)
                    .WithTaggedFlag("PWMPERIOD", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.EnableInterrupt0 + j * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, name: $"INTENSET[{j}]",
                        writeCallback: (k, _, value) =>
                        {
                            // the documentation is not clear on when should the compare timers be enabled or disabled
                            // so we are enabling/disabling them with the respective interrupts
                            compareTimers[k].Enabled = value;
                            compareInterruptEnabled[k].Value |= value;
                        },
                        valueProviderCallback: (k, _) => compareInterruptEnabled[k].Value)
                    .WithReservedBits(16, 9)
                    .WithTaggedFlag("RTCOMPARESYNC", 25)
                    .WithReservedBits(26, 1)
                    .WithTaggedFlag("PWMPERIOD", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.DisableInterrupt0 + j * 0x10, new DoubleWordRegister(this)
                   .WithFlags(0, numberOfEvents, name: $"INTENCLR[{j}]",
                        writeCallback: (k, _, value) =>
                        {
                            // the documentation is not clear on when should the compare timers be enabled or disabled
                            // so we are enabling/disabling them with the respective interrupts
                            compareTimers[k].Enabled = !value;
                            compareInterruptEnabled[k].Value &= !value;
                        },
                        valueProviderCallback: (k, _) => compareInterruptEnabled[k].Value)
                    .WithReservedBits(16, 9)
                    .WithTaggedFlag("RTCOMPARESYNC", 25)
                    .WithReservedBits(26, 1)
                    .WithTaggedFlag("PWMPERIOD", 27)
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.PendingInterrupt0 + j * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, FieldMode.Read, name: $"INTPEND[{j}]",
                        valueProviderCallback: (k, _) => compareReached[k].Value)
                    .WithReservedBits(16, 9)
                    .WithTaggedFlag("RTCOMPARESYNC", 25)
                    .WithReservedBits(26, 1)
                    .WithTaggedFlag("PWMPERIOD", 27)
                    .WithReservedBits(28, 4)
                );
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void UpdateInterrupts()
        {
            var flag = false;
            for(var i = 0; i < numberOfEvents; i++)
            {
                var thisEventEnabledAndSet = compareInterruptEnabled[i].Value && compareReached[i].Value;
                if(thisEventEnabledAndSet)
                {
                   this.Log(LogLevel.Noisy, "Interrupt set by CC{0}.", i);
                }
                flag |= thisEventEnabledAndSet;
            }
            IRQ.Set(flag);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField[] compareInterruptEnabled;
        private IFlagRegisterField autoEnable;
        private IFlagRegisterField enabled;

        private readonly LimitTimer sysCounter;
        private readonly ComparingTimer[] compareTimers;
        private readonly IFlagRegisterField[] compareReached;
        private readonly int numberOfEvents;

        private const ulong MaxValue52Bits = 0xFFFFFFFFFFFFF;
        private const int InitialFrequency = 16000000;
        private const int MaxEventCount = 16;
        private const int SyscountersCount = 11;

        private enum Register : long
        {
            CaptureCounterValue = 0x0,

            StartPWM = 0x6C,
            StopPWM = 0x70,
            SubscribeForTaskCapture = 0x80,

            CompareEvent = 0x100,

            EventSyscounterCompareSync = 0x164,
            EventPWMPeriodEnd = 0x16C,
            PublishConfigurationForEventCompare = 0x180,
            Shortcuts = 0x200,

            EnableOrDisableInterrupt0 = 0x300,
            EnableInterrupt0 = 0x304,
            DisableInterrupt0 = 0x308,
            PendingInterrupt0 = 0x30C,

            EnableDisableEventRouting = 0x400,
            EnableEventRouting = 0x404,
            DisableEventRouting = 0x408,

            CounterModeSelection = 0x510,

            CaptureCompareLow = 0x520,
            CaptureCompareHigh = 0x524,
            CaptureCompareAdd = 0x528,
            CaptureCompareConfig = 0x52C,

            Timeout = 0x6A4,
            Interval = 0x6A8,

            PWMConfiguration = 0x710,
            ClockOutput = 0x714,
            ClockConfiguration = 0x718,

            SyscounterLow = 0x720,
            SyscounterHigh = 0x724,
            SyscounterActive = 0x728
        }
    }
}
