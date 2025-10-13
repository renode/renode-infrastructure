//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class STM32F1GPIOPort : BaseGPIOPort, IDoubleWordPeripheral, ILocalGPIOReceiver
    {
        // invertedAFPins: List of pin configurations where each inner list contains the pin number as the first element,
        // followed by timer numbers that should have inverted alternate function signals on that pin.
        // Example: [[2, 5, 7], [4, 3]] means pin 2 has timers 5 and 7 inverted, pin 4 has timer 3 inverted.
        public STM32F1GPIOPort(IMachine machine, List<List<int>> invertedAFPins = null) : base(machine, NumberOfPorts)
        {
            if(invertedAFPins == null)
            {
                invertedAFPins = new List<List<int>>();
            }

            foreach(var list in invertedAFPins)
            {
                var pin = list[0];
                var tims = list.Skip(1).ToList();

                if(pin < 0 || pin >= NumberOfPorts)
                {
                    throw new ConstructionException($"Pin {pin} out of range [0, {NumberOfPorts - 1}]");
                }

                foreach(var tim in tims)
                {
                    if(tim < 0 || tim >= NumberOfTimers)
                    {
                        throw new ConstructionException($"TIM{tim} out of range [0, {NumberOfTimers - 1}]");
                    }

                    this.invertedAFPins.Add(new InvertedAFPin(pin, tim));
                }
            }

            pins = new PinMode[NumberOfPorts];
            pinsOutputMode = new OutputMode[NumberOfPorts];
            AlternateFunctionOutputs = new GPIOAlternateFunction[NumberOfPorts];
            for(var i = 0; i < NumberOfPorts; i++)
            {
                AlternateFunctionOutputs[i] = new GPIOAlternateFunction(this, i);
            }

            var configurationLowRegister = new DoubleWordRegister(this, 0x44444444);
            var configurationHighRegister = new DoubleWordRegister(this, 0x44444444);
            for(var offset = 0; offset < 32; offset += 4)
            {
                var lowId = offset / 4;
                var highId = lowId + 8;

                configurationLowRegister.DefineEnumField<PinMode>(offset, 2, name: $"MODE{lowId}", writeCallback: (_, value) => pins[lowId] = value, valueProviderCallback: _ => pins[lowId]);
                configurationLowRegister.DefineEnumField<OutputMode>(offset + 2, 2, name: $"CNF{lowId}", writeCallback: (_, value) => ChangeOutputMode(lowId, value), valueProviderCallback: _ => pinsOutputMode[lowId]);

                configurationHighRegister.DefineEnumField<PinMode>(offset, 2, name: $"MODE{highId}", writeCallback: (_, value) => pins[highId] = value, valueProviderCallback: _ => pins[highId]);
                configurationHighRegister.DefineEnumField<OutputMode>(offset + 2, 2, name: $"CNF{highId}", writeCallback: (_, value) => ChangeOutputMode(highId, value), valueProviderCallback: _ => pinsOutputMode[highId]);
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ConfigurationLow, configurationLowRegister},
                {(long)Registers.ConfigurationHigh, configurationHighRegister},
                {(long)Registers.InputData, new DoubleWordRegister(this)
                    // upper 16 bits are reserved
                    .WithValueField(0, 16, FieldMode.Read, name: "IDR", valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State))
                },
                {(long)Registers.OutputData, new DoubleWordRegister(this)
                    // upper 16 bits are reserved
                    .WithValueField(0, 16, name: "ODR", writeCallback: (_, value) => SetConnectionsStateUsingBits((uint)value), valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Values.Select(x=>x.IsSet)))
                },
                {(long)Registers.BitSetReset, new DoubleWordRegister(this)
                    .WithValueField(16, 16, FieldMode.Write, name: "BR", writeCallback: (_, value) => SetBitsFromMask((uint)value, false))
                    .WithValueField(0, 16, FieldMode.Write, name: "BS", writeCallback: (_, value) => SetBitsFromMask((uint)value, true))
                },
                {(long)Registers.BitReset, new DoubleWordRegister(this)
                    // upper 16 bits are reserved
                    .WithValueField(0, 16, FieldMode.Write, name: "BR", writeCallback: (_, value) => SetBitsFromMask((uint)value, false))
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);

            Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            if(pins[number] != PinMode.Input)
            {
                this.Log(LogLevel.Warning, "Received a signal on the output pin #{0}", number);
                return;
            }

            base.OnGPIO(number, value);
            Connections[number].Set(value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            for(var i = 0; i < NumberOfPorts; i++)
            {
                AlternateFunctionOutputs[i].Reset();
                ChangeOutputMode(i, OutputMode.ResetValue);
            }
        }

        public IGPIOReceiver GetLocalReceiver(int pin)
        {
            if(pin < 0 || pin >= NumberOfPorts)
            {
                throw new RecoverableException($"This peripheral supports GPIO inputs from 0 to {NumberOfPorts - 1}, but {pin} was called.");
            }

            return AlternateFunctionOutputs[pin];
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        // NOTE: This array holds connections from AFs to specific GPIO pins.
        // From the PoV of this peripheral they're inputs,
        // however they represent the output pins of this peripheral hence the name.
        public GPIOAlternateFunction[] AlternateFunctionOutputs;

        private void WritePin(int number, bool value)
        {
            State[number] = value;
            Connections[number].Set(value);
        }

        private void WriteState(ushort value)
        {
            for(var i = 0; i < NumberOfPorts; i++)
            {
                var state = ((value & 1u) == 1);
                WritePin(i, state);

                value >>= 1;
            }
        }

        private void ChangeOutputMode(int number, OutputMode newMode)
        {
            pinsOutputMode[number] = newMode;
            AlternateFunctionOutputs[number].IsConnected = (newMode == OutputMode.AlternateFunctionOpenDrain || newMode == OutputMode.AlternateFunctionPushPull);
        }

        private void SetBitsFromMask(uint mask, bool state)
        {
            foreach(var bit in BitHelper.GetSetBits(mask))
            {
                if(pins[bit] == PinMode.Input)
                {
                    this.Log(LogLevel.Warning, "Trying to set the state of the input pin #{0}", bit);
                    continue;
                }

                Connections[bit].Set(state);
                State[bit] = state;
            }
        }

        private readonly HashSet<InvertedAFPin> invertedAFPins = new HashSet<InvertedAFPin>();

        private readonly DoubleWordRegisterCollection registers;
        private readonly PinMode[] pins;
        private readonly OutputMode[] pinsOutputMode;

        private const int NumberOfPorts = 16;
        private const int NumberOfTimers = 18;

        public class GPIOAlternateFunction : IGPIOReceiver
        {
            public GPIOAlternateFunction(STM32F1GPIOPort port, int pin)
            {
                this.port = port;
                this.pin = pin;

                Reset();
            }

            public void Reset()
            {
                IsConnected = false;
                ActiveFunction = 0;
            }

            public void OnGPIO(int number, bool value)
            {
                if(!IsConnected || number != activeFunction)
                {
                    // Don't emit any log as it is valid to receive signals from AFs when they are not active.
                    // All alternate function sources are always connected and always sending signals.
                    // The GPIO configuration then decides whether those are connected
                    // to GPIO input/output or are simply ignored.
                    return;
                }

                var invert = port.invertedAFPins.Contains(new InvertedAFPin(pin, number));
                port.WritePin(pin, value ^ invert);
            }

            public bool IsConnected { get; set; }

            public ulong ActiveFunction
            {
                get => (ulong)activeFunction;
                set
                {
                    var val = (int)value;
                    activeFunction = val;
                }
            }

            private int activeFunction;

            private readonly STM32F1GPIOPort port;
            private readonly int pin;
        }

        private struct InvertedAFPin
        {
            public InvertedAFPin(int pin, int af)
            {
                Pin = pin;
                AF = af;
            }

            public int Pin { get; }

            public int AF { get; }
        }
        // TODO: AF inputs

        private enum Registers
        {
            ConfigurationLow = 0x00,
            ConfigurationHigh = 0x04,
            InputData = 0x08,
            OutputData = 0x0C,
            BitSetReset = 0x10,
            BitReset = 0x14,
            PortConfigurationLock = 0x18
        }

        private enum PinMode
        {
            Input = 0,
            Output10Mhz = 1,
            Output2Mhz = 2,
            Output50Mhz = 3
        }

        private enum OutputMode
        {
            ResetValue = GeneralPurposePushPull,
            GeneralPurposePushPull = 0,
            GeneralPurposeOpenDrain = 1,
            AlternateFunctionPushPull = 2,
            AlternateFunctionOpenDrain = 3
        }
    }
}