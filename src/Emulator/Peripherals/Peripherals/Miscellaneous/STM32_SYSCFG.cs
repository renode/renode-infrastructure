//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.IRQControllers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class STM32_SYSCFG : IDoubleWordPeripheral, IIRQController, INumberedGPIOOutput, IKnownSize
    {
        public STM32_SYSCFG()
        {
            var gpios = new Dictionary<int, IGPIO>();
            for(var i = 0; i < GpioPins; ++i)
            {
                gpios.Add(i, new GPIO());
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(gpios);
            registers = CreateRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            var pinNumber = number % GpioPins;
            var portNumber = number / GpioPins;
            if((int)extiMappings[pinNumber].Value == portNumber)
            {
                Connections[pinNumber].Set(value);
            }
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
                    extiMappings[regNumber * 4 + fieldNumber] = reg.DefineValueField(4 * fieldNumber, 4, name: "EXTI" + regNumber * 4 + fieldNumber, changeCallback: (_, __) => Connections[rn * 4 + fn].Unset());
                }
                map.Add((long)Registers.ExternalInterruptConfiguration1 + 4 * regNumber, reg);
            }
            return new DoubleWordRegisterCollection(this, map);
        }

        private readonly DoubleWordRegisterCollection registers;

        private readonly IValueRegisterField[] extiMappings = new IValueRegisterField[GpioPins];

        private const int GpioPins = 16;

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
