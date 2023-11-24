//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRA_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public RenesasRA_GPIO(IMachine machine, int numberOfConnections) : base(machine, numberOfConnections)
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

        private void DefineRegisters()
        {
            Registers.PortControl1.Define(this)
                .WithTaggedFlag("PDR00", 0)
                .WithTaggedFlag("PDR01", 1)
                .WithTaggedFlag("PDR02", 2)
                .WithTaggedFlag("PDR03", 3)
                .WithTaggedFlag("PDR04", 4)
                .WithTaggedFlag("PDR05", 5)
                .WithTaggedFlag("PDR06", 6)
                .WithTaggedFlag("PDR07", 7)
                .WithTaggedFlag("PDR08", 8)
                .WithTaggedFlag("PDR09", 9)
                .WithTaggedFlag("PDR10", 10)
                .WithTaggedFlag("PDR11", 11)
                .WithTaggedFlag("PDR12", 12)
                .WithTaggedFlag("PDR13", 13)
                .WithTaggedFlag("PDR14", 14)
                .WithTaggedFlag("PDR15", 15)
                .WithTaggedFlag("PODR00", 16)
                .WithTaggedFlag("PODR01", 17)
                .WithTaggedFlag("PODR02", 18)
                .WithTaggedFlag("PODR03", 19)
                .WithTaggedFlag("PODR04", 20)
                .WithTaggedFlag("PODR05", 21)
                .WithTaggedFlag("PODR06", 22)
                .WithTaggedFlag("PODR07", 23)
                .WithTaggedFlag("PODR08", 24)
                .WithTaggedFlag("PODR09", 25)
                .WithTaggedFlag("PODR10", 26)
                .WithTaggedFlag("PODR11", 27)
                .WithTaggedFlag("PODR12", 28)
                .WithTaggedFlag("PODR13", 29)
                .WithTaggedFlag("PODR14", 30)
                .WithTaggedFlag("PODR15", 31);

            Registers.PortControl2.Define(this)
                .WithTaggedFlag("PIDR00", 0)
                .WithTaggedFlag("PIDR01", 1)
                .WithTaggedFlag("PIDR02", 2)
                .WithTaggedFlag("PIDR03", 3)
                .WithTaggedFlag("PIDR04", 4)
                .WithTaggedFlag("PIDR05", 5)
                .WithTaggedFlag("PIDR06", 6)
                .WithTaggedFlag("PIDR07", 7)
                .WithTaggedFlag("PIDR08", 8)
                .WithTaggedFlag("PIDR09", 9)
                .WithTaggedFlag("PIDR10", 10)
                .WithTaggedFlag("PIDR11", 11)
                .WithTaggedFlag("PIDR12", 12)
                .WithTaggedFlag("PIDR13", 13)
                .WithTaggedFlag("PIDR14", 14)
                .WithTaggedFlag("PIDR15", 15)
                .WithTaggedFlag("PIDR00", 16)
                .WithTaggedFlag("PIDR01", 17)
                .WithTaggedFlag("PIDR02", 18)
                .WithTaggedFlag("PIDR03", 19)
                .WithTaggedFlag("PIDR04", 20)
                .WithTaggedFlag("PIDR05", 21)
                .WithTaggedFlag("PIDR06", 22)
                .WithTaggedFlag("PIDR07", 23)
                .WithTaggedFlag("PIDR08", 24)
                .WithTaggedFlag("PIDR09", 25)
                .WithTaggedFlag("PIDR10", 26)
                .WithTaggedFlag("PIDR11", 27)
                .WithTaggedFlag("PIDR12", 28)
                .WithTaggedFlag("PIDR13", 29)
                .WithTaggedFlag("PIDR14", 30)
                .WithTaggedFlag("PIDR15", 31);

            Registers.PortControl3.Define(this)
                .WithTaggedFlag("POSR00", 0)
                .WithTaggedFlag("POSR01", 1)
                .WithTaggedFlag("POSR02", 2)
                .WithTaggedFlag("POSR03", 3)
                .WithTaggedFlag("POSR04", 4)
                .WithTaggedFlag("POSR05", 5)
                .WithTaggedFlag("POSR06", 6)
                .WithTaggedFlag("POSR07", 7)
                .WithTaggedFlag("POSR08", 8)
                .WithTaggedFlag("POSR09", 9)
                .WithTaggedFlag("POSR10", 10)
                .WithTaggedFlag("POSR11", 11)
                .WithTaggedFlag("POSR12", 12)
                .WithTaggedFlag("POSR13", 13)
                .WithTaggedFlag("POSR14", 14)
                .WithTaggedFlag("POSR15", 15)
                .WithTaggedFlag("PORR00", 16)
                .WithTaggedFlag("PORR01", 17)
                .WithTaggedFlag("PORR02", 18)
                .WithTaggedFlag("PORR03", 19)
                .WithTaggedFlag("PORR04", 20)
                .WithTaggedFlag("PORR05", 21)
                .WithTaggedFlag("PORR06", 22)
                .WithTaggedFlag("PORR07", 23)
                .WithTaggedFlag("PORR08", 24)
                .WithTaggedFlag("PORR09", 25)
                .WithTaggedFlag("PORR10", 26)
                .WithTaggedFlag("PORR11", 27)
                .WithTaggedFlag("PORR12", 28)
                .WithTaggedFlag("PORR13", 29)
                .WithTaggedFlag("PORR14", 30)
                .WithTaggedFlag("PORR15", 31);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private enum Registers
        {
            PortControl1 = 0x0,
            PortControl2 = 0x4,
            PortControl3 = 0x8,
        }
    }
}
