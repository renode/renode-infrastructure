//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MPFS_Timer : IDoubleWordPeripheral, IKnownSize
    {
        public MPFS_Timer(IMachine machine, long frequency = 100000000)
        {
            Timer1IRQ = new GPIO();
            Timer2IRQ = new GPIO();

            timerInterruptEnable = new IFlagRegisterField[NumberOfInternalTimers];
            rawInterruptStatus = new IFlagRegisterField[NumberOfInternalTimers];
            backgroundLoadValue = new ulong[NumberOfInternalTimers];
            backgroundLoadValueIsValid = new bool[NumberOfInternalTimers];

            timer = new LimitTimer[NumberOfInternalTimers]
            {
                new LimitTimer(machine.ClockSource, frequency, this, "0", uint.MaxValue, autoUpdate: true, eventEnabled: true),
                new LimitTimer(machine.ClockSource, frequency, this, "1", uint.MaxValue, autoUpdate: true, eventEnabled: true),
                new LimitTimer(machine.ClockSource, frequency, this, "2", autoUpdate: true, eventEnabled: true)
            };

            for (var i = 0; i < NumberOfInternalTimers; i++)
            {
                var j = i;
                timer[i].LimitReached += delegate
                {
                    rawInterruptStatus[j].Value = true;
                    lock(timer[j])
                    {
                        if(backgroundLoadValueIsValid[j])
                        {
                            backgroundLoadValueIsValid[j]= false;
                            timer[j].Limit = backgroundLoadValue[j];
                            // TODO: doesn't it reduce the tick count by one (I mean shouldn't we put the new value AFTER this tick is finished?)
                        }
                    }
                    UpdateInterrupt();
                };
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                // the rest of registers is generated using `GenerateRegistersForTimer` method calls located below

                {(long)Registers.Timer64ValueHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "TIM64VALUEU", valueProviderCallback: _ => (uint)(timer[Timer.Timer64].Value >> 32))
                },

                {(long)Registers.Timer64ValueLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "TIM64VALUEL", valueProviderCallback: _ => (uint)timer[Timer.Timer64].Value)
                },

                {(long)Registers.Timer64LoadValueHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out var timer64LoadValueHigh, name: "TIM64LOADVALU")
                },

                {(long)Registers.Timer64LoadValueLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "TIM64LOADVALL", writeCallback: (_, valueLow) => timer[Timer.Timer64].Limit = (timer64LoadValueHigh.Value << 32) | valueLow)
                },

                {(long)Registers.Timer64BackgroundLoadValueHigh, new DoubleWordRegister(this)
                        .WithValueField(0, 32, out var timer64BackgroundLoadValueHigh, name: "TIM64BGLOADVAU")
                },

                {(long)Registers.Timer64BackgroundLoadValueLow, new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: "TIM64BGLOADVAL", writeCallback: (_, val) =>
                        {
                            lock(timer[Timer.Timer64])
                            {
                                backgroundLoadValue[Timer.Timer64] = ((ulong)timer64BackgroundLoadValueHigh.Value << 32) | val;
                                backgroundLoadValueIsValid[Timer.Timer64] = true;
                            }
                        })
                },

                {(long)Registers.TimerMode, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, TimerMode>(0, 1, out timerMode, name: "TIM64MODE", writeCallback: (_, val) => InternalSoftReset(val))}
            };

            GenerateRegistersForTimer("TIM1", Timer.Timer32_1, registersMap);
            GenerateRegistersForTimer("TIM2", Timer.Timer32_2, registersMap, offset: Registers.Timer2Value - Registers.Timer1Value);
            // 64-bit timer has different value/load_value/load_background_value registers that are defined directly when creating registersMap dictionary; the rest are common ones
            GenerateRegistersForTimer("TIM64", Timer.Timer64, registersMap, offset: Registers.Timer64Control - Registers.Timer1Control, includeValueRegisters: false);

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public void Reset()
        {
            registers.Reset();
            foreach(var eachTimer in timer)
            {
                eachTimer.Reset();
            }
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1000;

        public GPIO Timer1IRQ { get; private set; }
        public GPIO Timer2IRQ { get; private set; }

        private void InternalSoftReset(TimerMode mode)
        {
            Reset();
            // reset will force `timerMode` value to 0, so we must set its value manually
            timerMode.Value = mode;
        }

        private void UpdateInterrupt()
        {
            if(timerMode.Value == TimerMode.One64BitTimer)
            {
                Timer1IRQ.Set(CalculateTimerMaskedInterruptValue(Timer.Timer64));
                Timer2IRQ.Unset();
            }
            else
            {
                Timer1IRQ.Set(CalculateTimerMaskedInterruptValue(Timer.Timer32_1));
                Timer2IRQ.Set(CalculateTimerMaskedInterruptValue(Timer.Timer32_2));
            }
        }

        private bool CalculateTimerMaskedInterruptValue(int timerId)
        {
            return rawInterruptStatus[timerId].Value && timerInterruptEnable[timerId].Value;
        }

        private void GenerateRegistersForTimer(string name, int timerId, Dictionary<long, DoubleWordRegister> registersMap, long offset = 0, bool includeValueRegisters = true)
        {
            if(includeValueRegisters)
            {
                registersMap.Add((long)Registers.Timer1Value + offset, new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Read, name: $"{name}VALUE", valueProviderCallback: _ => (uint)timer[timerId].Value)
                );

                registersMap.Add((long)Registers.Timer1LoadValue + offset, new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: $"{name}LOADVAL", writeCallback: (_, val) => timer[timerId].Limit = val)
                );

                registersMap.Add((long)Registers.Timer1BackgroundLoadValue + offset, new DoubleWordRegister(this)
                        .WithValueField(0, 32, name: $"{name}BGLOADVAL", writeCallback: (_, val) =>
                        {
                            lock(timer[timerId])
                            {
                                backgroundLoadValue[timerId] = val;
                                backgroundLoadValueIsValid[timerId] = true;
                            }
                        })
                );
            }

            registersMap.Add((long)Registers.Timer1Control + offset, new DoubleWordRegister(this)
                    .WithFlag(0, name: $"{name}ENABLE", writeCallback: (_, val) => timer[timerId].Enabled = val)
                    .WithEnumField<DoubleWordRegister, OperatingMode>(1, 1, name: $"{name}MODE", writeCallback: (_, val) => timer[timerId].Mode = (val == OperatingMode.OneShot ? WorkMode.OneShot : WorkMode.Periodic))
                    .WithFlag(2, out timerInterruptEnable[timerId], name: $"{name}INTEN", writeCallback: (_, __) => UpdateInterrupt())
            );

            registersMap.Add((long)Registers.Timer1RawInterruptStatus + offset, new DoubleWordRegister(this)
                    .WithFlag(0, out rawInterruptStatus[timerId], FieldMode.Read | FieldMode.WriteOneToClear, name: $"{name}RIS", writeCallback: (_, __) => UpdateInterrupt())
            );

            registersMap.Add((long)Registers.Timer1MaskedInterruptStatus + offset, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: $"{name}MIS", valueProviderCallback: _ => CalculateTimerMaskedInterruptValue(timerId))
            );
        }


        private readonly DoubleWordRegisterCollection registers;

        private readonly IEnumRegisterField<TimerMode> timerMode;
        private readonly IFlagRegisterField[]  timerInterruptEnable;
        private readonly IFlagRegisterField[] rawInterruptStatus;

        private readonly bool[] backgroundLoadValueIsValid;
        private readonly ulong[] backgroundLoadValue;
        private readonly LimitTimer[] timer;

        private const int NumberOfInternalTimers = 3;

        private static class Timer
        {
            public const int Timer32_1 = 0;
            public const int Timer32_2 = 1;
            public const int Timer64 = 2;
        }

        private enum TimerMode
        {
            Two32BitTimers = 0,
            One64BitTimer = 1
        }

        private enum OperatingMode
        {
            Periodic = 0,
            OneShot = 1
        }

        private enum Registers
        {
            Timer1Value = 0x0,
            Timer1LoadValue = 0x4,
            Timer1BackgroundLoadValue = 0x8,
            Timer1Control = 0xC,
            Timer1RawInterruptStatus = 0x10,
            Timer1MaskedInterruptStatus = 0x14,

            Timer2Value = 0x18,
            Timer2LoadValue = 0x1C,
            Timer2BackgroundLoadValue = 0x20,
            Timer2Control = 0x24,
            Timer2RawInterruptStatus = 0x28,
            Timer2MaskedInterruptStatus = 0x2C,

            Timer64ValueHigh = 0x30,
            Timer64ValueLow = 0x34,
            Timer64LoadValueHigh = 0x38,
            Timer64LoadValueLow = 0x3C,
            Timer64BackgroundLoadValueHigh = 0x40,
            Timer64BackgroundLoadValueLow = 0x44,
            Timer64Control = 0x48,
            Timer64RawInterruptStatus = 0x4C,
            Timer64MaskedInterruptStatus = 0x50,
            TimerMode = 0x54
        }
    }
}
