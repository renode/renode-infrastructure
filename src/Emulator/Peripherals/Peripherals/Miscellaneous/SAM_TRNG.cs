//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using System;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SAM_TRNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM_TRNG(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x4000;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out var enableUnverified, FieldMode.Write, name: "CR_ENABLE")
                .WithReservedBits(1, 7)
                .WithValueField(8, 24, out var enableKey, FieldMode.Write, name: "CR_KEY")
                .WithWriteCallback((_, __) =>
                {
                    /* The enable bit and enable key have to be written at
                     * the same time -  verify it here.
                     */
                    if(enableUnverified.Value && enableKey.Value == RngKey)
                    {
                        enable.Value = true;
                    }
                });

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, out enable, FieldMode.Read, name: "ISR_DATRDY");

            Registers.OutputData.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if(!enable.Value)
                    {
                        this.Log(LogLevel.Warning, "Reading TRNG data from an uninitialized device");

                        return 0;
                    }

                    return (uint)rng.Next();
                }, name: "ODATA");

            /* Interrupts not generated properly yet */
            Registers.InterruptEnable.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        interruptEnabled.Value = true;
                    }
                }, name: "IER_DATRDY");

            Registers.InterruptDisable.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        interruptEnabled.Value = false;
                    }
                }, name: "IER_DATRDY");

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out interruptEnabled, FieldMode.Read, name: "IMR_DATRDY");
        }

        private readonly PseudorandomNumberGenerator rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private IFlagRegisterField enable;
        private IFlagRegisterField interruptEnabled;

        /* Enable key - ASCII for "RNG" */
        private const uint RngKey = 0x524E47;

        private enum Registers
        {
            Control = 0x0,
            InterruptEnable = 0x10,
            InterruptDisable = 0x14,
            InterruptMask = 0x18,
            InterruptStatus = 0x1C,
            OutputData = 0x50,
        }
    }
}
