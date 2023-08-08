//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class STM32F1GPIOPort : BaseGPIOPort, IDoubleWordPeripheral
    {
        public STM32F1GPIOPort(IMachine machine) : base(machine, NumberOfPorts)
        {
            pins = new PinMode[NumberOfPorts];

            var configurationLowRegister = new DoubleWordRegister(this, 0x44444444);
            var configurationHighRegister = new DoubleWordRegister(this, 0x44444444);
            for(var offset = 0; offset < 32; offset += 4)
            {
                var lowId = offset / 4;
                var highId = lowId + 8;

                configurationLowRegister.DefineEnumField<PinMode>(offset, 2, name: $"MODE{lowId}", writeCallback: (_, value) => pins[lowId] = value, valueProviderCallback: _ => pins[lowId]);
                configurationLowRegister.Tag($"CNF{lowId}", offset + 2, 2);

                configurationHighRegister.DefineEnumField<PinMode>(offset, 2, name: $"MODE{highId}", writeCallback: (_, value) => pins[highId] = value, valueProviderCallback: _ => pins[highId]);
                configurationHighRegister.Tag($"CNF{highId}", offset + 2, 2);
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

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
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

        private readonly DoubleWordRegisterCollection registers;
        private readonly PinMode[] pins;

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

        private const int NumberOfPorts = 16;
    }
}
