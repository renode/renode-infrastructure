//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RCAR_UART : IUART, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RCAR_UART()
        {
            IRQ = new GPIO();

            receiveQueue = new Queue<ushort>();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
           RegistersCollection.Write(offset, value);
        }

        public void WriteChar(byte value)
        {
            receiveQueue.Enqueue(value);
            // UpdateInterrupts();
        }

        private void TransmitData(byte value)
        {
            CharReceived?.Invoke(value);
        }

        private void DefineRegisters()
        {
            Registers.SerialMode.Define(this)

                .WithTag("CKS", 0, 2)
                .WithTaggedFlag("MP", 2)
                .WithTaggedFlag("STOP", 3)
                .WithTaggedFlag("PM", 4)
                .WithTaggedFlag("PE", 5)
                .WithTaggedFlag("CHR", 6)
                .WithTaggedFlag("CM", 7)
                .WithReservedBits(8, 24);
            
            Registers.BitRate.Define(this, 0xff)
                .WithTag("BRR", 0, 8)
                .WithReservedBits(8, 24);

            Registers.SerialControl.Define(this)

                .WithTag("CKE", 0, 2)
                .WithTaggedFlag("TOIE", 2)
                .WithTaggedFlag("REIE", 3)
                .WithTaggedFlag("RE", 4)
                .WithTaggedFlag("TE", 5)
                .WithTaggedFlag("RIE", 6)
                .WithTaggedFlag("TIE", 7)
                .WithReservedBits(8, 3)
                .WithTaggedFlag("TEIE", 11)
                .WithReservedBits(12, 20);
            Registers.TransmitData.Define(this, 0xff)
                .WithValueField(0, 8, FieldMode.Write, name: "TDR",
                    writeCallback: (_, value) =>
                    {
                    TransmitData((byte)value);
                    //UpdateInterrupts();
                    })
                .WithReservedBits(8, 24);

            Registers.SerialStatus.Define(this, 0x20)

                .WithTaggedFlag("DR", 0)
                .WithTaggedFlag("RDF", 1)
                .WithTaggedFlag("PER", 2)
                .WithTaggedFlag("FER", 3)
                .WithTaggedFlag("BRK", 4)
                .WithTaggedFlag("TDFE", 5)
                .WithTaggedFlag("TEND", 6)
                .WithTaggedFlag("ER", 7)
                .WithTaggedFlag("FER0", 8)
                .WithTaggedFlag("FER1", 9)
                .WithTaggedFlag("FER2", 10)
                .WithTaggedFlag("FER3", 11)
                .WithTaggedFlag("PER0", 12)
                .WithTaggedFlag("PER1", 13)
                .WithTaggedFlag("PER2", 14)
                .WithTaggedFlag("PER3", 15)
                .WithReservedBits(16, 16);
        }

        private readonly Queue<ushort> receiveQueue;

        [field: Transient]
        public event Action<byte> CharReceived;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x100;

        public uint BaudRate => 115200;

        public void Reset()
        {
          RegistersCollection.Reset();
        }

        public Bits StopBits => Bits.One;

        public Parity ParityBit => Parity.None;

        private enum Registers
        {
            SerialMode = 0x0,
            BitRate = 0x4,
            SerialControl = 0x8,
            TransmitData = 0xc,
            SerialStatus = 0x10,
            ReceiveData = 0x14,
            FifoControl = 0x18,
            FifoDataCount = 0x1c,
            SerialPort = 0x20,
            LineStatus = 0x24,
            FrequencyDivision = 0x30,
            ClockSelect = 0x34,
        }
    }
}

