//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_SystemResetControl : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public OpenTitan_SystemResetControl(IMachine machine, OpenTitan_ResetManager resetManager) : base(machine)
        {
            DefineRegisters();
            IRQ = new GPIO();
            FatalFault = new GPIO();
            this.resetManager = resetManager;
            combosDefinition = new ComboDefinition[NumberOfCombos];
            durationTimer = new DurationTimer(machine.ClockSource, DurationTimerFrequency, this, "comboDurationTimer");
            Reset();
        }

        public override void Reset()
        {
            durationTimer.Cancel();
            base.Reset();
            // IRQ.Unset is not necessary here as it should happen after registers are set to reset values
            FatalFault.Unset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == 0)
            {
                powerButton = value;
            }
            durationTimer.Cancel();
            CheckCombos();
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; }
        public GPIO FatalFault { get; }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out interruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "sysrst_ctrl")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => { UpdateInterrupt(); });

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptEnable, name: "sysrst_ctrl")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => { UpdateInterrupt(); });

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) { interruptState.Value = true; } }, name: "sysrst_ctrl")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => { UpdateInterrupt(); });

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalFault.Blink(); }, name: "fatal_fault")
                .WithReservedBits(1, 31);

            Registers.ConfigurationWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("write_en", 0)
                .WithReservedBits(1, 31);

            Registers.ECResetControl.Define(this, 0x7d0)
                .WithTag("ec_rst_pulse", 0, 16)
                .WithReservedBits(16, 16);

            Registers.UltralowpowerACDebounceControl.Define(this, 0x1f40)
                .WithTag("ulp_ac_debounce_timer", 0, 16)
                .WithReservedBits(16, 16);

            Registers.UltralowpowerLidDebounceControl.Define(this, 0x1f40)
                .WithTag("ulp_lid_debounce_timer", 0, 16)
                .WithReservedBits(16, 16);

            Registers.UltralowpowerPwrDebounceControl.Define(this, 0x1f40)
                .WithTag("ulp_pwrb_debounce_timer", 0, 16)
                .WithReservedBits(16, 16);

            Registers.UltralowpowerControl.Define(this)
                .WithTaggedFlag("ulp_enable", 0)
                .WithReservedBits(1, 31);

            Registers.UltralowpowerStatus.Define(this)
                .WithTaggedFlag("ulp_wakeup", 0)
                .WithReservedBits(1, 31);

            Registers.WakeupStatus.Define(this)
                .WithTaggedFlag("wakeup_sts", 0)
                .WithReservedBits(1, 31);

            Registers.KeyInputOutputInvert.Define(this)
                .WithFlag(0, out key0Invert, name: "key0_in")
                .WithFlag(1, name: "key0_out")
                .WithFlag(2, out key1Invert, name: "key1_in")
                .WithFlag(3, name: "key1_out")
                .WithFlag(4, out key2Invert, name: "key2_in")
                .WithFlag(5, name: "key2_out")
                .WithFlag(6, out pwrButtonInvert, name: "pwrb_in")
                .WithTaggedFlag("pwrb_out", 7)
                .WithFlag(8, out acPresentInvert, name: "ac_present")
                .WithTaggedFlag("bat_disable", 9)
                .WithTaggedFlag("lid_open", 10)
                .WithTaggedFlag("z3_wakeup", 11)
                .WithReservedBits(12, 20)
                .DefineWriteCallback((prevVal, val) =>
                    {
                        if(prevVal != val)
                        {
                            durationTimer.Cancel();
                            CheckCombos();
                        }
                    });

            Registers.PinAllowedControl.Define(this, 0x82)
                .WithTaggedFlag("bat_disable_0", 0)
                .WithTaggedFlag("ec_rst_l_0", 1)
                .WithTaggedFlag("pwrb_out_0", 2)
                .WithTaggedFlag("key0_out_0", 3)
                .WithTaggedFlag("key1_out_0", 4)
                .WithTaggedFlag("key2_out_0", 5)
                .WithTaggedFlag("z3_wakeup_0", 6)
                .WithTaggedFlag("flash_wp_l_0", 7)
                .WithTaggedFlag("bat_disable_1", 8)
                .WithTaggedFlag("ec_rst_l_1", 9)
                .WithTaggedFlag("pwrb_out_1", 10)
                .WithTaggedFlag("key0_out_1", 11)
                .WithTaggedFlag("key1_out_1", 12)
                .WithTaggedFlag("key2_out_1", 13)
                .WithTaggedFlag("z3_wakeup_1", 14)
                .WithTaggedFlag("flash_wp_l_1", 15)
                .WithReservedBits(16, 16);

            Registers.PinOutControl.Define(this, 0x82)
                .WithTaggedFlag("bat_disable", 0)
                .WithTaggedFlag("ec_rst_l", 1)
                .WithTaggedFlag("pwrb_out", 2)
                .WithTaggedFlag("key0_out", 3)
                .WithTaggedFlag("key1_out", 4)
                .WithTaggedFlag("key2_out", 5)
                .WithTaggedFlag("z3_wakeup", 6)
                .WithTaggedFlag("flash_wp_l", 7)
                .WithReservedBits(8, 24);

            Registers.PinOverrideValue.Define(this)
                .WithTaggedFlag("bat_disable", 0)
                .WithTaggedFlag("ec_rst_l", 1)
                .WithTaggedFlag("pwrb_out", 2)
                .WithTaggedFlag("key0_out", 3)
                .WithTaggedFlag("key1_out", 4)
                .WithTaggedFlag("key2_out", 5)
                .WithTaggedFlag("z3_wakeup", 6)
                .WithTaggedFlag("flash_wp_l", 7)
                .WithReservedBits(8, 24);

            Registers.PinInValue.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: (_) => powerButton, name: "pwrb_in")
                .WithTaggedFlag("key0_in", 1)
                .WithTaggedFlag("key1_in", 2)
                .WithTaggedFlag("key2_in", 3)
                .WithTaggedFlag("lid_open", 4)
                .WithTaggedFlag("ac_present", 5)
                .WithTaggedFlag("ec_rst_l", 6)
                .WithTaggedFlag("flash_wp_l", 7)
                .WithReservedBits(8, 24);

            Registers.KeyInterruptControl.Define(this)
                .WithTaggedFlag("pwrb_in_H2L", 0)
                .WithTaggedFlag("key0_in_H2L", 1)
                .WithTaggedFlag("key1_in_H2L", 2)
                .WithTaggedFlag("key2_in_H2L", 3)
                .WithTaggedFlag("ac_present_H2L", 4)
                .WithTaggedFlag("ec_rst_l_H2L", 5)
                .WithTaggedFlag("flash_wp_l_H2L", 6)
                .WithTaggedFlag("pwrb_in_L2H", 8)
                .WithTaggedFlag("key0_in_L2H", 9)
                .WithTaggedFlag("key1_in_L2H", 10)
                .WithTaggedFlag("key2_in_L2H", 11)
                .WithTaggedFlag("ac_present_L2H", 12)
                .WithTaggedFlag("ec_rst_l_L2H", 13)
                .WithTaggedFlag("flash_wp_l_L2H", 14)
                .WithReservedBits(15, 17);

            Registers.KeyInterruptDebounceControl.Define(this, 0x7d0)
                .WithTag("debounce_timer", 0, 16)
                .WithReservedBits(16, 16);

            Registers.AutoBlockDebounceControl.Define(this, 0x7d0)
                .WithTag("debounce_timer", 0, 16)
                .WithTaggedFlag("auto_block_enable", 16)
                .WithReservedBits(17, 15);

            Registers.AutoBlockOut.Define(this)
                .WithTaggedFlag("key0_out_sel", 0)
                .WithTaggedFlag("key1_out_sel", 1)
                .WithTaggedFlag("key2_out_sel", 2)
                .WithTaggedFlag("key0_out_value", 4)
                .WithTaggedFlag("key1_out_value", 5)
                .WithTaggedFlag("key2_out_value", 6)
                .WithReservedBits(7, 25);

            Registers.ComboSelectControl0.DefineMany(this, NumberOfCombos, (register, idx) =>
            {
                register
                    .WithEnumField<DoubleWordRegister, InputsState>(0, 5,
                        valueProviderCallback: _ => combosDefinition[idx].Inputs,
                        writeCallback: (_, val) => { combosDefinition[idx].Inputs = (InputsState)val; },
                        name: $"sel_{idx}")
                    .WithReservedBits(5, 27);
            });

            Registers.ComboDurationControl0.DefineMany(this, NumberOfCombos, (register, idx) =>
            {
                register
                    .WithValueField(0, 32,
                        writeCallback: (_, val) => { combosDefinition[idx].TriggerDurationInCycles = (uint)val; },
                        valueProviderCallback: (_) => combosDefinition[idx].TriggerDurationInCycles,
                        name: $"detection_timer_{idx}");
            });

            Registers.ComboOutControl0.DefineMany(this, NumberOfCombos, (register, idx) =>
            {
                register
                    .WithEnumField<DoubleWordRegister, ComboAction>(0, 4,
                        writeCallback: (_, val) => { combosDefinition[idx].Actions = val; },
                        valueProviderCallback: _ => combosDefinition[idx].Actions,
                        name: $"combo_out_{idx}")
                        .WithReservedBits(4, 28);
            });

            Registers.ComboInterruptStatus.Define(this)
                .WithTaggedFlag("combo0_H2L", 0)
                .WithTaggedFlag("combo1_H2L", 1)
                .WithTaggedFlag("combo2_H2L", 2)
                .WithTaggedFlag("combo3_H2L", 3)
                .WithReservedBits(4, 28);

            Registers.KeyInterruptStatus.Define(this)
                .WithTaggedFlag("pwrb_H2L", 0)
                .WithTaggedFlag("key0_in_H2L", 1)
                .WithTaggedFlag("key1_in_H2L", 2)
                .WithTaggedFlag("key2_in_H2L", 3)
                .WithTaggedFlag("ac_present_H2L", 4)
                .WithTaggedFlag("ec_rst_l_H2L", 5)
                .WithTaggedFlag("flash_wp_l_H2L", 6)
                .WithTaggedFlag("pwrb_L2H", 7)
                .WithTaggedFlag("key0_in_L2H", 8)
                .WithTaggedFlag("key1_in_L2H", 9)
                .WithTaggedFlag("key2_in_L2H", 10)
                .WithTaggedFlag("ac_present_L2H", 11)
                .WithTaggedFlag("ec_rst_l_L2H", 12)
                .WithTaggedFlag("flash_wp_l_L2H", 13)
                .WithReservedBits(14, 18);
        }

        private void CheckCombos()
        {
            var currentState = GetCurrentInputsState();

            foreach(var combo in combosDefinition.Where(x => x.Inputs == currentState))
            {
                switch(combo.Actions)
                {
                    case ComboAction.None:
                        this.Log(LogLevel.Warning, "No action set on combo, ignoring");
                        break;
                    case ComboAction.ResetRequest:
                        // Resets all except POR modules as stated in https://github.com/lowRISC/opentitan/issues/12288
                        durationTimer.ExecOnElapsed(ResetRequest, combo.TriggerDurationInCycles);
                        break;
                    default:
                        this.Log(LogLevel.Error, "The {0} action is not implemented. Currently only the reset request is supported");
                        break;
                }
                return;
            }
        }

        private void ResetRequest()
        {
            // Resets all except POR modules as stated in https://github.com/lowRISC/opentitan/issues/12288
            this.resetManager.LifeCycleReset();
        }

        private InputsState GetCurrentInputsState()
        {
            InputsState currentState = InputsState.None;
            if(powerButton ^ pwrButtonInvert.Value)
            {
                currentState |= InputsState.PowerButton;
            }
            if(key0Invert.Value)
            {
                currentState |= InputsState.Key0;
            }
            if(key1Invert.Value)
            {
                currentState |= InputsState.Key0;
            }
            if(key2Invert.Value)
            {
                currentState |= InputsState.Key2;
            }
            if(acPresentInvert.Value)
            {
                currentState |= InputsState.AcPresent;
            }

            return currentState;
        }

        private void UpdateInterrupt()
        {
            var interrupt = interruptEnable.Value && interruptState.Value;
            IRQ.Set(interrupt);
            this.DebugLog("IRQ set to {0}", interrupt);
        }

        private const uint NumberOfCombos = 4;
        private const int DurationTimerFrequency = 200000;
        private readonly OpenTitan_ResetManager resetManager;
        private readonly ComboDefinition[] combosDefinition;
        private readonly DurationTimer durationTimer;

        private IFlagRegisterField interruptState;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField key0Invert;
        private IFlagRegisterField key1Invert;
        private IFlagRegisterField key2Invert;
        private IFlagRegisterField acPresentInvert;
        private IFlagRegisterField pwrButtonInvert;
        private bool powerButton;

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            ConfigurationWriteEnable = 0x10,
            ECResetControl = 0x14,
            UltralowpowerACDebounceControl = 0x18,
            UltralowpowerLidDebounceControl = 0x1c,
            UltralowpowerPwrDebounceControl = 0x20,
            UltralowpowerControl = 0x24,
            UltralowpowerStatus = 0x28,
            WakeupStatus = 0x2c,
            KeyInputOutputInvert = 0x30,
            PinAllowedControl = 0x34,
            PinOutControl = 0x38,
            PinOverrideValue = 0x3c,
            PinInValue = 0x40,
            KeyInterruptControl = 0x44,
            KeyInterruptDebounceControl = 0x48,
            AutoBlockDebounceControl = 0x4c,
            AutoBlockOut = 0x50,
            ComboSelectControl0 = 0x54,
            ComboSelectControl1 = 0x58,
            ComboSelectControl2 = 0x5C,
            ComboSelectControl3 = 0x60,
            ComboDurationControl0 = 0x64,
            ComboDurationControl1 = 0x68,
            ComboDurationControl2 = 0x6C,
            ComboDurationControl3 = 0x70,
            ComboOutControl0 = 0x74,
            ComboOutControl1 = 0x78,
            ComboOutControl2 = 0x7C,
            ComboOutControl3 = 0x80,
            ComboInterruptStatus = 0x84,
            KeyInterruptStatus = 0x88,
        }

        [Flags]
        private enum InputsState
        {
            None = 0x0,
            Key0 = 0x1,
            Key1 = 0x2,
            Key2 = 0x4,
            PowerButton = 0x8,
            AcPresent = 0x10,
        }

        [Flags]
        private enum ComboAction
        {
            None = 0x0,
            BatteryDisable = 0x1,
            Interrupt = 0x2,
            EmbeddedControllerReset = 0x4,
            ResetRequest = 0x8,
        }

        private struct ComboDefinition
        {
            public ComboDefinition(InputsState inputs, ComboAction action, uint triggerDuration)
            {
                this.Inputs = inputs;
                this.Actions = action;
                this.TriggerDurationInCycles = triggerDuration;
            }

            public InputsState Inputs;
            public ComboAction Actions;
            public uint TriggerDurationInCycles;
        }

        private class DurationTimer: LimitTimer
        {
            public DurationTimer(IClockSource clockSource, long frequency, IPeripheral owner, string name)
                : base(clockSource, frequency, owner, name, limit: uint.MaxValue, direction: Direction.Ascending,
                       workMode: WorkMode.OneShot, enabled:false, eventEnabled: true, autoUpdate: true)
            {
               // Intentionally left blank
            }

            public void Cancel()
            {
                this.ClearSubscriptions();
                this.Enabled = false;
            }

            public void ExecOnElapsed(Action action, uint limitInCycles)
            {
                // In case we have something already set up - clean up
                Cancel();
                this.Limit = limitInCycles;
                this.LimitReached += action;
                this.Value = 0;
                this.Enabled = true;
            }
        }
    } // End class OpenTitan_SysrstCtrl
}
