//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class LiteX_CPUTimer : BasicDoubleWordPeripheral, IKnownSize, IRiscVTimeProvider
    {
        public LiteX_CPUTimer(Machine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();

            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "cpu timer", enabled: true, eventEnabled: true);
            innerTimer.CompareReached += () =>
            {
                this.Log(LogLevel.Noisy, "Limit reached, setting IRQ");
                IRQ.Set(true);
            };
            DefineRegisters();
        }

        public long Size => 0x100;

        public GPIO IRQ {get;}

        public ulong TimerValue => innerTimer.Value;

        public override void Reset()
        {
            base.Reset();

            innerTimer.Reset();
            latchedValue = 0;
            IRQ.Set(false);
        }

        private void DefineRegisters()
        {
            Register.Latch.Define(this)
                .WithFlag(0, name: "latch_bit", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    latchedValue = innerTimer.Value;
                })
            ;

            Register.Time.DefineMany(this, 8, stepInBytes: 4, setup: (reg, idx) =>
            {
                // idx=0 - most significant byte
                // ...
                // idx=7 - least significant byte
                reg.WithValueField(0, 8, valueProviderCallback: (_) =>
                {
                    return (uint)(latchedValue >> ((7 - idx) * 8) & 0xff);
                });
            });

            Register.TimeCompare.DefineMany(this, 8, stepInBytes: 4, setup: (reg, idx) =>
            {
                // idx=0 - most significant byte
                // ...
                // idx=7 - least significant byte

                // this field should by 8-bits long, but it's defined as 32-bits (and the value is ANDed with 0xFF) to avoid unhandled bits warnings
                reg.WithValueField(0, 32, writeCallback: (_, val) =>
                {
                    innerTimer.Compare = innerTimer.Compare.ReplaceBits((ulong)(val & 0xFF), 8, (7 - idx) * 8);
                    this.Log(LogLevel.Noisy, "Compare value set to 0x{0:X}, dpos: {1}", innerTimer.Compare, (7 - idx) * 8);
                    if(innerTimer.Value < innerTimer.Compare)
                    {
                        this.Log(LogLevel.Noisy, "Current timer value is 0x{0:X} - clearing IRQ", innerTimer.Value);
                        IRQ.Set(false);
                    }
                });
            });
        }

        private readonly ComparingTimer innerTimer;
        private ulong latchedValue;

        private enum Register
        {
            Latch = 0x0,
            Time = 0x4,
            TimeCompare = 0x24,
        }
    }
}
