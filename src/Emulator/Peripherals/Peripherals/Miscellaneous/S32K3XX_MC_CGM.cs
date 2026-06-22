//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Diagnostics;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_MC_CGM : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_MC_CGM(IMachine machine, List<IHasFrequency> coreClk) : base(machine)
        {
            this.coreClk = coreClk;

            lastStatusFlagValues = new int[20];
            for(var i = 0; i < lastStatusFlagValues.Length; i++)
            {
                lastStatusFlagValues[i] = 0;
            }
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            phaseLockedLoop0Frequency = 0;
            for(var i = 0; i < lastStatusFlagValues.Length; i++)
            {
                lastStatusFlagValues[i] = 0;
            }
            base.Reset();
            OnClockingChanged();
        }

        public long Size => 0x800;

        public ulong PhaseLockedLoop0Frequency
        {
            get => phaseLockedLoop0Frequency;
            set
            {
                phaseLockedLoop0Frequency = value;
                OnClockingChanged();
            }
        }

        private void OnClockingChanged()
        {
            // coreClk is controled by Mux0 Div0
            var baseClock = clockMux0Source.Value switch
            {
                SourceClock.FastInternalRCOscillator => FastInternalRCOscillatorFrequency,
                SourceClock.PhaseLockedLoopPHI0 => phaseLockedLoop0Frequency,
                _ => throw new UnreachableException($"Nonexistant SourceClock variant {clockMux0Source.Value}")
            };
            var coreClkFreq = mux0Div0Enable.Value ? (baseClock / (mux0Div0Divider.Value + 1)) : baseClock;
            foreach(var peripheral in coreClk)
            {
                peripheral.Frequency = coreClkFreq;
            }
        }

        private void DefineRegisters()
        {
            Registers.PCFSStepDuration.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("SDUR", 0, 16);
            Registers.PCFSDividerChange8.Define(this)
                .WithTag("INIT", 16, 16)
                .WithReservedBits(8, 8)
                .WithTag("RATE", 0, 8);
            Registers.PCFSDividerEnd8.Define(this, 0x0000_03E7)
                .WithReservedBits(20, 12)
                .WithTag("DIVE", 0, 20);
            Registers.PCFSDividerStart8.Define(this, 0x0000_03E7)
                .WithReservedBits(20, 12)
                .WithTag("DIVS", 0, 20);
            Registers.ClockMux0SelectControl.Define(this)
                .WithReservedBits(28, 4)
                .WithEnumField<DoubleWordRegister, SourceClock>(24, 4, out clockMux0Source, name: "SELCTL")
                .WithReservedBits(4, 20)
                .WithTaggedFlag("SAFE_SW", 3)
                .WithTaggedFlag("CLK_SW", 2)
                .WithTaggedFlag("RAMPDOWN", 1)
                .WithTaggedFlag("RAMPUP", 0)
                .WithChangeCallback((_, _) => OnClockingChanged());
            Registers.ClockMux0SelectStatus.Define(this, 0x0008_0000)
                .WithReservedBits(28, 4)
                .WithEnumField<DoubleWordRegister, SourceClock>(24, 4, mode: FieldMode.Read, valueProviderCallback: _ => clockMux0Source.Value, name: "SELSTAT")
                .WithValueField(17, 3, mode: FieldMode.Read, valueProviderCallback: _ => 0b001, name: "SWTRG")
                .WithTaggedFlag("SWIP", 16)
                .WithReservedBits(4, 12)
                // This is a hack. The driver does a lot of triggering changes, check that they are pending, and then check that they got cleared
                // To make our way through the driver we just return alternating flags set and unset on every other read so we make it through init
                .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ =>
                {
                    lastStatusFlagValues[0] = ~lastStatusFlagValues[0];
                    return (byte)lastStatusFlagValues[0];
                });
            Registers.ClockMux0Divider0Control.Define(this, 0x8000_0000)
                .WithFlag(31, out mux0Div0Enable, name: "mux0Div0DE")
                .WithReservedBits(19, 12)
                .WithValueField(16, 3, out mux0Div0Divider, name: "mux0Div0DIV")
                .WithReservedBits(0, 16)
                .WithChangeCallback((_, _) => OnClockingChanged());
            Registers.ClockMux0DividerUpdateStatus.Define32Many(this, 20, (reg, i) =>
            {
                reg
                    .WithReservedBits(1, 30)
                    .WithFlag(0, mode: FieldMode.Read, valueProviderCallback: _ => false, name: $"DIV_STAT{i}");
            }, 0x40);
            // End of actually implemented registers, the following are just mocks to make the driver happy.
            Registers.ClockMux1SelectControl.Define32Many(this, 19, (reg, i) =>
            {
                reg
                    .WithReservedBits(28, 4)
                    .WithValueField(24, 4, name: "SELCTL")
                    .WithReservedBits(4, 20)
                    .WithTaggedFlag("SAFE_SW", 3)
                    .WithTaggedFlag("CLK_SW", 2)
                    .WithTaggedFlag("RAMPDOWN", 1)
                    .WithTaggedFlag("RAMPUP", 0)
                    .WithChangeCallback((_, _) => OnClockingChanged());
            }, 0x40);
            Registers.ClockMux1SelectStatus.Define32Many(this, 19, (reg, i) =>
            {
                reg
                    .WithReservedBits(28, 4)
                    .WithTag("SELSTAT", 24, 4)
                    .WithValueField(17, 3, mode: FieldMode.Read, valueProviderCallback: _ => 0b001, name: "SWTRG")
                    .WithTaggedFlag("SWIP", 16)
                    .WithReservedBits(4, 12)
                    // This is a hack. The driver does a lot of triggering changes, check that they are pending, and then check that they got cleared
                    // To make our way through the driver we just return alternating flags set and unset on every other read so we make it through init
                    .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ =>
                    {
                        lastStatusFlagValues[i + 1] = ~lastStatusFlagValues[i + 1];
                        return (byte)lastStatusFlagValues[i + 1];
                    });
            }, 0x40, 0x0008_0000);
        }

        private ulong phaseLockedLoop0Frequency = 0; // Will get its value set from PLL peripheral before using it

        private IEnumRegisterField<SourceClock> clockMux0Source;
        private IFlagRegisterField mux0Div0Enable;
        private IValueRegisterField mux0Div0Divider;
        private readonly int[] lastStatusFlagValues;
        private readonly List<IHasFrequency> coreClk;
        // Static clock frequencies
        private const ulong FastInternalRCOscillatorFrequency = 48_000_000;

        private enum SourceClock : byte
        {
            FastInternalRCOscillator = 0b0000,
            PhaseLockedLoopPHI0 = 0b1000
        }

        private enum Registers
        {
            PCFSStepDuration = 0x0,
            // Gap intentional
            PCFSDividerChange8 = 0x58,
            PCFSDividerEnd8 = 0x5C,
            PCFSDividerStart8 = 0x60,
            // Gap intentional
            ClockMux0SelectControl = 0x300,
            ClockMux0SelectStatus = 0x304,
            ClockMux0Divider0Control = 0x308,
            // ...
            ClockMux0Divider7Control = 0x324,
            ClockMux0DividerTriggerControl = 0x334,
            ClockMux0DividerTrigger = 0x338,
            ClockMux0DividerUpdateStatus = 0x33C,
            // The following status registers are just mocked
            ClockMux1SelectControl = 0x340,
            ClockMux1SelectStatus = 0x344,
        }
    }
}