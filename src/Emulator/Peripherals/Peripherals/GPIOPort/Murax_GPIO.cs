//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class Murax_GPIO : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public Murax_GPIO(IMachine machine) : base(machine, 32)
        {
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

        public long Size => 0xC;

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private void DefineRegisters()
        {
            Registers.Output.Define(this)
                .WithValueField(0, 32, out output,
                    writeCallback: (_, val) => RefreshConnectionsState(),
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet)))
            ;

            Registers.OutputEnable.Define(this)
                .WithValueField(0, 32, out outputEnable,
                    writeCallback: (_, val) => RefreshConnectionsState())
            ;
        }

        private void RefreshConnectionsState()
        {
            var outputBits = BitHelper.GetBits((uint)outputEnable.Value);
            var bits = BitHelper.GetBits((uint)output.Value);
            for(var i = 0; i < 32; i++)
            {
                Connections[i].Set(bits[i] && outputBits[i]);
            }
        }

        private IValueRegisterField outputEnable;
        private IValueRegisterField output;

        private enum Registers
        {
            Input = 0x0,
            Output = 0x4,
            OutputEnable = 0x8
        }
    }
}
