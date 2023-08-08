//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2022 ION Mobility
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // SCG - System Clock Generator
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K_SCG : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K_SCG(IMachine machine) : base(machine)
        {
            Registers.ClockStatus.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => slowClockRatio.Value, name: "DIVSLOW")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => busClockRatio.Value, name: "DIVBUS")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => coreClockRatio.Value, name: "DIVCORE")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ => systemClockSource.Value, name: "SCS")
                .WithReservedBits(28, 4)
            ;

            Registers.RunClockControl.Define(this, 0x3000001)
                .WithValueField(0, 4, out slowClockRatio, name: "DIVSLOW")
                .WithValueField(4, 4, out busClockRatio, name: "DIVBUS")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, out coreClockRatio, name: "DIVCORE")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, out systemClockSource, name: "SCS")
                .WithReservedBits(28, 4)
            ;

            Registers.VLPRClockControl.Define(this)
                .WithValueField(0, 4, valueProviderCallback: _ => 3, name: "DIVSLOW")
                .WithValueField(4, 4, valueProviderCallback: _ => 1, name: "DIVBUS")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, valueProviderCallback: _ => 0, name: "DIVCORE")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, valueProviderCallback: _ => 3, name: "SCS")
                .WithReservedBits(28, 4)
            ;

            Registers.HSRUNClockControl.Define(this)
                .WithValueField(0, 4, valueProviderCallback: _ => 3, name: "DIVSLOW")
                .WithValueField(4, 4, valueProviderCallback: _ => 1, name: "DIVBUS")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, valueProviderCallback: _ => 0, name: "DIVCORE")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, valueProviderCallback: _ => 3, name: "SCS")
                .WithReservedBits(28, 4)
            ;

            Registers.CLKOUTConfiguration.Define(this)
                .WithReservedBits(0, 24)
                .WithValueField(24, 4, valueProviderCallback: _ => 1, name: "CLKOUTSEL")
                .WithReservedBits(28, 4)
            ;

            Registers.OscillatorControlStatus.Define(this)
                .WithFlag(0, valueProviderCallback: _ => true, name: "SOSCEN")
                .WithReservedBits(1, 15)
                .WithFlag(16, valueProviderCallback: _ => false, name: "SOSCCM")
                .WithFlag(17, valueProviderCallback: _ => false, name: "SOSCCMRE")
                .WithReservedBits(18, 5)
                .WithFlag(23, valueProviderCallback: _ => false, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "SOSCVLD")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => false, name: "SOSCSEL")
                .WithFlag(26, FieldMode.WriteOneToClear, name: "SOSCERR")
                .WithReservedBits(27, 5)
            ;

            Registers.OscillatorDivide.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => 1, name: "SOSCDIV1")
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, valueProviderCallback: _ => 1, name: "SOSCDIV2")
                .WithReservedBits(11, 21)
            ;

            Registers.SlowIRCControlStatus.Define(this, 0x3)
                .WithFlag(0, out slowIRCEnable, name: "SIRCEN")
                .WithFlag(1, name: "SIRCSTEN")
                .WithFlag(2, name: "SIRCLPEN")
                .WithReservedBits(3, 20)
                .WithFlag(23, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => slowIRCEnable.Value, name: "SIRCVLD")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => false, name: "SIRCSEL")
                .WithReservedBits(26, 6)
            ;

            Registers.SlowIRCDivide.Define(this, 0x101)
                .WithValueField(0, 3, name: "SIRCDIV1")
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, name: "SIRCDIV2")
                .WithReservedBits(11, 21)
            ;

            Registers.SlowIRCConfiguration.Define(this)
                .WithFlag(0, name: "RANGE")
                .WithReservedBits(1, 30)
            ;

            Registers.FastIRCControlStatus.Define(this)
                .WithFlag(0, valueProviderCallback: _ => true, name: "FIRCEN")
                .WithReservedBits(1, 2)
                .WithFlag(3, valueProviderCallback: _ => false, name: "FIRCREGOFF")
                .WithReservedBits(4, 19)
                .WithFlag(23, valueProviderCallback: _ => false, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "FIRCVLD")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "FIRCSEL")
                .WithFlag(26, valueProviderCallback: _ => false, name: "FIRCERR")
                .WithReservedBits(27, 5)
            ;
        }

        public long Size => 0x1000;

        private readonly IValueRegisterField systemClockSource;
        private readonly IValueRegisterField coreClockRatio;
        private readonly IValueRegisterField busClockRatio;
        private readonly IValueRegisterField slowClockRatio;
        private readonly IFlagRegisterField slowIRCEnable;

        private enum Registers
        {
            VersionId = 0x0,
            Parameter = 0x4,
            ClockStatus = 0x10,
            RunClockControl = 0x14,
            VLPRClockControl = 0x18,
            HSRUNClockControl = 0x1C,
            CLKOUTConfiguration = 0x20,
            OscillatorControlStatus = 0x100,
            OscillatorDivide = 0x104,
            OscillatorConfiguration = 0x108,

            SlowIRCControlStatus = 0x200,
            SlowIRCDivide = 0x204,
            SlowIRCConfiguration = 0x208,

            FastIRCControlStatus = 0x300,
            FastIRCDivide = 0x304,
            FastIRCConfiguration = 0x308,
            SystemPLLControlStatus = 0x600,
            SystemPLLDivide = 0x604,
            SystemPLLConfiguration = 0x608
        }
    }
}
