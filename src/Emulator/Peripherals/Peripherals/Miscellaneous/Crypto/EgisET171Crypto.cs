//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    /// <summary>
    /// This class only implements the TRNG/entropy source function of the crypto module
    /// Based on the Zephyr driver from https://github.com/EgisMCU/hal_egis
    /// </summary>
    public class EgisET171_Crypto : BasicDoubleWordPeripheral, IKnownSize
    {
        public EgisET171_Crypto(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100000;

        private void DefineRegisters()
        {
            Registers.Compatible.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0x400); // Software check that this register has this bit set, seems to be flags for what features the hardware supports
            Registers.ControlRegister.Define(this)
                .WithValueField(0, 32); // Driver expects this to keep its values
            Registers.BytesReady.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => UInt32.MaxValue); // Software checks if this is greater or equal to the number of bytes it needs, so set it to uintmax to also pass the check
            Registers.WakeupLevel.Define(this)
                .WithTag("WakeupLevel", 0, 32);
            Registers.CurrentWakeupLevel.Define(this)
                .WithTag("CurrentWakeupLevel", 0, 32);
            Registers.ConditioningKey0.DefineMany(this, 4, (register, idx) =>
                register.WithValueField(0, 32, name: $"ConditioningKey{idx}")
            );
            Registers.StatusRegister.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0x80 | 0x7); // Bits the driver expects to be set
            Registers.InitWait.Define(this)
                .WithTag("InitWait", 0, 32);
            Registers.OffTimeDelay.Define(this)
                .WithTag("OffTimeDelay", 0, 32);
            Registers.SampleClockDiv.Define(this)
                .WithTag("SampleClockDiv", 0, 32);
            Registers.RNGOutputRegister.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _ => (ulong)random.Next());
        }

        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private enum Registers
        {
            Compatible = 0x400,
            ControlRegister = 0x1000,
            BytesReady = 0x1004,
            WakeupLevel = 0x1008,
            CurrentWakeupLevel = 0x100C,
            ConditioningKey0 = 0x1010,
            // ...
            ConditioningKey3 = 0x101C,
            StatusRegister = 0x1030,
            InitWait = 0x1034,
            OffTimeDelay = 0x1040,
            SampleClockDiv = 0x1044,
            RNGOutputRegister = 0x1080,
        }
    }
}