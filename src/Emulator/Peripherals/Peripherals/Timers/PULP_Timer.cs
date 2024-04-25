//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PULP_Timer : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public PULP_Timer(IMachine machine, long frequency) : base(machine)
        {
            interruptEnable = new IFlagRegisterField[NumberOfTimers];
            oneShot = new IFlagRegisterField[NumberOfTimers];
            cycleMode = new IFlagRegisterField[NumberOfTimers];

            var irqs = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                irqs[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);

            timers = new ComparingTimer[NumberOfTimers];
            for(var i = 0; i < NumberOfTimers; i++)
            {
                var j = i;
                timers[j] = new ComparingTimer(machine.ClockSource, frequency, this, $"Timer {j}", limit: uint.MaxValue, direction: Time.Direction.Ascending, workMode: Time.WorkMode.Periodic,
                    enabled: false, eventEnabled: true, compare: uint.MaxValue);

                timers[j].CompareReached += delegate
                {
                    this.Log(LogLevel.Noisy, "Timer {0} IRQ compare event", i);
                    if(interruptEnable[j].Value)
                    {
                        Connections[j].Blink(); //verified with RTL
                    }
                    if(oneShot[j].Value)
                    {
                        timers[j].Enabled = false;
                    }
                    if(cycleMode[j].Value)
                    {
                        timers[j].Value = 0;
                    }
                };

                var shift = j * 4;

                var configReg = ((Registers)(Registers.ConfigLow + shift)).Define(this)
                    .WithFlag(0,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                timers[j].Enabled = val;
                            }
                        },
                        valueProviderCallback: _ => timers[j].Enabled,
                        name: $"Timer {j} enable (EN)")
                    .WithFlag(1,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                timers[j].Value = 0;
                                // this also should affect the prescaller
                            }
                        }, valueProviderCallback: _ => false, name: "RST")
                    .WithFlag(2, out interruptEnable[j], name: "IRQEN")
                    .WithTag("IEM", 3, 1)
                    .WithFlag(4, out cycleMode[j], name: "MODE")
                    .WithFlag(5, out oneShot[j], name: "ONE_S")
                    .WithTag("PEN", 6, 1)
                    .WithTag("CCFG", 7, 1)
                    .WithTag("PVAL", 8, 8)
                    .WithReservedBits(16, 15)
                ;
                if(j == 0)
                {
                    // 64-bit timer mode not yet implemented
                    configReg.Tag("CASC", 31, 1);
                }
                else
                {
                    configReg.Reserved(31, 1);
                }

                ((Registers)(Registers.CounterValueLow + shift)).Define(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) => timers[j].Value = val,
                        valueProviderCallback: _ =>
                        {
                            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                            {
                                // being here means we are on the CPU thread
                                cpu.SyncTime();
                            }
                            return (uint)timers[j].Value;
                        },
                        name: $"Timer {j} counter value (CNT)")
                ;

                ((Registers)(Registers.ComparatorLow + shift)).Define(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Debug, "Setting Timer {0} Compare to: {1:X}", j, val);
                            timers[j].Compare = val;
                        },
                        valueProviderCallback: _ =>
                        {
                            return (uint)timers[j].Compare;
                        },
                        name: $"Timer {j} comparator value (CMP)")
                ;
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                timers[i].Reset();
            }
            // no need to reset Connections, as they only blink
        }

        public long Size => 0x80;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private ComparingTimer[] timers;
        private readonly IFlagRegisterField[] interruptEnable;
        private readonly IFlagRegisterField[] oneShot;
        private readonly IFlagRegisterField[] cycleMode;

        private const int NumberOfTimers = 2;

        private enum Registers : long
        {
            ConfigLow = 0x0,
            ConfigHigh = 0x4,
            CounterValueLow = 0x8,
            CounterValueHigh = 0xC,
            ComparatorLow = 0x10,
            ComparatorHigh = 0x14,
            StartCountingLow = 0x18,
            StartCountingHigh = 0x1C,
            ResetLow = 0x20,
            ResetHigh = 0x24
        }
    }
}
