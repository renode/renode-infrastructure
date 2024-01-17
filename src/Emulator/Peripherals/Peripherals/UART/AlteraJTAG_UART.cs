//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class AlteraJTAG_UART : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public AlteraJTAG_UART(IMachine machine, uint fifoDepth = 64) : base(machine)
        {
            this.fifoDepth = fifoDepth;
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            base.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        // IRQ is not supported yet
        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x8;

        public override uint BaudRate => 0;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.None;

        protected override void CharWritten()
        {
        }

        protected override void QueueEmptied()
        {
        }

        private void DefineRegisters()
        {
            // in the current implementation we rely on the callbacks ordering
            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "DATA",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var c))
                        {
                            this.Log(LogLevel.Warning, "Tried to read an empty data register");
                        }
                        return c;
                    },
                    writeCallback: (_, value) => TransmitCharacter((byte)value))
                .WithReservedBits(8, 7)
                .WithFlag(15, FieldMode.Read, name: "RVALID", valueProviderCallback: _ => true)
                .WithValueField(16, 16, FieldMode.Read, name: "RAVAIL", valueProviderCallback: _ => (ulong)Math.Min(fifoDepth, Count));

            Registers.Control.Define(this)
                .WithTaggedFlag("RE", 0)
                .WithTaggedFlag("WE", 1)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("RI", 8)
                .WithTaggedFlag("WI", 9)
                .WithTaggedFlag("AC", 10)
                .WithReservedBits(11, 5)
                .WithValueField(16, 16, FieldMode.Read, name: "WSPACE", valueProviderCallback: _ => (ulong)fifoDepth);
        }

        private readonly uint fifoDepth;

        private enum Registers
        {
            Data = 0x0,
            Control = 0x4,
        }
    }
}
