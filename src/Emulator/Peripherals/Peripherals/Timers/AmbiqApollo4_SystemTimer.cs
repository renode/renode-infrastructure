//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class AmbiqApollo4_SystemTimer : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public AmbiqApollo4_SystemTimer(IMachine machine) : base(machine)
        {
            // Changing 'CLKSEL' (it's 'NOCLK' by default) is necessary to enable 'systemTimer'.
            systemTimer = new LimitTimer(machine.ClockSource, InvalidFrequency, this, "System Timer", uint.MaxValue,
                Direction.Ascending, enabled: false, workMode: WorkMode.Periodic, eventEnabled: true, autoUpdate: true, divider: 1);
            systemTimer.LimitReached += () => HandleLimitReached();

            for(var i = 0; i < interruptOutputs.Length; i++)
            {
                interruptOutputs[i] = new GPIO();
            }
            for(var i = 0; i < captureRegisters.Length; i++)
            {
                captureRegisters[i] = new CaptureRegister(this, i);
            }
            for(var i = 0; i < compareRegisters.Length; i++)
            {
                compareRegisters[i] = new CompareRegister(this, i, interruptOutputs[i], systemTimer);
            }

            DefineRegisters();
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Debug, "GPIO#{0} {1}", number, value ? "set" : "unset");
            foreach(var captureRegister in captureRegisters)
            {
                captureRegister.OnGPIO(number, value);
            }
        }

        public override void Reset()
        {
            Array.ForEach(captureRegisters, register => register.Reset());
            Array.ForEach(compareRegisters, register => register.Reset());
            // All but IRQI should be reset with CompareRegister.Reset; nevertheless, let's unset all IRQs.
            Array.ForEach(interruptOutputs, irq => irq.Unset());
            systemTimer.Reset();

            base.Reset();
        }

        public long Frequency => systemTimer.Frequency;

        // Comparator IRQs
        public GPIO IRQA => interruptOutputs[0];
        public GPIO IRQB => interruptOutputs[1];
        public GPIO IRQC => interruptOutputs[2];
        public GPIO IRQD => interruptOutputs[3];
        public GPIO IRQE => interruptOutputs[4];
        public GPIO IRQF => interruptOutputs[5];
        public GPIO IRQG => interruptOutputs[6];
        public GPIO IRQH => interruptOutputs[7];

        // Capture + overflow event IRQ
        public GPIO IRQI => interruptOutputs[8];

        public long Size => 0x110;

        public uint Value
        {
            get
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    // being here means we are on the CPU thread
                    cpu.SyncTime();
                }
                return (uint)systemTimer.Value;
            }
        }

        private void DefineRegisters()
        {
            Registers.Capture0.DefineMany(this, 4,
                (register, registerIndex) =>
                {
                    register.WithValueField(0, 32, FieldMode.Read, name: $"SCAPT{registerIndex}",
                        valueProviderCallback: _ => captureRegisters[registerIndex].ValueCaptured);
                }, stepInBytes: 4);

            Registers.CaptureControl0.DefineMany(this, 4,
                (register, registerIndex) =>
                {
                    var captureRegister = captureRegisters[registerIndex];
                    register.WithValueField(0, 7, out captureRegister.TriggerSourceGPIOPinNumber, name: $"STSEL{registerIndex}")
                        .WithReservedBits(7, 1)
                        .WithFlag(8, out captureRegister.CaptureOnHighToLowGPIOTransition, name: $"STPOL{registerIndex}")
                        .WithFlag(9, out captureRegister.Enabled, name: $"CAPTURE{registerIndex}")
                        .WithReservedBits(10, 22)
                        ;
                }, stepInBytes: 4, resetValue: 0x7F);

            Registers.Compare0.DefineMany(this, 8,
                (register, registerIndex) =>
                {
                    register.WithValueField(0, 32, name: $"SCMPR{registerIndex}",
                        // SCMPR value written is relative to the current COUNTER (systemTimer's Value).
                        writeCallback: (_, newValue) =>
                        {
                            // Ambiq HAL does a Compare delta adjustment:
                            // on HW it takes 2 clock cycles for writes to this register to be effective
                            // and the interrupt itself is delayed by 1.
                            // Hence the timer incrementation.
                            if((compareRegisters[registerIndex].CompareValue - (uint)newValue) > 3)
                            {
                                systemTimer.Increment(3);
                            }
                            compareRegisters[registerIndex].CompareValue = Value + (uint)newValue;
                        },
                        valueProviderCallback: _ => compareRegisters[registerIndex].CompareValue);
                }, stepInBytes: 4);

            Registers.Configuration.Define(this, 0x80000000)
                .WithEnumField(0, 4, out clockSelect, name: "CLKSEL", changeCallback: (_, __) => UpdateFrequency())
                .WithReservedBits(4, 4)
                .WithFlags(8, 8, name: "COMPARExEN", changeCallback: (registerIndex, _, newValue) => compareRegisters[registerIndex].Enabled = newValue)
                .WithReservedBits(16, 14)
                .WithFlag(30, out clear, name: "CLEAR", changeCallback: (_, __) => UpdateSystemTimerState())
                .WithFlag(31, out freeze, name: "FREEZE", changeCallback: (_, __) => UpdateSystemTimerState())
                ;

            Registers.InterruptClear.Define(this)
                .WithFlags(0, 8, FieldMode.Write, name: "COMPAREx",
                    writeCallback: (registerIndex, _, newValue) => { if(newValue) compareRegisters[registerIndex].InterruptStatus = false; })
                .WithFlag(8, out overflowInterruptStatus, FieldMode.WriteOneToClear, name: "OVERFLOW",
                    changeCallback: (_, __) => UpdateCaptureOverflowIRQ())
                .WithFlags(9, 4, FieldMode.Write, name: "CAPTUREx",
                    writeCallback: (registerIndex, _, newValue) => { if(newValue) captureRegisters[registerIndex].InterruptStatus = false; })
                .WithReservedBits(13, 19)
                ;

            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 8, name: "COMPAREx",
                    changeCallback: (registerIndex, _, newValue) => compareRegisters[registerIndex].InterruptEnable = newValue,
                    valueProviderCallback: (registerIndex, _) => compareRegisters[registerIndex].InterruptEnable)
                .WithFlag(8, out overflowInterruptEnable, name: "OVERFLOW",
                    changeCallback: (_, __) => UpdateCaptureOverflowIRQ())
                .WithFlags(9, 4, name: "CAPTUREx",
                    changeCallback: (registerIdx, _, newValue) => captureRegisters[registerIdx].InterruptEnable = newValue,
                    valueProviderCallback: (registerIdx, _) => captureRegisters[registerIdx].InterruptEnable)
                .WithReservedBits(13, 19)
                ;

            Registers.InterruptSet.Define(this)
                .WithFlags(0, 8, FieldMode.Write, name: "COMPAREx",
                    writeCallback: (registerIndex, _, newValue) => { if(newValue) compareRegisters[registerIndex].InterruptStatus = true; })
                .WithFlag(8, out overflowInterruptStatus, FieldMode.Set, name: "OVERFLOW",
                    changeCallback: (_, __) => UpdateCaptureOverflowIRQ())
                .WithFlags(9, 4, FieldMode.Write, name: "CAPTUREx",
                    writeCallback: (registerIndex, _, newValue) => { if(newValue) captureRegisters[registerIndex].InterruptStatus = true; })
                .WithReservedBits(13, 19)
                ;

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 8, FieldMode.Read, name: "COMPAREx",
                    valueProviderCallback: (registerIndex, _) => compareRegisters[registerIndex].InterruptStatus)
                .WithFlag(8, out overflowInterruptStatus, FieldMode.Read, name: "OVERFLOW")
                .WithFlags(9, 4, FieldMode.Read, name: "CAPTUREx",
                    valueProviderCallback: (registerIdx, _) => captureRegisters[registerIdx].InterruptStatus)
                .WithReservedBits(13, 19)
                ;

            Registers.SystemTimerCount.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "STTMR", valueProviderCallback: _ => Value)
                ;
        }

        private void HandleLimitReached()
        {
            this.Log(LogLevel.Debug, "COUNTER overflow occurred");
            overflowInterruptStatus.Value = true;
            UpdateCaptureOverflowIRQ();
        }

        private void UpdateCaptureOverflowIRQ()
        {
            var newIrqState = false;
            foreach(var captureRegister in captureRegisters)
            {
                if(captureRegister.InterruptEnable && captureRegister.InterruptStatus)
                {
                    newIrqState = true;
                    break;
                }
            }
            if(!newIrqState && overflowInterruptStatus.Value && overflowInterruptEnable.Value)
            {
                newIrqState = true;
            }

            if(IRQI.IsSet != newIrqState)
            {
                this.Log(LogLevel.Debug, "IRQI {0}", newIrqState ? "set" : "unset");
                IRQI.Set(newIrqState);
            }
        }

        private void UpdateFrequency()
        {
            var frequencySet = InvalidFrequency;
            const long KHz = 1000;
            switch(clockSelect.Value)
            {
                case ClockSelectValues.NOCLK:
                    this.Log(LogLevel.Debug, "CLKSEL set to NOCLK. Timer will be disabled.");
                    break;
                case ClockSelectValues.HFRC_6MHZ:
                    frequencySet = 6 * 1000 * KHz;
                    break;
                case ClockSelectValues.HFRC_375KHZ:
                    frequencySet = 375 * KHz;
                    break;
                case ClockSelectValues.XTAL_32KHZ:
                    frequencySet = 32 * KHz;
                    break;
                case ClockSelectValues.XTAL_16KHZ:
                    frequencySet = 16 * KHz;
                    break;
                case ClockSelectValues.XTAL_1KHZ:
                case ClockSelectValues.LFRC_1KHZ:
                    frequencySet = 1 * KHz;
                    break;
                case ClockSelectValues.CTIMER0:
                case ClockSelectValues.CTIMER1:
                    this.Log(LogLevel.Warning, "Unsupported CLKSEL value: {0}", clockSelect.Value);
                    break;
                default:
                    this.Log(LogLevel.Error, "Invalid CLKSEL value: 0x{0:X}", (uint)clockSelect.Value);
                    break;
            }

            if(frequencySet != InvalidFrequency)
            {
                this.Log(LogLevel.Debug, "Updating timer's frequency to {0} KHz; CLKSEL={1} ({2})", frequencySet / KHz, (uint)clockSelect.Value, clockSelect.Value);
                systemTimer.Frequency = frequencySet;
                Array.ForEach(compareRegisters, register => register.Frequency = frequencySet);
            }
            // CLKSEL influences the timer state depending on whether the frequency is valid or not.
            UpdateSystemTimerState();
        }

        private void UpdateSystemTimerState()
        {
            systemTimer.Enabled = !freeze.Value && !clear.Value && systemTimer.Frequency != InvalidFrequency;
            if(clear.Value)
            {
                systemTimer.ResetValue();
            }
            Array.ForEach(compareRegisters, register => register.UpdateState());
        }

        private IFlagRegisterField clear;
        private IEnumRegisterField<ClockSelectValues> clockSelect;
        private IFlagRegisterField freeze;
        private IFlagRegisterField overflowInterruptEnable;
        private IFlagRegisterField overflowInterruptStatus;

        private readonly CaptureRegister[] captureRegisters = new CaptureRegister[CaptureRegistersCount];
        private readonly CompareRegister[] compareRegisters = new CompareRegister[CompareRegistersCount];
        private readonly GPIO[] interruptOutputs = new GPIO[InterruptOutputsCount];
        private readonly LimitTimer systemTimer;

        private const uint CompareRegistersCount = 8;
        private const uint CaptureRegistersCount = 4;
        private const uint InterruptOutputsCount = 9;
        // It's used for CLKSEL options which stop the timer. 0 can't be set as the timer's frequency, hence 1.
        private const long InvalidFrequency = 1;

        private class CaptureRegister
        {
            public CaptureRegister(AmbiqApollo4_SystemTimer owner, int index)
            {
                this.owner = owner;
                var nameSuffix = (char)('A' + index);
                name = $"CAPTURE_{nameSuffix}";
            }

            public void OnGPIO(int number, bool high)
            {
                if(Enabled.Value
                        && (int)TriggerSourceGPIOPinNumber.Value == number
                        // The 'value' is 'true' here if the opposite (low to high) transition has just occurred.
                        && CaptureOnHighToLowGPIOTransition.Value != high)
                {
                    ValueCaptured = owner.Value;
                    InterruptStatus = true;
                    owner.Log(LogLevel.Debug, "{0}: Register set with value: 0x{1:X}", name, ValueCaptured);
                }
            }

            public void Reset()
            {
                interruptEnable = false;
                interruptStatus = false;
                ValueCaptured = 0;
            }

            public bool InterruptEnable
            {
                get => interruptEnable;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting Interrupt Enable to: {1}", name, value);
                    interruptEnable = value;
                    owner.UpdateCaptureOverflowIRQ();
                }
            }

            public bool InterruptStatus
            {
                get => interruptStatus;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting Interrupt Status to: {1}", name, value);
                    interruptStatus = value;
                    owner.UpdateCaptureOverflowIRQ();
                }
            }

            public IFlagRegisterField CaptureOnHighToLowGPIOTransition;
            public IFlagRegisterField Enabled;
            public IValueRegisterField TriggerSourceGPIOPinNumber;
            public uint ValueCaptured;

            private bool interruptEnable;
            private bool interruptStatus;

            private readonly string name;
            private readonly AmbiqApollo4_SystemTimer owner;
        }

        private class CompareRegister
        {
            public CompareRegister(AmbiqApollo4_SystemTimer owner, int index, GPIO irq, LimitTimer systemTimer)
            {
                this.irq = irq;
                this.owner = owner;
                this.systemTimer = systemTimer;
                var nameSuffix = (char)('A' + index);
                name = $"COMPARE_{nameSuffix}";

                innerTimer = new ComparingTimer(owner.machine.ClockSource, owner.Frequency, owner, name, direction: Direction.Ascending, limit: uint.MaxValue,
                        enabled: false, workMode: WorkMode.Periodic, eventEnabled: true, compare: 0, divider: 1);
                innerTimer.CompareReached += () =>
                {
                    owner.Log(LogLevel.Debug, "{0}: Compare value (0x{1:X}) reached", name, innerTimer.Compare);
                    InterruptStatus = true;
                };
            }

            public void Reset()
            {
                enabled = false;
                innerTimer.Reset();
                interruptEnable = false;
                interruptStatus = false;
                irq.Unset();
            }

            public void UpdateState()
            {
                if(enabled && systemTimer.Enabled)
                {
                    innerTimer.Value = owner.Value;
                    innerTimer.Enabled = true;
                }
                else
                {
                    innerTimer.Enabled = false;
                }
            }

            public uint CompareValue
            {
                get => (uint)innerTimer.Compare;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting compare value to: 0x{1:X}", name, value);
                    innerTimer.Compare = value;
                }
            }

            public bool Enabled
            {
                get => enabled;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting Enabled to: {1}", name, value);
                    enabled = value;
                    UpdateState();
                }
            }

            public long Frequency
            {
                get => innerTimer.Frequency;
                set => innerTimer.Frequency = value;
            }

            public bool InterruptEnable
            {
                get => interruptEnable;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting interrupt enable to: {1}", name, value);
                    interruptEnable = value;
                    UpdateIRQ();
                }
            }

            public bool InterruptStatus
            {
                get => interruptStatus;
                set
                {
                    owner.Log(LogLevel.Noisy, "{0}: Setting interrupt status to: {1}", name, value);
                    interruptStatus = value;
                    UpdateIRQ();
                }
            }

            private void UpdateIRQ()
            {
                var newIrqState = interruptEnable && interruptStatus;
                if(irq.IsSet != newIrqState)
                {
                    owner.Log(LogLevel.Debug, "{0}: {1} IRQ", name, newIrqState ? "Setting" : "Clearing");
                    irq.Set(newIrqState);
                }
            }

            private bool enabled;
            private bool interruptEnable;
            private bool interruptStatus;

            private readonly ComparingTimer innerTimer;
            private readonly GPIO irq;
            private readonly string name;
            private readonly AmbiqApollo4_SystemTimer owner;
            private readonly LimitTimer systemTimer;
        }

        private enum ClockSelectValues
        {
            NOCLK,
            HFRC_6MHZ,
            HFRC_375KHZ,
            XTAL_32KHZ,
            XTAL_16KHZ,
            XTAL_1KHZ,
            LFRC_1KHZ,
            CTIMER0,
            CTIMER1,
        }

        private enum Registers : long
        {
            Configuration = 0x0,
            SystemTimerCount = 0x4,
            CaptureControl0 = 0x10,
            CaptureControl1 = 0x14,
            CaptureControl2 = 0x18,
            CaptureControl3 = 0x1C,
            Compare0 = 0x20,
            Compare1 = 0x24,
            Compare2 = 0x28,
            Compare3 = 0x2C,
            Compare4 = 0x30,
            Compare5 = 0x34,
            Compare6 = 0x38,
            Compare7 = 0x3C,
            Capture0 = 0x40,
            Capture1 = 0x44,
            Capture2 = 0x48,
            Capture3 = 0x4C,
            SystemTimerNVRAM0 = 0x50,
            SystemTimerNVRAM1 = 0x54,
            SystemTimerNVRAM2 = 0x58,
            InterruptEnable = 0x100,
            InterruptStatus = 0x104,
            InterruptClear = 0x108,
            InterruptSet = 0x10C,
        }
    }
}
