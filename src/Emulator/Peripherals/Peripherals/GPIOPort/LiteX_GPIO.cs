//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class LiteX_GPIO : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_GPIO(Machine machine, Type type) : base(machine, 32)
        {
            this.type = type;

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public long Size => type == Type.InOut ? 0x8 : 0x4;

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private void DefineRegisters()
        {
            if(type != Type.Out)
            {
                Registers.Control1.Define(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "In",
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State))
                ;
            }

            if(type != Type.In)
            {
                (type == Type.Out ? Registers.Control1 : Registers.Control2).Define(this)
                    .WithValueField(0, 32, name: "Out",
                        writeCallback: (_, val) =>
                        {
                            var bits = BitHelper.GetBits(val);
                            for(var i = 0; i < 32; i++)
                            {
                                Connections[i].Set(bits[i]);
                            }
                        },
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet)))
                ;
            }
        }

        private readonly Type type;

        public enum Type
        {
            In,
            Out,
            InOut
        }

        private enum Registers
        {
            Control1 = 0x0,
            Control2 = 0x4
        }
    }
}

