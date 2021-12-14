//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class DummyI2CSlave : II2CPeripheral
    {
        public DummyI2CSlave()
        {
            registers = CreateRegisters();
        }

        public void Write(byte[] data)
        {
            selectedRegister = data[1];
        }

        public byte[] Read(int count = 1)
        {
            return new byte[] { registers.Read(selectedRegister) };
        }

        public void FinishTransmission()
        {
            // Intentionally do nothing.
        }

        public void Reset()
        {
            registers.Reset();
        }

        public byte Register0Value { get; set; }
        public byte Register1Value { get; set; }

        private ByteRegisterCollection CreateRegisters()
        {
            var map = new Dictionary<long, ByteRegister> {
                {(long)Registers.Register0, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => Register0Value)
                },

                {(long)Registers.Register1, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => Register1Value)
                }
            };

            return new ByteRegisterCollection(this, map);
        }

        private readonly ByteRegisterCollection registers;

        private uint selectedRegister;

        private enum Registers
        {
            Register0 = 0,
            Register1 = 0x1
        }
    }
}
