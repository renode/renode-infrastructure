//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToQuadWord)]
    // Based on IA-PC HPET (High Precision Event Timers) Specification
    public class HPET : LimitTimer, IQuadWordPeripheral, IKnownSize
    {
        public HPET(IMachine machine, long frequency = 100000000)
            : base(machine.ClockSource, frequency, direction: Direction.Ascending, limit: uint.MaxValue)
        {
            if(frequency < MinimumFrequency) {
                throw new ConstructionException($"Provided frequency = {frequency}, but it should be at least {MinimumFrequency}");
            }

            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)Registers.GeneralCapabilitiesAndID, new QuadWordRegister(this)
                    .WithValueField(32, 32, FieldMode.Read, name: "COUNTER_CLK_PERIOD",
                        valueProviderCallback: _ => (ulong)(1e15 / Frequency))
                    .WithTag("VENDOR_ID", 16, 16)
                    .WithTaggedFlag("LEG_RT_CAP", 15)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("COUNT_SIZE_CAP", 13)
                    .WithTag("NUM_TIM_CAP", 8, 4)
                    .WithTag("REV_ID", 0, 8)
                },
                {(long)Registers.GeneralConfiguration, new QuadWordRegister(this)
                    .WithReservedBits(2, 62)
                    .WithTaggedFlag("LEG_RT_CNF", 1)
                    .WithFlag(0, valueProviderCallback: _ => Enabled,
                        writeCallback: (_, value) => Enabled = value, name: "ENABLE_CNF")
                },
                {(long)Registers.GeneralInterruptStatus, new QuadWordRegister(this)
                    .WithReservedBits(32, 32)
                    .WithTag("Tn_INT_STS", 0, 32)
                },
                {(long)Registers.MainCounterValue, new QuadWordRegister(this)
                    .WithValueField(0, 64, valueProviderCallback: _ =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        return (uint)Value;
                    },
                    writeCallback: (_, val) =>
                    {
                        if(Enabled)
                        {
                            this.WarningLog("Setting timer value when timer is enabled is not permited, ignoring");
                            return;
                        }
                        Value = val;
                    })
                },
            };
            for(var i = 0; i < NumberOfComparators; i++)
            {
                registersMap.Add(
                    (long)Registers.Timer0ConfigurationAndCapability + 0x20 * i,
                    new QuadWordRegister(this)
                        .WithTag($"T{i}_INT_ROUTE_CAP", 32, 32)
                        .WithReservedBits(16, 16)
                        .WithTaggedFlag($"T{i}_FSB_INT_DEL_CAP", 15)
                        .WithTaggedFlag($"T{i}_FSB_EN_CNF", 14)
                        .WithTag($"T{i}_INT_ROUTE_CNF", 9, 5)
                        .WithTaggedFlag("Tn_32MODE_CNF", 8)
                        .WithReservedBits(7, 1)
                        .WithTaggedFlag($"T{i}_VAL_SET_CNF", 6)
                        .WithTaggedFlag($"T{i}_SIZE_CAP", 5)
                        .WithTaggedFlag($"T{i}_PER_INT_CAP", 4)
                        .WithTaggedFlag($"T{i}_TYPE_CNF", 3)
                        .WithTaggedFlag($"T{i}_INT_ENB_CNF", 2)
                        .WithTaggedFlag($"T{i}_INT_TYPE_CNF", 1)
                        .WithReservedBits(0, 1)
                );
                registersMap.Add(
                    (long)Registers.Timer0ComparatorValue + 0x20 * i,
                    new QuadWordRegister(this)
                        .WithTag($"T{i}_COMPARATOR_VALUE", 0, 64)
                );
                registersMap.Add(
                    (long)Registers.Timer0FSBInterruptRoute + 0x20 * i,
                    new QuadWordRegister(this)
                        .WithTag($"T{i}_FSB_INT_ADDR", 32, 32)
                        .WithTag($"T{i}_FSB_INT_VAL", 0, 32)
                );
            }
            registers = new QuadWordRegisterCollection(this, registersMap);
        }

        public ulong ReadQuadWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public long Size => 0x158;

        private readonly QuadWordRegisterCollection registers;

        private const int NumberOfComparators = 3;

        // According to HPET Specification (2.4),
        // COUNTER_CLK_PERIOD should be at most 10^8, so frequency must be at least 10^7.
        private const int MinimumFrequency = (int)1e7;

        private enum Registers : long
        {
            GeneralCapabilitiesAndID = 0x0,
            GeneralConfiguration = 0x10,
            GeneralInterruptStatus = 0x20,
            MainCounterValue = 0xf0,
            Timer0ConfigurationAndCapability = 0x100,
            Timer0ComparatorValue = 0x108,
            Timer0FSBInterruptRoute = 0x110,
            Timer1ConfigurationAndCapability = 0x120,
            Timer1ComparatorValue = 0x128,
            Timer1FSBInterruptRoute = 0x130,
            Timer2ConfigurationAndCapability = 0x140,
            Timer2ComparatorValue = 0x148,
            Timer2FSBInterruptRoute = 0x150,
        }
    }
}
