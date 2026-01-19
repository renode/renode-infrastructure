using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class MH1903_SPIM : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {

        public MH1903_SPIM(IMachine machine) : base(machine)
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

        public ushort ReadWord(long offset)
        {
            var alignedOffset = offset & ~0x3;
            var existingValue = RegistersCollection.Read(alignedOffset);
            int shift = (int)(offset & 0x3) * 8;
            uint mask = 0xFFFFu << shift;
            return (ushort)((existingValue & mask) >> shift);
        }

        public void WriteWord(long offset, ushort value)
        {
            var alignedOffset = offset & ~0x3;
            var existingValue = RegistersCollection.Read(alignedOffset);
            int shift = (int)(offset & 0x3) * 8;
            uint mask = 0xFFFFu << shift;
            uint newValue = (existingValue & ~mask) | ((uint)value << shift);
            RegistersCollection.Write(alignedOffset, newValue);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x100;

        private void DefineRegisters()
        {
            // CTRLR0 - Control Register 0 at 0x00
            Registers.CTRLR0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 4, name: "dfs", writeCallback: (_, value) => dfs = (uint)value)
                .WithValueField(4, 2, name: "frf")
                .WithValueField(6, 2, name: "scph")
                .WithValueField(8, 2, name: "scpol")
                .WithValueField(10, 4, name: "tmod")
                .WithValueField(14, 2, name: "srl")
                .WithValueField(16, 5, name: "ndf")
                .WithReservedBits(21, 11);

            // CTRLR1 - Control Register 1 at 0x04
            Registers.CTRLR1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 16, name: "ndf_or_nr");

            // SSIENR - SPI Enable Register at 0x08
            Registers.SSIENR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "ssi_en",
                    writeCallback: (_, value) => spiEnabled = value,
                    valueProviderCallback: _ => spiEnabled);

            // MWCR - Master and Slave Select Register at 0x0C
            Registers.MWCR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "mwmod")
                .WithFlag(1, name: "mdd")
                .WithFlag(2, name: "mhs");

            // SER - Slave Enable Register at 0x10
            Registers.SER.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ser");

            // BAUDR - Baud Rate Divisor at 0x14
            Registers.BAUDR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 16, name: "sckdv");

            // TXFTLR - Transmit FIFO Threshold Level at 0x18
            Registers.TXFTLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "tft");

            // RXFTLR - Receive FIFO Threshold Level at 0x1C
            Registers.RXFTLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "rft");

            // TXFLR - Transmit FIFO Level Register at 0x20
            Registers.TXFLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 9, FieldMode.Read, name: "txtfl",
                    valueProviderCallback: _ => (uint)txFifo.Count);

            // RXFLR - Receive FIFO Level Register at 0x24
            Registers.RXFLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 9, FieldMode.Read, name: "rxtfl",
                    valueProviderCallback: _ => (uint)rxFifo.Count);

            // SR - Status Register at 0x28
            Registers.SR.Define(this, resetValue: 0x00000006)
                .WithFlag(0, FieldMode.Read, name: "busy", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "tfnf", valueProviderCallback: _ => txFifo.Count < FifoSize)
                .WithFlag(2, FieldMode.Read, name: "tfe", valueProviderCallback: _ => txFifo.Count == 0)
                .WithFlag(3, FieldMode.Read, name: "rfne", valueProviderCallback: _ => rxFifo.Count > 0)
                .WithFlag(4, FieldMode.Read, name: "rff", valueProviderCallback: _ => rxFifo.Count >= FifoSize)
                .WithFlag(5, FieldMode.Read, name: "txe", valueProviderCallback: _ => false)
                .WithFlag(6, FieldMode.Read, name: "dcol", valueProviderCallback: _ => false)
                .WithReservedBits(7, 25);

            // IMR - Interrupt Mask Register at 0x2C
            Registers.IMR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "txeim")
                .WithFlag(1, name: "txoim")
                .WithFlag(2, name: "rxuim")
                .WithFlag(3, name: "rxoim")
                .WithFlag(4, name: "rxfim")
                .WithFlag(5, name: "mstim")
                .WithReservedBits(6, 26);

            // ISR - Interrupt Status Register at 0x30
            Registers.ISR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "txeis", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "txois", valueProviderCallback: _ => false)
                .WithFlag(2, FieldMode.Read, name: "rxuis", valueProviderCallback: _ => false)
                .WithFlag(3, FieldMode.Read, name: "rxois", valueProviderCallback: _ => false)
                .WithFlag(4, FieldMode.Read, name: "rxfis", valueProviderCallback: _ => false)
                .WithFlag(5, FieldMode.Read, name: "mstis", valueProviderCallback: _ => false)
                .WithReservedBits(6, 26);

            // RISR - Raw Interrupt Status Register at 0x34
            Registers.RISR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "txeir")
                .WithFlag(1, FieldMode.Read, name: "txoir")
                .WithFlag(2, FieldMode.Read, name: "rxuir")
                .WithFlag(3, FieldMode.Read, name: "rxoir")
                .WithFlag(4, FieldMode.Read, name: "rxfir")
                .WithFlag(5, FieldMode.Read, name: "mstir")
                .WithReservedBits(6, 26);

            // TXOICR - Transmit FIFO Overflow Interrupt Clear at 0x38
            Registers.TXOICR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "txoicr");

            // RXOICR - Receive FIFO Overflow Interrupt Clear at 0x3C
            Registers.RXOICR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "rxoicr");

            // RXUICR - Receive FIFO Underflow Interrupt Clear at 0x40
            Registers.RXUICR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "rxuicr");

            // MSTICR - Master Sync Interrupt Clear at 0x44
            Registers.MSTICR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "msticr");

            // ICR - Interrupt Clear Register at 0x48
            Registers.ICR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "icr");

            // DMACR - DMA Control Register at 0x4C
            Registers.DMACR.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "rdmae")
                .WithFlag(1, name: "tdmae");

            // DMATDLR - DMA TX Data Level at 0x50
            Registers.DMATDLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "dmatdl");

            // DMARDLR - DMA RX Data Level at 0x54
            Registers.DMARDLR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "dmardl");

            // IDR - Identification Register at 0x58
            Registers.IDR.Define(this, resetValue: 0x6117A000)
                .WithValueField(0, 32, FieldMode.Read, name: "idr");

            // VERSION - Version Register at 0x5C
            Registers.VERSION.Define(this, resetValue: 0x3230302A)
                .WithValueField(0, 32, FieldMode.Read, name: "version");

            // DR - Data Register at 0x60
            Registers.DR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "dr",
                    writeCallback: (_, value) => OnDataWrite((uint)value),
                    valueProviderCallback: _ => OnDataRead());

            // RX_SAMPLE_DLY - RX Sample Delay at 0xF0
            Registers.RX_SAMPLE_DLY.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "rsd");
        }

        private void OnDataWrite(uint value)
        {
            if(txFifo.Count < FifoSize)
            {
                txFifo.Enqueue((byte)value);
                ExecuteSpiTransaction();
            }
        }

        private uint OnDataRead()
        {
            if(rxFifo.Count > 0)
            {
                return rxFifo.Dequeue();
            }
            return 0;
        }

        private void ExecuteSpiTransaction()
        {
            if(!spiEnabled || txFifo.Count == 0)
                return;

            if(ChildCollection.Count == 0)
            {
                // Act like we send and nothing answered
                txFifo.Dequeue();
                rxFifo.Enqueue(0xFF);
                return;
            }

            var slave = ChildCollection.Values.First();

            // Transmit one byte from TX FIFO and get response
            byte txByte = txFifo.Dequeue();
            byte rxByte = slave.Transmit(txByte);
            rxFifo.Enqueue(rxByte);

            // If more data in TX FIFO, continue
            if(txFifo.Count > 0)
            {
                ExecuteSpiTransaction();
            }
            else
            {
                //slave.FinishTransmission();
            }
        }
        private bool spiEnabled = false;
        private uint dfs = 0;

        private readonly Queue<byte> txFifo = new Queue<byte>();
        private readonly Queue<byte> rxFifo = new Queue<byte>();
        private const int FifoSize = 256;

        public override void Reset()
        {
            RegistersCollection.Reset();
            txFifo.Clear();
            rxFifo.Clear();
            spiEnabled = false;
            dfs = 0;
        }

        private enum Registers : long
        {
            CTRLR0 = 0x00,
            CTRLR1 = 0x04,
            SSIENR = 0x08,
            MWCR = 0x0C,
            SER = 0x10,
            BAUDR = 0x14,
            TXFTLR = 0x18,
            RXFTLR = 0x1C,
            TXFLR = 0x20,
            RXFLR = 0x24,
            SR = 0x28,
            IMR = 0x2C,
            ISR = 0x30,
            RISR = 0x34,
            TXOICR = 0x38,
            RXOICR = 0x3C,
            RXUICR = 0x40,
            MSTICR = 0x44,
            ICR = 0x48,
            DMACR = 0x4C,
            DMATDLR = 0x50,
            DMARDLR = 0x54,
            IDR = 0x58,
            VERSION = 0x5C,
            DR = 0x60,
            RX_SAMPLE_DLY = 0xF0,
        }
    }
}
