//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
// NRF52840 GPIOTE (GPIO Tasks and Events) Implementation
//
// This implementation follows the nRF52840 GPIOTE specification:
// https://docs.nordicsemi.com/bundle/ps_nrf52840/page/gpiote.html
//
// Key features implemented:
// - 8 configurable GPIOTE channels for tasks and events
// - EVENTS_IN[0..7] for individual pin event detection
// - EVENTS_PORT for collective pin detection via DETECT signal
// - INTENSET/INTENCLR registers (spec-compliant naming)
// - LATENCY register for low-power mode support
// - PPI integration for event triggering
// - Proper polarity handling (LoToHi, HiToLo, Toggle, None)
// - Low-power mode with PIN_CNF.SENSE coordination
//
// Register Map:
// 0x000-0x01C: TASKS_OUT[0..7]
// 0x030-0x04C: TASKS_SET[0..7] 
// 0x060-0x07C: TASKS_CLR[0..7]
// 0x100-0x11C: EVENTS_IN[0..7]
// 0x17C:       EVENTS_PORT
// 0x304:       INTENSET
// 0x308:       INTENCLR
// 0x30C:       LATENCY
// 0x510-0x52C: CONFIG[0..7]

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class NRF52840_GPIOTasksEvents : BasicDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_GPIOTasksEvents(IMachine machine, NRF52840_GPIO port0 = null, NRF52840_GPIO port1 = null) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();

            ports = new[] { port0, port1 };
            if (port0 != null)
            {
                port0.PinChanged += OnPinChanged;
                port0.Detect += OnDetect;
            }
            if (port1 != null)
            {
                port1.PinChanged += OnPinChanged;
                port1.Detect += OnDetect;
            }

            pinToChannelMapping = new Dictionary<NRF52840_GPIO.Pin, Channel>();
            channels = new Channel[NumberOfChannels];
            for (var i = 0; i < channels.Length; i++)
            {
                channels[i] = new Channel(i, this);
            }

            lowPowerMode = false;
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var ch in channels)
            {
                ch.Reset();
            }
            pinToChannelMapping.Clear();
            lowPowerMode = false;

            UpdateInterrupt();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public event Action<uint> EventTriggered;

        private void DefineRegisters()
        {
            Registers.TasksOut.DefineMany(this, NumberOfChannels, (register, idx) =>
            {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_OUT",
                        writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Debug, "TASKS_OUT write: channel #{0}, value={1}", idx, val);
                            if (val) channels[idx].WritePin(val);
                        })
                    .WithReservedBits(1, 31);
            });

            Registers.TasksSet.DefineMany(this, NumberOfChannels, (register, idx) =>
            {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_SET",
                        writeCallback: (_, val) => { if (val) channels[idx].SetPin(); })
                    .WithReservedBits(1, 31);
            });

            Registers.TasksClear.DefineMany(this, NumberOfChannels, (register, idx) =>
            {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_CLR",
                        writeCallback: (_, val) => { if (val) channels[idx].ClearPin(); })
                    .WithReservedBits(1, 31);
            });

            Registers.EventsIn.DefineMany(this, NumberOfChannels, (register, idx) =>
            {
                register
                    .WithFlag(0, name: "EVENTS_IN",
                        valueProviderCallback: _ => channels[idx].EventPending,
                        writeCallback: (_, val) => channels[idx].EventPending = val)
                    .WithReservedBits(1, 31)
                    .WithWriteCallback((_, __) => UpdateInterrupt());
            });

            Registers.EventsPort.Define(this)
                .WithFlag(0, out portInterruptPending)
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.IntensitySet.Define(this)
                .WithFlags(0, 8, name: "IN",
                        valueProviderCallback: (idx, _) => channels[idx].EventEnabled,
                        writeCallback: (idx, _, val) => { if (val) channels[idx].EventEnabled = true; })
                .WithReservedBits(8, 23)
                .WithFlag(31, out portInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "PORT")
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.IntensityClear.Define(this)
                .WithFlags(0, 8, name: "IN",
                        valueProviderCallback: (idx, _) => channels[idx].EventEnabled,
                        writeCallback: (idx, _, val) => { if (val) channels[idx].EventEnabled = false; })
                .WithReservedBits(8, 23)
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "PORT",
                    valueProviderCallback: _ => portInterruptEnabled.Value,
                    writeCallback: (_, val) => { if (val) portInterruptEnabled.Value = false; })
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.Latency.Define(this)
                .WithFlag(0, out lowPowerModeFlag, name: "LATENCY")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) =>
                {
                    lowPowerMode = lowPowerModeFlag.Value;
                    this.Log(LogLevel.Debug, "GPIOTE latency mode set to: {0}", lowPowerMode ? "LowPower" : "HighFreq");
                })
            ;

            Registers.Configuration.DefineMany(this, NumberOfChannels, (register, idx) =>
            {
                register
                    .WithEnumField<DoubleWordRegister, Mode>(0, 2, name: "MODE",
                        valueProviderCallback: _ => channels[idx].Mode,
                        writeCallback: (_, val) => channels[idx].Mode = val)
                    .WithReservedBits(2, 6)
                    .WithValueField(8, 5, name: "PSEL",
                        valueProviderCallback: _ => channels[idx].SelectedPin,
                        writeCallback: (_, val) => channels[idx].SelectedPin = (uint)val)
                    .WithValueField(13, 1, name: "PORT",
                        valueProviderCallback: _ => channels[idx].SelectedPort,
                        writeCallback: (_, val) => channels[idx].SelectedPort = (uint)val)
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, Polarity>(16, 2, name: "POLARITY",
                        valueProviderCallback: _ => channels[idx].Polarity,
                        writeCallback: (_, val) => channels[idx].Polarity = val)
                    .WithReservedBits(18, 2)
                    .WithFlag(20, name: "OUTINIT",
                        valueProviderCallback: _ => channels[idx].CurrentState,
                        writeCallback: (_, val) => channels[idx].CurrentState = val)
                    .WithReservedBits(21, 11)
                    .WithWriteCallback((_, __) =>
                    {
                        pinToChannelMapping.Clear();

                        // In low-power mode, coordinate with PIN_CNF.SENSE field
                        if (lowPowerMode && channels[idx].Mode == Mode.Event)
                        {
                            ConfigureLowPowerSense(channels[idx]);
                        }

                        UpdateInterrupt();
                    });
            });
        }

        private bool TryGetChannel(NRF52840_GPIO.Pin pin, out Channel channel)
        {
            if (pinToChannelMapping.TryGetValue(pin, out channel))
            {
                return true;
            }

            foreach (var ch in channels)
            {
                if (pin.Id == ch.SelectedPin && pin.Parent == ports[ch.SelectedPort])
                {
                    pinToChannelMapping[pin] = ch;
                    channel = ch;
                    return true;
                }
            }

            channel = null;
            return false;
        }

        private void OnPinChanged(NRF52840_GPIO.Pin pin, bool value)
        {
            if ((pin.Direction != NRF52840_GPIO.PinDirection.Input && !pin.InputOverride)
               || !TryGetChannel(pin, out var channel))
            {
                return;
            }

            if (channel.Mode != Mode.Event)
            {
                channel.CurrentState = value;
                return;
            }

            // In low-power mode, POLARITY=Toggle is not supported
            if (lowPowerMode && channel.Polarity == Polarity.Toggle)
            {
                this.Log(LogLevel.Warning, "POLARITY=Toggle not supported in low-power mode for channel {0}", channel.Id);
                return;
            }

            bool eventShouldTrigger = false;

            switch (channel.Polarity)
            {
                case Polarity.None:
                    eventShouldTrigger = false;
                    break;

                case Polarity.LoToHi:
                    eventShouldTrigger = !channel.CurrentState && value;
                    break;

                case Polarity.HiToLo:
                    eventShouldTrigger = channel.CurrentState && !value;
                    break;

                case Polarity.Toggle:
                    eventShouldTrigger = channel.CurrentState != value;
                    break;
            }

            channel.CurrentState = value;

            // Set event pending if the condition is met
            if (eventShouldTrigger)
            {
                channel.EventPending = true;
                TriggerEvent(channel.Id);
                this.Log(LogLevel.Debug, "GPIOTE channel {0} event triggered (pin {1}.{2}, polarity {3})",
                    channel.Id, channel.SelectedPort, channel.SelectedPin, channel.Polarity);
            }

            UpdateInterrupt();
        }

        private void OnDetect()
        {
            portInterruptPending.Value = true;

            // Read LATCH register from GPIO ports to determine which pins triggered
            var latchedPins = new List<(int port, int pin)>();
            for (int portIdx = 0; portIdx < ports.Length; portIdx++)
            {
                if (ports[portIdx] != null)
                {
                    // In a real implementation, we would read the LATCH register
                    // For now, we assume the DETECT was triggered by sense-enabled pins
                    for (int pinIdx = 0; pinIdx < ports[portIdx].Pins.Length; pinIdx++)
                    {
                        var pin = ports[portIdx].Pins[pinIdx];
                        if (pin.IsSensing)
                        {
                            latchedPins.Add((portIdx, pinIdx));
                        }
                    }
                }
            }

            this.Log(LogLevel.Debug, "PORT event triggered by {0} pins", latchedPins.Count);

            // Trigger PORT event for PPI
            EventTriggered?.Invoke((uint)Registers.EventsPort);
            UpdateInterrupt();
        }

        private void TriggerEvent(int channelId)
        {
            // Trigger individual channel event for PPI
            // Events are at base address 0x100 + channelId * 4
            uint eventOffset = (uint)Registers.EventsIn + (uint)(channelId * 4);
            EventTriggered?.Invoke(eventOffset);
        }

        private void ConfigureLowPowerSense(Channel channel)
        {
            // In low-power mode, configure PIN_CNF.SENSE according to CONFIG.POLARITY
            if (!TryGetPinFromChannel(channel, out var pin))
            {
                return;
            }

            NRF52840_GPIO.SenseMode requiredSenseMode;
            switch (channel.Polarity)
            {
                case Polarity.LoToHi:
                    requiredSenseMode = NRF52840_GPIO.SenseMode.High;
                    break;
                case Polarity.HiToLo:
                    requiredSenseMode = NRF52840_GPIO.SenseMode.Low;
                    break;
                case Polarity.Toggle:
                    requiredSenseMode = NRF52840_GPIO.SenseMode.Disabled; // Not supported in low-power
                    break;
                case Polarity.None:
                    requiredSenseMode = NRF52840_GPIO.SenseMode.Disabled;
                    break;
                default:
                    requiredSenseMode = NRF52840_GPIO.SenseMode.Disabled;
                    break;
            }

            if (pin.SenseMode != requiredSenseMode)
            {
                this.Log(LogLevel.Debug, "Configuring pin {0}.{1} SENSE mode to {2} for low-power GPIOTE",
                    channel.SelectedPort, channel.SelectedPin, requiredSenseMode);
                pin.SenseMode = requiredSenseMode;
            }
        }

        private bool TryGetPinFromChannel(Channel channel, out NRF52840_GPIO.Pin pin)
        {
            var port = ports[channel.SelectedPort];
            if (port == null || channel.SelectedPin >= port.Pins.Length)
            {
                pin = null;
                return false;
            }

            pin = port.Pins[channel.SelectedPin];
            return true;
        }

        private void UpdateInterrupt()
        {
            var channelInterruptFlag = false;
            var portInterruptFlag = false;

            // Check individual channel interrupts
            foreach (var ch in channels)
            {
                if (ch.EventPending && ch.EventEnabled)
                {
                    channelInterruptFlag = true;
                    this.Log(LogLevel.Noisy, "Channel {0} interrupt pending", ch.Id);
                }
            }

            // Check PORT interrupt
            portInterruptFlag = portInterruptPending.Value && portInterruptEnabled.Value;
            if (portInterruptFlag)
            {
                this.Log(LogLevel.Noisy, "PORT interrupt pending");
            }

            var finalInterruptFlag = channelInterruptFlag || portInterruptFlag;

            this.Log(LogLevel.Noisy, "GPIOTE IRQ: channels={0}, port={1}, final={2}",
                channelInterruptFlag, portInterruptFlag, finalInterruptFlag);
            IRQ.Set(finalInterruptFlag);
        }

        private readonly uint NumberOfChannels = 8;

        private IFlagRegisterField portInterruptPending;
        private IFlagRegisterField portInterruptEnabled;
        private IFlagRegisterField lowPowerModeFlag;
        private bool lowPowerMode;

        private readonly Dictionary<NRF52840_GPIO.Pin, Channel> pinToChannelMapping;
        private readonly Channel[] channels;
        private readonly NRF52840_GPIO[] ports;

        private class Channel
        {
            public Channel(int id, NRF52840_GPIOTasksEvents parent)
            {
                this.id = id;
                this.parent = parent;
            }

            public void WritePin(bool value)
            {
                switch (Polarity)
                {
                    case Polarity.None:
                        break;

                    case Polarity.LoToHi:
                        parent.Log(LogLevel.Debug, "WritePin: LoToHi, channel #{0}", id);
                        WritePinInner(true);
                        break;

                    case Polarity.HiToLo:
                        parent.Log(LogLevel.Debug, "WritePin: HiToLo, channel #{0}", id);
                        WritePinInner(false);
                        break;

                    case Polarity.Toggle:
                        parent.Log(LogLevel.Debug, "WritePin: Toggle, channel #{0}", id);
                        WritePinInner(toggle: true);
                        break;
                }
            }

            public void SetPin()
            {
                parent.Log(LogLevel.Debug, "SetPin, channel #{0}", id);
                WritePinInner(true);
            }

            public void ClearPin()
            {
                parent.Log(LogLevel.Debug, "ClearPin, channel #{0}", id);
                WritePinInner(false);
            }

            public void Reset()
            {
                Mode = Mode.Disabled;
                SelectedPin = 0;
                SelectedPort = 0;
                Polarity = Polarity.None;
                CurrentState = false;

                EventEnabled = false;
                EventPending = false;
            }

            private bool TryGetPin(out NRF52840_GPIO.Pin pin)
            {
                var port = parent.ports[SelectedPort];
                if (port == null)
                {
                    parent.Log(LogLevel.Warning, "Trying to access a not connected port #{0}", SelectedPort);
                    pin = null;
                    return false;
                }

                if (SelectedPin >= port.Pins.Length)
                {
                    parent.Log(LogLevel.Warning, "Trying to access a not existing pin #{0} in port #{1}", SelectedPin, SelectedPort);
                    pin = null;
                    return false;
                }

                pin = port.Pins[SelectedPin];
                return true;
            }

            private void WritePinInner(bool value = false, bool toggle = false)
            {
                if (Mode != Mode.Task)
                {
                    parent.Log(LogLevel.Warning, "Setting channel #{0} not configured as TASK. Stack trace:\n{1}", id, Environment.StackTrace);
                    return;
                }

                if (!TryGetPin(out var pin))
                {
                    return;
                }

                pin.Direction = NRF52840_GPIO.PinDirection.Output;
                pin.OutputValue = toggle
                    ? !pin.OutputValue
                    : value;
            }

            public Mode Mode { get; set; }

            public uint SelectedPin { get; set; }
            public uint SelectedPort { get; set; }

            public Polarity Polarity { get; set; }

            public bool CurrentState { get; set; }

            public bool EventPending { get; set; }
            public bool EventEnabled { get; set; }

            public int Id => id;
            private readonly int id;
            private readonly NRF52840_GPIOTasksEvents parent;
        }

        private enum Mode
        {
            Disabled = 0,
            Event = 1,
            Task = 3
        }

        private enum Polarity
        {
            None = 0,
            LoToHi = 1,
            HiToLo = 2,
            Toggle = 3
        }

        private enum Registers
        {
            // Tasks - this is a group of 8 registers
            TasksOut = 0x0,
            // this is a group of 8 registers
            TasksSet = 0x30,
            // this is a group of 8 registers
            TasksClear = 0x60,

            // Events - this is a group of 8 registers
            EventsIn = 0x100,
            EventsPort = 0x17C,

            // Interrupt control
            IntensitySet = 0x304,    // INTENSET
            IntensityClear = 0x308,  // INTENCLR

            // Power management
            Latency = 0x30C,         // LATENCY

            // Configuration - this is a group of 8 registers
            Configuration = 0x510
        }
    }
}
