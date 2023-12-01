//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This timer model is limited to only one match channel (channel 0), and only in reset mode (MR0R=1).
    public class LPC_CTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public LPC_CTimer(IMachine machine, long frequency = DefaultFrequency) : base(machine)
        {
            this.frequency = frequency;
            timer = new LimitTimer(machine.ClockSource, frequency, this, nameof(timer),
                direction: Direction.Ascending, eventEnabled: true);
            timer.LimitReached += () =>
            {
                match0InterruptFlag.Value = true;
                UpdateInterrupt();
            };
            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            IRQ.Unset();
            base.Reset();
        }

        public GPIO IRQ { get; }
        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Interrupt.Define(this)
                .WithFlag(0, name: "MR0INT",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear, flagField: out match0InterruptFlag)
                .WithTaggedFlag("MR1INT", 1)
                .WithTaggedFlag("MR2INT", 2)
                .WithTaggedFlag("MR3INT", 3)
                .WithTaggedFlag("CR0INT", 4)
                .WithTaggedFlag("CR1INT", 5)
                .WithTaggedFlag("CR2INT", 6)
                .WithReservedBits(7, 25)
                .WithChangeCallback((_, __) => UpdateInterrupt());

            Registers.TimerControl.Define(this)
                .WithFlag(0, name: "CEN",
                    valueProviderCallback: _ => timer.Enabled,
                    changeCallback: (_, value) => timer.Enabled = value)
                .WithFlag(1, name: "CRST",
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            timer.Value = 0;
                        }
                        // The counters remain reset until CRST is returned to zero.
                        timer.Enabled = !value;
                    })
                .WithReservedBits(2, 30);

            Registers.TimerCounter.Define(this)
                .WithValueField(0, 32, name: "TCVAL",
                    mode: FieldMode.Read, valueProviderCallback: _ => timer.Value);

            Registers.Prescale.Define(this)
                .WithValueField(0, 32, name: "PRVAL",
                    valueProviderCallback: _ => (ulong)timer.Divider - 1,
                    changeCallback: (_, value) => timer.Divider = (int)value + 1);

            Registers.PrescaleCounter.Define(this)
                .WithTag("PCVAL", 0, 32)
                .WithReadCallback((_, __) => this.WarningLog("Prescale counter is not implemented"));

            Registers.MatchControl.Define(this)
                .WithFlag(0, name: "MR0I",
                    flagField: out match0InterruptEnable)
                .WithFlag(1, name: "MR0R",
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            this.WarningLog("Non-resetting compare is not implemented");
                        }
                    })
                .WithTaggedFlag("MR0S", 2) // not planned controllable TC stop per match channel, unused by Zephyr and Linux
                .WithTaggedFlag("MR1I", 3)
                .WithTaggedFlag("MR1R", 4)
                .WithTaggedFlag("MR1S", 5)
                .WithTaggedFlag("MR2I", 6)
                .WithTaggedFlag("MR2R", 7)
                .WithTaggedFlag("MR2S", 8)
                .WithTaggedFlag("MR3I", 9)
                .WithTaggedFlag("MR3R", 10)
                .WithTaggedFlag("MR3S", 11)
                .WithReservedBits(12, 12)
                .WithTaggedFlag("MR0RL", 24)
                .WithTaggedFlag("MR1RL", 25)
                .WithTaggedFlag("MR2RL", 26)
                .WithTaggedFlag("MR3RL", 27)
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Registers.Match0.Define(this)
                .WithValueField(0, 32, name: "MATCH",
                    valueProviderCallback: _ => timer.Limit,
                    changeCallback: (_, value) => timer.Limit = value);

            Registers.Match1.Define(this)
                .WithTag("MATCH", 0, 32);

            Registers.Match2.Define(this)
                .WithTag("MATCH", 0, 32);

            Registers.Match3.Define(this)
                .WithTag("MATCH", 0, 32);

            Registers.CaptureControl.Define(this)
                .WithTaggedFlag("CAP0RE", 0)
                .WithTaggedFlag("CAP0FE", 1)
                .WithTaggedFlag("CAP0I", 2)
                .WithTaggedFlag("CAP1RE", 3)
                .WithTaggedFlag("CAP1FE", 4)
                .WithTaggedFlag("CAP1I", 5)
                .WithTaggedFlag("CAP2RE", 6)
                .WithTaggedFlag("CAP2FE", 7)
                .WithTaggedFlag("CAP2I", 8)
                .WithReservedBits(9, 23);

            Registers.Capture0.DefineMany(this, NumberOfChannels, (register, i) => register
                .WithTag("CAP", 0, 32)
            );

            Registers.ExternalMatch.Define(this)
                .WithTaggedFlag("EM0", 0)
                .WithTaggedFlag("EM1", 1)
                .WithTaggedFlag("EM2", 2)
                .WithTaggedFlag("EM3", 3)
                .WithTag("EMC0", 4, 2)
                .WithTag("EMC1", 6, 2)
                .WithTag("EMC2", 8, 2)
                .WithTag("EMC3", 10, 2)
                .WithReservedBits(12, 20);

            Registers.CountControl.Define(this)
                .WithTag("CTMODE", 0, 2)
                .WithTag("CINSEL", 2, 2)
                .WithTaggedFlag("ENCC", 4)
                .WithTag("SELCC", 5, 2)
                .WithReservedBits(8, 24);

            Registers.PwmControl.Define(this)
                .WithTaggedFlag("PWMEN0", 0)
                .WithTaggedFlag("PWMEN1", 1)
                .WithTaggedFlag("PWMEN2", 2)
                .WithTaggedFlag("PWMEN3", 3)
                .WithReservedBits(5, 27);

            Registers.Match0Shadow.DefineMany(this, NumberOfChannels, (register, i) => register
                .WithTag("SHADOW", 0, 32)
            );
        }

        private void UpdateInterrupt()
        {
            var irqValue = match0InterruptEnable.Value && match0InterruptFlag.Value;
            IRQ.Set(irqValue);
            this.DebugLog("IRQ set to {0}", irqValue);
        }

        private IFlagRegisterField match0InterruptFlag;
        private IFlagRegisterField match0InterruptEnable;

        private readonly LimitTimer timer;
        private readonly long frequency;

        // Currently only one channel (index 0) is implemented
        private const int NumberOfChannels = 4;
        private const long DefaultFrequency = 10000000;

        private enum Registers : uint
        {
            Interrupt = 0x00,
            TimerControl = 0x04,
            TimerCounter = 0x08,
            Prescale = 0x0c,
            PrescaleCounter = 0x10,
            MatchControl = 0x14,
            Match0 = 0x18,
            Match1 = 0x1c,
            Match2 = 0x20,
            Match3 = 0x24,
            CaptureControl = 0x28,
            Capture0 = 0x2c,
            Capture1 = 0x30,
            Capture2 = 0x34,
            Capture3 = 0x38,
            ExternalMatch = 0x3c,
            // mind the gap
            CountControl = 0x70,
            PwmControl = 0x74,
            Match0Shadow = 0x78,
            Match1Shadow = 0x7c,
            Match2Shadow = 0x80,
            Match3Shadow = 0x84,
        }
    }
}
