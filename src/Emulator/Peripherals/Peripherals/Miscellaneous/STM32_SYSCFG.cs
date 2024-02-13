//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class STM32_SYSCFG : IDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize, ILocalGPIOReceiver
    {
        public STM32_SYSCFG()
        {
            var gpios = new Dictionary<int, IGPIO>();
            for(var i = 0; i < GpioPins; ++i)
            {
                gpios.Add(i, new GPIO());
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(gpios);
            internalReceiversCache = new Dictionary<int, InternalReceiver>();
            registers = CreateRegisters();
        }

        /* The pattern to connect IRQs in REPL would be:
         * `[a-b] -> syscfg@index[a-b]`, where:
         * -> index - index of the mapped peripheral
         * -> a, b - the range of exposed GPIO pins (lines to be redirected to EXTI)
         * since this peripheral maps IRQs line-to-line (e.g. line X of input into line X of output)
         * the thing that's muxed is the peripheral, the line belongs to (the index)
         */
        public IGPIOReceiver GetLocalReceiver(int index)
        {
            if(!internalReceiversCache.TryGetValue(index, out var receiver))
            {
                receiver = new InternalReceiver(this, index);
                internalReceiversCache.Add(index, receiver);
            }
            return receiver;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            foreach(var connection in Connections.Values)
            {
                connection.Unset();
            }
            foreach(var receiver in internalReceiversCache.Values)
            {
                for(int pin = 0; pin < GpioPins; ++pin)
                {
                    receiver.UpdateGPIO(pin);
                }
            }
            registers.Reset();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
            get; private set;
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var map = new Dictionary<long, DoubleWordRegister>();
            for(var regNumber = 0; regNumber < 4; ++regNumber)
            {
                var reg = new DoubleWordRegister(this, 0);
                for(var fieldNumber = 0; fieldNumber < 4; ++fieldNumber)
                {
                    var rn = regNumber;
                    var fn = fieldNumber;
                    var pinNumber = regNumber * 4 + fieldNumber;
                    extiMappings[pinNumber] = reg.DefineValueField(4 * fieldNumber, 4, name: "EXTI" + pinNumber,
                        changeCallback: (_, portNumber) =>
                        {
                            Connections[pinNumber].Unset();
                            ((InternalReceiver)GetLocalReceiver((int)portNumber)).UpdateGPIO(pinNumber);
                        }
                    );
                }
                map.Add((long)Registers.ExternalInterruptConfiguration1 + 4 * regNumber, reg);
            }
            return new DoubleWordRegisterCollection(this, map);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly Dictionary<int, InternalReceiver> internalReceiversCache;

        private readonly IValueRegisterField[] extiMappings = new IValueRegisterField[GpioPins];

        private const int GpioPins = 16;

        private class InternalReceiver : IGPIOReceiver
        {
            public InternalReceiver(STM32_SYSCFG parent, int portNumber)
            {
                this.parent = parent;
                this.portNumber = portNumber;
                this.state = new bool[GpioPins];
            }

            public void OnGPIO(int pinNumber, bool value)
            {
                if(pinNumber >= GpioPins)
                {
                    parent.Log(LogLevel.Error, "GPIO port {0}, pin {1}, is not supported. Up to {2} pins are supported", portNumber, pinNumber, GpioPins);
                    return;
                }
                parent.Log(LogLevel.Noisy, "GPIO port {0}, pin {1}, raised IRQ: {2}", portNumber, pinNumber, value);
                state[pinNumber] = value;
                UpdateGPIO(pinNumber);
            }

            public void UpdateGPIO(int pinNumber)
            {
                if((int)parent.extiMappings[pinNumber].Value == portNumber)
                {
                    parent.Connections[pinNumber].Set(state[pinNumber]);
                }
            }

            public void Reset()
            {
                // IRQs are cleared on Parent reset
                // Don't clear `state` array here - as it represents the state of input signals, and is not a property of this peripheral
                // The state can only be cleared when the input signal is reset - but it's not controlled by us, but by the peripheral connected to OnGPIO (IRQ/GPIO line)
                // and since peripherals with connected GPIOs will naturally unset them in their Reset, the state array won't contain stale data
            }

            // The state is recorded, so it's possible to update the GPIO state when changing source peripheral
            // since this peripheral is effectively a mux - when changing input source, it's needed to get the other source's value
            private readonly bool[] state;
            private readonly STM32_SYSCFG parent;
            private readonly int portNumber;
        }

        private enum Registers
        {
            MemoryRemap = 0x0,
            PeripheralModeConfiguration = 0x4,
            ExternalInterruptConfiguration1 = 0x8,
            ExternalInterruptConfiguration2 = 0xC,
            ExternalInterruptConfiguration3 = 0x10,
            ExternalInterruptConfiguration4 = 0x14,
            ConfigurationRegister = 0x18,
            CompensationCellControl = 0x20,
            CompensationCellValue = 0x24,
            CompensationCellCode = 0x28,
            PowerControl = 0x2C,
            PackageType = 0x124,
            User0 = 0x300,
            User1 = 0x304,
            User2 = 0x308,
            User3 = 0x30C,
            User4 = 0x310,
            User5 = 0x314,
            User6 = 0x318,
            User7 = 0x31C,
            User8 = 0x320,
            User9 = 0x324,
            User10 = 0x328,
            User11 = 0x32C,
            User12 = 0x330,
            User13 = 0x334,
            User14 = 0x338,
            User15 = 0x33C,
            User16 = 0x340,
            User17 = 0x344,
        }
    }
}
