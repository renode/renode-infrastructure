//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class NRF52840_GPIOTasksEvents : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_GPIOTasksEvents(IMachine machine, NRF52840_GPIO port0 = null, NRF52840_GPIO port1 = null) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();

            ports = new [] { port0, port1 };
            if(port0 != null)
            {
                port0.PinChanged += OnPinChanged;
                port0.Detect += OnDetect;
            }
            if(port1 != null)
            {
                port1.PinChanged += OnPinChanged;
                port1.Detect += OnDetect;
            }

            pinToChannelMapping = new Dictionary<NRF52840_GPIO.Pin, Channel>();
            channels = new Channel[NumberOfChannels];
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i] = new Channel(i, this);
            }
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var ch in channels)
            {
                ch.Reset();
            }
            pinToChannelMapping.Clear();

            UpdateInterrupt();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.TasksOut.DefineMany(this, NumberOfChannels, (register, idx) => {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_OUT",
                        writeCallback: (_, val) => channels[idx].WritePin(val))
                    .WithReservedBits(1, 31);
            });

            Registers.TasksSet.DefineMany(this, NumberOfChannels, (register, idx) => {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_SET",
                        writeCallback: (_, val) => { if(val) channels[idx].SetPin(); })
                    .WithReservedBits(1, 31);
            });

            Registers.TasksClear.DefineMany(this, NumberOfChannels, (register, idx) => {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_CLR",
                        writeCallback: (_, val) => { if(val) channels[idx].ClearPin(); })
                    .WithReservedBits(1, 31);
            });

            Registers.EventsIn.DefineMany(this, NumberOfChannels, (register, idx) => {
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

            Registers.EnableInterrupt.Define(this)
                .WithFlags(0, 8, name: "EVENT_IN",
                        valueProviderCallback: (idx, _) => channels[idx].EventEnabled,
                        writeCallback: (idx, _, val) => { if(val) channels[idx].EventEnabled = true; })
                .WithReservedBits(8, 23)
                .WithFlag(31, out portInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "PORT")
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.DisableInterrupt.Define(this)
                .WithFlags(0, 8, name: "EVENT_IN",
                        valueProviderCallback: (idx, _) => channels[idx].EventEnabled,
                        writeCallback: (idx, _, val) => { if(val) channels[idx].EventEnabled = false; })
                .WithReservedBits(8, 23)
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "PORT",
                    valueProviderCallback: _ => portInterruptEnabled.Value,
                    writeCallback: (_, val) => { if(val) portInterruptEnabled.Value = false; })
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.Configuration.DefineMany(this, NumberOfChannels, (register, idx) => {
                register
                    .WithEnumField<DoubleWordRegister,Mode>(0, 2, name: "MODE",
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
                        UpdateInterrupt();
                    });
            });
        }

        private bool TryGetChannel(NRF52840_GPIO.Pin pin, out Channel channel)
        {
            if(pinToChannelMapping.TryGetValue(pin, out channel))
            {
                return true;
            }

            foreach(var ch in channels)
            {
                if(pin.Id == ch.SelectedPin && pin.Parent == ports[ch.SelectedPort])
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
            if((pin.Direction != NRF52840_GPIO.PinDirection.Input && !pin.InputOverride)
               || !TryGetChannel(pin, out var channel))
            {
                return;
            }

            if(channel.Mode != Mode.Event)
            {
                channel.CurrentState = value;
                return;
            }

            switch(channel.Polarity)
            {
                case Polarity.None:
                    channel.EventPending = false;
                    break;

                case Polarity.LoToHi:
                    channel.EventPending = !channel.CurrentState && value;
                    break;

                case Polarity.HiToLo:
                    channel.EventPending = channel.CurrentState && !value;
                    break;

                case Polarity.Toggle:
                    channel.EventPending = channel.CurrentState != value;
                    break;
            }

            channel.CurrentState = value;
            UpdateInterrupt();
        }

        private void OnDetect()
        {
            portInterruptPending.Value = true;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var flag = false;
            foreach(var ch in channels)
            {
                portInterruptPending.Value |= ch.EventPending;
                flag |= ch.EventPending && ch.EventEnabled;
            }

            flag |= portInterruptPending.Value && portInterruptEnabled.Value;

            this.NoisyLog("Setting IRQ to {0}", flag);
            IRQ.Set(flag);
        }

        private readonly uint NumberOfChannels = 8;

        private IFlagRegisterField portInterruptPending;
        private IFlagRegisterField portInterruptEnabled;

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
                switch(Polarity)
                {
                    case Polarity.None:
                        break;

                    case Polarity.LoToHi:
                        WritePinInner(true);
                        break;

                    case Polarity.HiToLo:
                        WritePinInner(false);
                        break;

                    case Polarity.Toggle:
                        WritePinInner(toggle: true);
                        break;
                }
            }

            public void SetPin()
            {
                WritePinInner(true);
            }

            public void ClearPin()
            {
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
                if(port == null)
                {
                    parent.Log(LogLevel.Warning, "Trying to access a not connected port #{0}", SelectedPort);
                    pin = null;
                    return false;
                }

                if(SelectedPin >= port.Pins.Length)
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
                if(Mode != Mode.Task)
                {
                    parent.Log(LogLevel.Warning, "Setting channel #{0} not configured as TASK", id);
                    return;
                }

                if(!TryGetPin(out var pin))
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
            // this is a group of 8 registers
            TasksOut = 0x0,
            // this is a group of 8 registers
            TasksSet = 0x30,
            // this is a group of 8 registers
            TasksClear = 0x60,
            // this is a group of 8 registers
            EventsIn = 0x100,

            EventsPort = 0x17C,

            EnableInterrupt = 0x304,
            DisableInterrupt = 0x308,

            // this is a group of 8 registers
            Configuration = 0x510
        }
    }
}
