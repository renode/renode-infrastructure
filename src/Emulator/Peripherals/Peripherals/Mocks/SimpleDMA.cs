//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals
{
    public class SimpleDMA : IDoubleWordPeripheral, IKnownSize
    {
        public SimpleDMA(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Data, new DoubleWordRegister(this).WithValueField(0, 32, out data)},

                {(long)Registers.WriteTo, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Write,
                    writeCallback: (_, address) => {
                        sysbus.WriteDoubleWord(address, (uint)data.Value);
                    })
                },

                {(long)Registers.ReadFrom, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Write,
                    writeCallback: (_, address) => {
                        data.Value = sysbus.ReadDoubleWord(address);
                    })
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x100;

        private IValueRegisterField data;

        private readonly DoubleWordRegisterCollection registers;
        private readonly IBusController sysbus;

        private enum Registers : long
        {
            Data = 0x0,
            WriteTo = 0x04,
            ReadFrom = 0x08
        }
    }
}
