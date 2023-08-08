//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Marvell_Armada_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public Marvell_Armada_Timer(IMachine machine, long frequency) : base(machine.ClockSource, frequency, direction: Direction.Descending, limit: uint.MaxValue, enabled: true)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithTaggedFlag("Timer0En", 0)
                    .WithTaggedFlag("Timer0Auto", 1)
                    .WithTaggedFlag("Timer1En", 2)
                    .WithTaggedFlag("Timer1Auto", 3)
                    .WithTaggedFlag("Timer2En", 4)
                    .WithTaggedFlag("Timer2Auto", 5)
                    .WithTaggedFlag("Timer3En", 6)
                    .WithTaggedFlag("Timer3Auto", 7)
                    .WithTaggedFlag("WdTimerEn", 8)
                    .WithTaggedFlag("WdTimerAuto", 9)
                    .WithTaggedFlag("WdTimer25MhzEn", 10)
                    .WithTaggedFlag("Timer0_25MhzEn", 11)
                    .WithTaggedFlag("Timer1_25MhzEn", 12)
                    .WithTaggedFlag("Timer2_25MhzEn", 13)
                    .WithTaggedFlag("Timer3_25MhzEn", 14)
                    .WithReservedBits(15, 1)
                    .WithTag("WdTimerRatio", 16, 3)
                    .WithTag("Timer0Ratio", 19, 3)
                    .WithTag("Timer1Ratio", 22, 3)
                    .WithTag("Timer2Ratio", 25, 3)
                    .WithTag("Timer3Ratio", 28, 3)
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.EventStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("Timer0Expired", 0)
                    .WithReservedBits(1, 7)
                    .WithTaggedFlag("Timer1Expired", 8)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag("Timer2Expired", 16)
                    .WithReservedBits(17, 7)
                    .WithTaggedFlag("Timer3Expired", 24)
                    .WithReservedBits(25, 6)
                    .WithTaggedFlag("WdTimerExpired", 31)
                },
                {(long)Registers.Timer0ReloadValue, new DoubleWordRegister(this)
                    .WithTag("Timer0Rel", 0, 32)
                },
                {(long)Registers.Timer0Value, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        return (uint)Value;
                    }, writeCallback: (_, value) => Value = value)
                },
             };
            registers = new DoubleWordRegisterCollection(this, registersMap);
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
            base.Reset();
            registers.Reset();
        }

        public long Size => 0x100;

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            Control = 0x00,
            EventStatus = 0x04,
            // gap
            Timer0ReloadValue = 0x10,
            Timer0Value = 0x14,
            Timer1ReloadValue = 0x18,
            Timer1Value = 0x1c,
            Timer2ReloadValue = 0x20,
            Timer2Value = 0x24,
            Timer3ReloadValue = 0x28,
            Timer3Value = 0x2c,
        }
    }
}
