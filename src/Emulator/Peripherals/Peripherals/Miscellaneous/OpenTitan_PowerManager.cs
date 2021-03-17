//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_PowerManager : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_PowerManager(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out wakeupState, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE.wakeup")
                .WithIgnoredBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out wakeupEnable, name: "INTR_ENABLE.wakeup")
                .WithIgnoredBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, name: "INTR_TEST.wakeup", writeCallback: (_, val) =>
                {
                    wakeupState.Value |= val;
                    UpdateInterrupts();
                })
                .WithIgnoredBits(1, 31);
            
            Registers.ControlConfigRegWriteEnable.Define(this, 0x1)
                .WithFlag(0, out controlConfigRegWriteEnable, FieldMode.Read, name: "CTRL_CFG_REGWEN.EN")
                .WithIgnoredBits(1, 31);

            Registers.Control.Define(this, 0x180)
                .WithFlag(0, out lowPowerHint)
                .WithReservedBits(1, 3)
                .WithFlag(4, out coreClockEnable)
                .WithFlag(5, out ioClockEnable)
                .WithFlag(6, out usbClockEnableLowPower)
                .WithFlag(7, out usbClockEnableActive)
                .WithFlag(8, out mainPowerDown)
                .WithReservedBits(9, 23)
                .WithWriteCallback((_, __) => LogPowerState());

            Registers.ConfigClockDomainSync.Define(this, 0x1)
                .WithReservedBits(0, 32);
            
            Registers.WakeupEnableRegWriteEnable.Define(this)
                .WithReservedBits(0, 32);

            Registers.WakeStatus.Define(this)
                .WithReservedBits(0, 32);

            Registers.ResetEnableRegWriteEnable.Define(this, 0x1)
                .WithReservedBits(0,32);

            Registers.ResetEnable.Define(this)
                .WithReservedBits(0, 32);

            Registers.ResetStatus.Define(this)
                .WithReservedBits(0, 32);

            Registers.EscalateResetStatue.Define(this)
                .WithReservedBits(0, 32);

            Registers.WakeInfoCaptureDis.Define(this)
                .WithReservedBits(0, 32);

            Registers.WakeInfo.Define(this)
                .WithReservedBits(0, 32);
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(wakeupState.Value && wakeupEnable.Value);
        }

        private void LogPowerState()
        {
            this.Log(LogLevel.Debug, "LowPowerHint:{0}, CoreClockEnable:{1}, " +
                                     "IoClockEnable:{2}, UsbClockEnableLowPower:{3}, " +
                                     "UsbClockEnableActive: {4}, MainPowerDown: {5}", 
                                     lowPowerHint.Value, coreClockEnable.Value,
                                     ioClockEnable.Value, usbClockEnableLowPower.Value,
                                     usbClockEnableActive.Value, mainPowerDown.Value);
        }

        private IFlagRegisterField wakeupState;
        private IFlagRegisterField wakeupEnable;

        private IFlagRegisterField controlConfigRegWriteEnable;
        
        private IFlagRegisterField lowPowerHint;
        private IFlagRegisterField coreClockEnable;
        private IFlagRegisterField ioClockEnable;
        private IFlagRegisterField usbClockEnableLowPower;
        private IFlagRegisterField usbClockEnableActive;
        private IFlagRegisterField mainPowerDown;

        private enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            ControlConfigRegWriteEnable = 0xc,
            Control = 0x10,
            ConfigClockDomainSync = 0x14,
            WakeupEnableRegWriteEnable = 0x18,
            WakeupEnable = 0x1c,
            WakeStatus = 0x20,
            ResetEnableRegWriteEnable = 0x24,
            ResetEnable = 0x28,
            ResetStatus = 0x2c,
            EscalateResetStatue = 0x30,
            WakeInfoCaptureDis = 0x34,
            WakeInfo = 0x38
        }
    }
}

