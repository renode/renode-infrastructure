//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasDA14_GPT : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public RenesasDA14_GPT(IMachine machine, long lowPowerFrequency = DefaultLowPowerFrequency,
            bool extendedTimer = false) : base(machine)
        {
            this.lowPowerFrequency = lowPowerFrequency;
            this.extendedTimer = extendedTimer;

            timer = new LimitTimer(machine.ClockSource, lowPowerFrequency, this, "timer", FreeRunLimit, direction: Direction.Ascending, workMode: WorkMode.Periodic, eventEnabled: true);
            timer.LimitReached += () =>
            {
                var currentValue = timer.Limit;
                var triggerInterrupt = true;

                if(freeRunEnabled.Value && timer.Direction == Direction.Ascending && currentValue != FreeRunLimit)
                {
                    timer.Limit = FreeRunLimit;
                    timer.Value = currentValue;
                }
                else
                {
                    triggerInterrupt = currentValue != FreeRunLimit;
                    timer.Limit = timerLimit.Value;
                    timer.ResetValue();
                }

                interruptTriggered = triggerInterrupt;
                UpdateInterrupts();
            };

            IRQ = new GPIO();
            CaptureIRQ = new GPIO();

            connections = new GPIOConnection[extendedTimer ? ExtendedGPIOConnections : DefaultGPIOConnections];
            for(var i = 0; i < connections.Length; i++)
            {
                connections[i] = new GPIOConnection(this);
            }

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            interruptTriggered = false;
            foreach(var connection in connections)
            {
                connection.Reset();
            }
            
            timer.Reset();
            IRQ.Unset();
            CaptureIRQ.Unset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(!IsValidConnection(number))
            {
                return;
            }

            connections[number].SetValue(value);
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; }
        public GPIO CaptureIRQ { get; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out timerEnabled, name: "TIM_EN",
                    writeCallback: (_, __) => SetTimerEnabled())
                .WithTaggedFlag("TIM_ONESHOT_MODE_EN", 1)
                .WithFlag(2, name: "TIM_COUNT_DOWN_EN",
                    valueProviderCallback: _ => timer.Direction == Direction.Descending,
                    changeCallback: (_, value) => timer.Direction = value ? Direction.Descending : Direction.Ascending)
                .WithFlag(3, name: "TIM_IN1_EVENT_FALL_EN",
                    valueProviderCallback: _ => CallForConnection(0, con => con.UseFallingEdge),
                    writeCallback: (_, value) => CallForConnection(0, con => con.UseFallingEdge = value))
                .WithFlag(4, name: "TIM_IN2_EVENT_FALL_EN",
                    valueProviderCallback: _ => CallForConnection(1, con => con.UseFallingEdge),
                    writeCallback: (_, value) => CallForConnection(1, con => con.UseFallingEdge = value))
                .WithFlag(5, out interruptEnabled, name: "TIM_IRQ_EN",
                    writeCallback: (_, __) => UpdateInterrupts())
                .WithFlag(6, out freeRunEnabled, name: "TIM_FREE_RUN_MODE_EN")
                .WithFlag(7, name: "TIM_SYS_CLK_EN",
                    valueProviderCallback: _ => timer.Frequency == DivNClockFrequency,
                    changeCallback: (_, value) => timer.Frequency = value ? DivNClockFrequency : lowPowerFrequency)
                .WithFlag(8, out timerClockEnabled, name: "TIM_CLK_EN",
                    writeCallback: (_, __) => SetTimerEnabled())
                .If(extendedTimer)
                    .Then(r => r
                        .WithFlag(9, name: "TIM_IN3_EVENT_FALL_EN",
                            valueProviderCallback: _ => CallForConnection(2, con => con.UseFallingEdge),
                            writeCallback: (_, value) => CallForConnection(2, con => con.UseFallingEdge = value))
                        .WithFlag(10, name: "TIM_IN4_EVENT_FALL_EN",
                            valueProviderCallback: _ => CallForConnection(3, con => con.UseFallingEdge),
                            writeCallback: (_, value) => CallForConnection(3, con => con.UseFallingEdge = value))
                        .WithFlag(11, name: "TIM_CAP_GPIO1_IRQ_EN",
                            valueProviderCallback: _ => CallForConnection(0, con => con.InterruptEnabled),
                            writeCallback: (_, value) => CallForConnection(0, con => con.InterruptEnabled = value))
                        .WithFlag(12, name: "TIM_CAP_GPIO2_IRQ_EN",
                            valueProviderCallback: _ => CallForConnection(1, con => con.InterruptEnabled),
                            writeCallback: (_, value) => CallForConnection(1, con => con.InterruptEnabled = value))
                        .WithFlag(13, name: "TIM_CAP_GPIO3_IRQ_EN",
                            valueProviderCallback: _ => CallForConnection(2, con => con.InterruptEnabled),
                            writeCallback: (_, value) => CallForConnection(2, con => con.InterruptEnabled = value))
                        .WithFlag(14, name: "TIM_CAP_GPIO4_IRQ_EN",
                            valueProviderCallback: _ => CallForConnection(3, con => con.InterruptEnabled),
                            writeCallback: (_, value) => CallForConnection(3, con => con.InterruptEnabled = value))
                        .WithReservedBits(15, 17))
                    .Else(r => r.WithReservedBits(9, 23));

            Registers.CounterValue.Define(this)
                .WithValueField(0, 24, FieldMode.Read, name: "TIM_TIMER_VALUE",
                    valueProviderCallback: _ => GetTimerValue())
                .WithReservedBits(24, 8);

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TIM_IN1_STATE",
                    valueProviderCallback: _ => CallForConnection(0, con => con.Value))
                .WithFlag(1, FieldMode.Read, name: "TIM_IN2_STATE",
                    valueProviderCallback: _ => CallForConnection(1, con => con.Value))
                .WithTag("TIM_ONESHOT_PHASE", 2, 2)
                .If(extendedTimer)
                    .Then(r => r
                        .WithFlag(4, FieldMode.Read, name: "TIM_GPIO1_EVENT_PENDING",
                            valueProviderCallback: _ => CallForConnection(0, con => con.EventTriggered))
                        .WithFlag(5, FieldMode.Read, name: "TIM_GPIO2_EVENT_PENDING",
                            valueProviderCallback: _ => CallForConnection(1, con => con.EventTriggered))
                        .WithFlag(6, FieldMode.Read, name: "TIM_GPIO3_EVENT_PENDING",
                            valueProviderCallback: _ => CallForConnection(2, con => con.EventTriggered))
                        .WithFlag(7, FieldMode.Read, name: "TIM_GPIO4_EVENT_PENDING",
                            valueProviderCallback: _ => CallForConnection(3, con => con.EventTriggered)))
                    .Else(r => r.WithReservedBits(4, 4))
                .WithFlag(8, FieldMode.Read, name: "TIM_IRQ_STATUS",
                    valueProviderCallback: _ => interruptTriggered)
                .WithTaggedFlag("TIM_TIMER_BUSY", 9)
                .WithTaggedFlag("TIM_PWM_BUSY", 10)
                .WithFlag(11, FieldMode.Read, name: "TIM_SWITCHED_TO_DIVN_CLK",
                    valueProviderCallback: _ => timer.Frequency == DivNClockFrequency)
                .If(extendedTimer)
                    .Then(r => r
                        .WithFlag(12, FieldMode.Read, name: "TIM_IN3_STATE",
                            valueProviderCallback: _ => CallForConnection(2, con => con.Value))
                        .WithFlag(13, FieldMode.Read, name: "TIM_IN4_STATE",
                            valueProviderCallback: _ => CallForConnection(3, con => con.Value))
                        .WithReservedBits(14, 18))
                    .Else(r => r.WithReservedBits(12, 20));

            Registers.GPIO1Selection.Define(this)
                .WithTag("TIM_GPIO1_CONF", 0, 6)
                .WithReservedBits(6, 26);

            Registers.GPIO2Selection.Define(this)
                .WithTag("TIM_GPIO2_CONF", 0, 6)
                .WithReservedBits(6, 26);

            Registers.Settings.Define(this)
                .WithValueField(0, 24, out timerLimit, name: "TIM_RELOAD",
                    writeCallback: (_, value) =>
                    {
                        timer.Limit = value;
                        if(timer.Direction == Direction.Descending)
                        {
                            timer.ResetValue();
                        }
                    })
                .WithValueField(24, 5, name: "TIM_PRESCALER",
                    valueProviderCallback: _ => (ulong)timer.Divider - 1,
                    writeCallback: (_, value) => timer.Divider = (int)value + 1)
                .WithReservedBits(29, 3);

            Registers.ShotDuration.Define(this)
                .WithTag("TIM_SHOTWIDTH", 0, 24)
                .WithReservedBits(24, 8);

            Registers.EventValueGPIO1.Define(this)
                .WithValueField(0, 24, FieldMode.Read, name: "TIM_CAPTURE_GPIO1",
                    valueProviderCallback: _ => CallForConnection(0, con => con.CaptureTimestamp))
                .WithReservedBits(24, 8);

            Registers.EventValueGPIO2.Define(this)
                .WithValueField(0, 24, FieldMode.Read, name: "TIM_CAPTURE_GPIO2",
                    valueProviderCallback: _ => CallForConnection(1, con => con.CaptureTimestamp))
                .WithReservedBits(24, 8);

            Registers.Prescaler.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "TIM_PRESCALER_VAL",
                    valueProviderCallback: _ => (ulong)timer.Divider - 1)
                .WithReservedBits(5, 27);

            Registers.PWMControl.Define(this)
                .WithTag("TIM_PWM_FREQ", 0, 16)
                .WithTag("TIM_PWM_DC", 16, 16);

            if(extendedTimer)
            {
                Registers.GPIO3Selection.Define(this)
                    .WithTag("TIM_GPIO3_CONF", 0, 6)
                    .WithReservedBits(6, 26);

                Registers.GPIO4Selection.Define(this)
                    .WithTag("TIM_GPIO4_CONF", 0, 6)
                    .WithReservedBits(6, 26);

                Registers.EventValueGPIO3.Define(this)
                    .WithValueField(0, 24, FieldMode.Read, name: "TIM_CAPTURE_GPIO3",
                        valueProviderCallback: _ => CallForConnection(2, con => con.CaptureTimestamp))
                    .WithReservedBits(24, 8);

                Registers.EventValueGPIO4.Define(this)
                    .WithValueField(0, 24, FieldMode.Read, name: "TIM_CAPTURE_GPIO4",
                        valueProviderCallback: _ => CallForConnection(3, con => con.CaptureTimestamp))
                    .WithReservedBits(24, 8);

                Registers.GPIOEventClear.Define(this)
                    .WithFlag(0, FieldMode.Write, name: "TIM_CLEAR_GPIO1_EVENT",
                        writeCallback: (_, value) => CallForConnection(0, con => ClearGPIOEvent(con, value)))
                    .WithFlag(1, FieldMode.Write, name: "TIM_CLEAR_GPIO2_EVENT",
                        writeCallback: (_, value) => CallForConnection(1, con => ClearGPIOEvent(con, value)))
                    .WithFlag(2, FieldMode.Write, name: "TIM_CLEAR_GPIO3_EVENT",
                        writeCallback: (_, value) => CallForConnection(2, con => ClearGPIOEvent(con, value)))
                    .WithFlag(3, FieldMode.Write, name: "TIM_CLEAR_GPIO4_EVENT",
                        writeCallback: (_, value) => CallForConnection(3, con => ClearGPIOEvent(con, value)))
                    .WithReservedBits(4, 28);
            }

            (extendedTimer ? Registers.InterruptClearExtended : Registers.InterruptClear).Define(this)
                .WithFlag(0, FieldMode.Write, name: "TIM_CLEAR_IRQ",
                    writeCallback: (_, __) =>
                    {
                        interruptTriggered = false;
                        UpdateInterrupts();
                    })
                .WithReservedBits(1, 31);
        }

        private ulong GetTimerValue()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }

            return timer.Value;
        }

        private void UpdateInterrupts()
        {
            if(!timer.Enabled)
            {
                IRQ.Unset();
                CaptureIRQ.Unset();
                return;
            }

            var timerIrqValue = interruptEnabled.Value && interruptTriggered;
            this.DebugLog("{0} interrupt", timerIrqValue ? "Setting" : "Unsetting");
            IRQ.Set(timerIrqValue);

            if(extendedTimer)
            {
                var gpioIrqValue = false;
                foreach(var connection in connections)
                {
                    gpioIrqValue |= connection.InterruptEnabled && connection.EventTriggered;
                }

                this.DebugLog("{0} capture interrupt", gpioIrqValue ? "Setting" : "Unsetting");
                CaptureIRQ.Set(gpioIrqValue);
            }
            else
            {
                CaptureIRQ.Unset();
            }
        }

        private void SetTimerEnabled()
        {
            var enabled = timerEnabled.Value && timerClockEnabled.Value;
            timer.Enabled = enabled;
        }

        private void CallForConnection(int index, Action<GPIOConnection> callback, bool logMessage = false)
        {
            if(!IsValidConnection(index, logMessage))
            {
                return;
            }

            callback(connections[index]);
        }

        private T CallForConnection<T>(int index, Func<GPIOConnection, T> callback, bool logMessage = false)
        {
            if(!IsValidConnection(index, logMessage))
            {
                return default(T);
            }

            return callback(connections[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidConnection(int index, bool logMessage = true)
        {
            if(index < 0 || index >= connections.Length)
            {
                if(logMessage)
                {
                    this.WarningLog("GPIO connection {0} is out of range of [0;{1})", index, connections.Length);
                }

                return false;
            }
            return true;
        }

        private void ClearGPIOEvent(GPIOConnection connection, bool value)
        {
            if(!value)
            {
                return;
            }

            connection.EventTriggered = false;
            UpdateInterrupts();
        }

        private readonly LimitTimer timer;
        private readonly GPIOConnection[] connections;

        private readonly long lowPowerFrequency;
        private readonly bool extendedTimer;

        private bool interruptTriggered;

        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField timerEnabled;
        private IFlagRegisterField timerClockEnabled;
        private IFlagRegisterField freeRunEnabled;

        private IValueRegisterField timerLimit;

        private const long DefaultLowPowerFrequency = 32000;
        private const long DivNClockFrequency = 32000000;
        private const long FreeRunLimit = (1 << 24) - 1;

        private const int DefaultGPIOConnections = 2;
        private const int ExtendedGPIOConnections = 4;

        private enum Registers
        {
            Control                 = 0x00, // TIMER_CTRL_REG
            CounterValue            = 0x04, // TIMER_TIMER_VAL_REG
            Status                  = 0x08, // TIMER_STATUS_REG
            GPIO1Selection          = 0x0C, // TIMER_GPIO1_CONF_REG
            GPIO2Selection          = 0x10, // TIMER_GPIO2_CONF_REG
            Settings                = 0x14, // TIMER_SETTINGS_REG
            ShotDuration            = 0x18, // TIMER_SHOTWIDTH_REG
            // Gap
            EventValueGPIO1         = 0x20, // TIMER_CAPTURE_GPIO1_REG
            EventValueGPIO2         = 0x24, // TIMER_CAPTURE_GPIO2_REG
            Prescaler               = 0x28, // TIMER_PRESCALER_VAL_REG
            PWMControl              = 0x2C, // TIMER_PWM_CTRL_REG
            // Gap
            InterruptClear          = 0x34, // TIMER_CLEAR_IRQ_REG

            // Registers for extended timer version
            GPIO3Selection          = 0x34, // TIMER_GPIO3_CONF_REG
            GPIO4Selection          = 0x38, // TIMER_GPIO4_CONF_REG
            EventValueGPIO3         = 0x3C, // TIMER_CAPTURE_GPIO3_REG
            EventValueGPIO4         = 0x40, // TIMER_CAPTURE_GPIO4_REG
            GPIOEventClear          = 0x44, // TIMER_CLEAR_GPIO_EVENT_REG
            InterruptClearExtended  = 0x48, // TIMER_CLEAR_IRQ_REG
        }

        private class GPIOConnection
        {            
            public GPIOConnection(RenesasDA14_GPT owner)
            {
                this.owner = owner;
            }

            public void Reset()
            {
                Enabled = false;
                InterruptEnabled = false;
                EventTriggered = false;
                Value = false;
                UseFallingEdge = false;
                CaptureTimestamp = 0;
            }

            public void SetValue(bool value)
            {
                if(!Enabled)
                {
                    return;
                }

                if(UseFallingEdge)
                {
                    EventTriggered = Value && !value;
                }
                else
                {
                    EventTriggered = !Value && value;
                }

                Value = value;
                if(EventTriggered)
                {
                    CaptureTimestamp = owner.GetTimerValue();
                }
                owner.UpdateInterrupts();
            }

            public bool Enabled { get; set; }

            public bool EventTriggered { get; set; }

            public bool Value { get; private set; }
            public ulong CaptureTimestamp { get; private set; }

            public bool InterruptEnabled { get; set; }
            public bool UseFallingEdge { get; set; }

            private readonly RenesasDA14_GPT owner;
        }
    }
}
