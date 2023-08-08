//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals
{
    public class HiFive_PWM : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IPeripheralRegister<IGPIOReceiver, NumberRegistrationPoint<int>>
    {
        public HiFive_PWM(IMachine machine, uint frequency = 16000000)
        {
            connections = new Dictionary<int, IGPIO>
            {
                {0, new GPIO()},
                {1, new GPIO()},
                {2, new GPIO()},
                {3, new GPIO()}
            };

            this.machine = machine;

            IRQ = new GPIO();
            interruptPending = new IFlagRegisterField[NumberOfComparers];
            compare = new IValueRegisterField[NumberOfComparers];

            rawTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(rawTimer), TimerLimit + 1, workMode: WorkMode.Periodic, direction: Direction.Ascending, eventEnabled: true);
            rawTimer.LimitReached += HandleLimitReached;

            timers = new ComparingTimer[NumberOfComparers];
            for(var i = 0; i < timers.Length; i++)
            {
                var j = i;
                timers[i] = new ComparingTimer(machine.ClockSource, frequency, this, (i + 1).ToString(), CompareMask + 1, workMode: WorkMode.Periodic, compare: CompareMask, direction: Direction.Ascending, eventEnabled: true);
                timers[i].CompareReached += () =>
                {
                    // handle 'pwmzerocmp' flag (defined only for timer0)
                    if(i == 0)
                    {
                        // documentation says that this should be done one cycle after the 'pwms' counter reaches the compare value!
                        if(timers[i].Value == compare[i].Value && resetAfterMatch.Value)
                        {
                            SetValue(0);
                            HandleLimitReached();
                        }
                    }

                    UpdateCompare(j);
                };
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out scale, name: "pwmscale", writeCallback: (_, value) =>
                    {
                        Array.ForEach(timers, t => t.Divider = (uint)(1 << (int)value));
                        Array.ForEach(timers, t => t.Value = (rawTimer.Value >> (int)value) & CompareMask);
                    })
                    .WithFlag(8, out sticky, name: "pwmsticky")
                    .WithFlag(9, out resetAfterMatch, name: "zerocmp")
                    .WithFlag(12, out enableAlways, name: "pwmenalways", writeCallback: (_, __) => RecalculateEnable())
                    .WithFlag(13, out enableOneShot, name: "pwmenoneshot", writeCallback: (_, __) => RecalculateEnable())
                    .WithFlag(28, out interruptPending[0], name: "pwmcmp0ip")
                    .WithFlag(29, out interruptPending[1], name: "pwmcmp1ip")
                    .WithFlag(30, out interruptPending[2], name: "pwmcmp2ip")
                    .WithFlag(31, out interruptPending[3], name: "pwmcmp3ip")
                    // this is a global update
                    .WithWriteCallback((_, __) => UpdateInterrupt())
                },

                {(long)Registers.Count, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: "pwmcount", valueProviderCallback: _ => (uint)rawTimer.Value, writeCallback: (_, value) =>
                    {
                        SetValue((uint)value);
                        UpdateInterrupt();
                    })
                    .WithReservedBits(31, 1)
                },

                {(long)Registers.ScaledCount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, name: "pwms", valueProviderCallback: _ => (uint)timers[0].Value)
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Compare0, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out compare[0], name: "pwmcmp0", writeCallback: (_, value) => UpdateCompare(0))
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Compare1, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out compare[1], name: "pwmcmp1", writeCallback: (_, value) => UpdateCompare(1))
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Compare2, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out compare[2], name: "pwmcmp2", writeCallback: (_, value) => UpdateCompare(2))
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Compare3, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out compare[3], name: "pwmcmp3", writeCallback: (_, value) => UpdateCompare(3))
                    .WithReservedBits(16, 16)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public void Reset()
        {
            registers.Reset();
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

        public GPIO IRQ { get; private set; }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections => connections;

        private void RecalculateEnable()
        {
            var enabled = (enableAlways.Value || enableOneShot.Value);
            rawTimer.Enabled = enabled;
            Array.ForEach(timers, t => t.Enabled = enabled);
        }

        private void HandleLimitReached()
        {
            enableOneShot.Value = false;
            RecalculateEnable();
        }

        private void UpdateInterrupt()
        {
            var shouldBeSet = false;
            for(var i = 0; i < timers.Length; i++)
            {
                var isInterrupt = (timers[i].Value >= compare[i].Value);
                if(sticky.Value)
                {
                    interruptPending[i].Value |= isInterrupt;
                }
                else
                {
                    interruptPending[i].Value = isInterrupt;
                }

                connections[i].Set(isInterrupt);
                shouldBeSet |= isInterrupt;
            }

            IRQ.Set(shouldBeSet);
        }

        private void UpdateCompare(int i)
        {
            timers[i].Compare = (timers[i].Value == 0)
                ? compare[i].Value
                : 0;

            UpdateInterrupt();
        }

        private void SetValue(uint value)
        {
            rawTimer.Value = value;
            Array.ForEach(timers, t => t.Value = (value >> (int)scale.Value) & CompareMask);
        }

        public void Register(IGPIOReceiver peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IGPIOReceiver peripheral)
        {
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        private IFlagRegisterField[] interruptPending;
        private IFlagRegisterField sticky;
        private IFlagRegisterField resetAfterMatch;
        private IFlagRegisterField enableAlways;
        private IFlagRegisterField enableOneShot;
        private IValueRegisterField scale;
        private IValueRegisterField[] compare;

        private readonly LimitTimer rawTimer;
        private readonly ComparingTimer[] timers;
        private readonly DoubleWordRegisterCollection registers;
        private readonly Dictionary<int, IGPIO> connections;
        private readonly IMachine machine;

        private enum Registers
        {
            Configuration = 0x0,
            // 0x4 is reserved
            Count = 0x8,
            // 0xC is reserved
            ScaledCount = 0x10,
            // 0x14, 0x18, 0x1C are reserved
            Compare0 = 0x20,
            Compare1 = 0x24,
            Compare2 = 0x28,
            Compare3 = 0x2C
        }

        private const uint TimerLimit = (1u << 31) - 1;
        private const uint CompareMask = (1u << 16) - 1;
        private const int NumberOfComparers = 4;
    }
}