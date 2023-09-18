//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class Renesas_SCI : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public Renesas_SCI(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }

        public override void WriteChar(byte value)
        {
            receiveFifo.Enqueue(value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            receiveFifo.Clear();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x400;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private void DefineRegisters()
        {
            Registers.ReceiveData.Define(this, resetValue: 0x0)
                .WithValueField(0, 9, FieldMode.Read, name: "RDAT",
                    valueProviderCallback: _ =>
                    {
                        if(!receiveFifo.TryDequeue(out byte value))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        return value;
                    })
                .WithTaggedFlag("MPB", 9)
                .WithFlag(10, mode: FieldMode.Read, name: "DR",
                    valueProviderCallback: _ =>
                    {
                        return receiveFifo.Count > 0;
                    })
                .WithTaggedFlag("FPER", 11)
                .WithTaggedFlag("FFER", 12)
                .WithReservedBits(13, 11)
                .WithTaggedFlag("ORER", 24)
                .WithReservedBits(25, 2)
                .WithTaggedFlag("PER", 27)
                .WithTaggedFlag("FER", 28)
                .WithReservedBits(29, 3);

            Registers.TransmitData.Define(this, resetValue: 0xffffffff)
                .WithValueField(0, 9, FieldMode.Write, name: "TDAT",
                    writeCallback: (_, value) =>
                    {
                        if(BitHelper.IsBitSet(value, 8))
                        {
                            this.Log(LogLevel.Warning, "Trying to transmit data with 9-th bit set: {0:X}, sending: {1:X}", value, (byte)value);
                        }
                        this.TransmitCharacter((byte)value);
                    })
                .WithTaggedFlag("MPBT", 9)
                .WithReservedBits(10, 22);

            // no special effects, software requires RE and TE flags to be settable
            Registers.CommonControl0.Define(this, resetValue: 0x0)
                .WithFlag(0, name: "RE")
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "TE")
                .WithReservedBits(5, 3)
                .WithTaggedFlag("MPIE", 8)
                .WithTaggedFlag("DCME", 9)
                .WithTaggedFlag("IDSEL", 10)
                .WithReservedBits(11, 5)
                .WithTaggedFlag("RIE", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("TIE", 20)
                .WithTaggedFlag("TEIE", 21)
                .WithReservedBits(22, 2)
                .WithTaggedFlag("SSE", 24)
                .WithReservedBits(25, 7);

            Registers.FIFOReceiveStatus.Define(this, resetValue: 0x0)
                .WithFlag(0, mode: FieldMode.Read, name: "DR",
                    valueProviderCallback: _ =>
                    {
                        return receiveFifo.Count > 0;
                    })
                .WithReservedBits(1, 7)
                .WithTag("R", 8, 6)
                .WithReservedBits(14, 2)
                .WithTag("PNUM", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("FNUM", 24, 6)
                .WithReservedBits(30, 2);
        }

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private enum Registers : long
        {
            ReceiveData = 0x0,
            TransmitData = 0x4,
            CommonControl0 = 0x8,
            FIFOReceiveStatus = 0x50,
        }
    }
}
