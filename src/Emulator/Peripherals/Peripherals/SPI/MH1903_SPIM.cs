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
            // Control Register 0 at 0x00
            Registers.Control0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 4, name: "DataFrameSize", writeCallback: (_, value) => dfs = (uint)value)
                .WithValueField(4, 2, name: "FrameFormat")
                .WithValueField(6, 2, name: "ClockPhase")
                .WithValueField(8, 2, name: "ClockPolarity")
                .WithValueField(10, 4, name: "TransferMode")
                .WithValueField(14, 2, name: "ShiftRegisterLoop")
                .WithValueField(16, 5, name: "NumberOfDataFrames")
                .WithReservedBits(21, 11);

            // Control Register 1 at 0x04
            Registers.Control1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 16, name: "NumberOfDataFramesOrReceiveFrames");

            // SPI Enable Register at 0x08
            Registers.SpiEnable.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "Enable",
                    writeCallback: (_, value) => spiEnabled = value,
                    valueProviderCallback: _ => spiEnabled);

            // Master and Slave Select Register at 0x0C
            Registers.MasterAndSlaveSelect.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "MasterWriteMode")
                .WithFlag(1, name: "MasterDataDirection")
                .WithFlag(2, name: "MasterHandshake");

            // Slave Enable Register at 0x10
            Registers.SlaveEnable.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SlaveEnable");

            // Baud Rate Divisor at 0x14
            Registers.BaudRateDivisor.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 16, name: "SerialClockDivisor");

            // Transmit FIFO Threshold Level at 0x18
            Registers.TransmitFifoThreshold.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "TransmitFifoThreshold");

            // Receive FIFO Threshold Level at 0x1C
            Registers.ReceiveFifoThreshold.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "ReceiveFifoThreshold");

            // Transmit FIFO Level Register at 0x20
            Registers.TransmitFifoLevel.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 9, FieldMode.Read, name: "TransmitFifoLevel",
                    valueProviderCallback: _ => (uint)txFifo.Count);

            // Receive FIFO Level Register at 0x24
            Registers.ReceiveFifoLevel.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 9, FieldMode.Read, name: "ReceiveFifoLevel",
                    valueProviderCallback: _ => (uint)rxFifo.Count);

            // Status Register at 0x28
            Registers.Status.Define(this, resetValue: 0x00000006)
                .WithFlag(0, FieldMode.Read, name: "Busy", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "TransmitFifoNotFull", valueProviderCallback: _ => txFifo.Count < FifoSize)
                .WithFlag(2, FieldMode.Read, name: "TransmitFifoEmpty", valueProviderCallback: _ => txFifo.Count == 0)
                .WithFlag(3, FieldMode.Read, name: "ReceiveFifoNotEmpty", valueProviderCallback: _ => rxFifo.Count > 0)
                .WithFlag(4, FieldMode.Read, name: "ReceiveFifoFull", valueProviderCallback: _ => rxFifo.Count >= FifoSize)
                .WithFlag(5, FieldMode.Read, name: "TransmissionError", valueProviderCallback: _ => false)
                .WithFlag(6, FieldMode.Read, name: "DataCollision", valueProviderCallback: _ => false)
                .WithReservedBits(7, 25);

            // Interrupt Mask Register at 0x2C
            Registers.InterruptMask.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "TransmitEmptyInterruptMask")
                .WithFlag(1, name: "TransmitOverflowInterruptMask")
                .WithFlag(2, name: "ReceiveUnderflowInterruptMask")
                .WithFlag(3, name: "ReceiveOverflowInterruptMask")
                .WithFlag(4, name: "ReceiveFifoFullInterruptMask")
                .WithFlag(5, name: "MultiMasterInterruptMask")
                .WithReservedBits(6, 26);

            // Interrupt Status Register at 0x30
            Registers.InterruptStatus.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "TransmitEmptyInterrupt", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "TransmitOverflowInterrupt", valueProviderCallback: _ => false)
                .WithFlag(2, FieldMode.Read, name: "ReceiveUnderflowInterrupt", valueProviderCallback: _ => false)
                .WithFlag(3, FieldMode.Read, name: "ReceiveOverflowInterrupt", valueProviderCallback: _ => false)
                .WithFlag(4, FieldMode.Read, name: "ReceiveFifoFullInterrupt", valueProviderCallback: _ => false)
                .WithFlag(5, FieldMode.Read, name: "MultiMasterInterrupt", valueProviderCallback: _ => false)
                .WithReservedBits(6, 26);

            // Raw Interrupt Status Register at 0x34
            Registers.RawInterruptStatus.Define(this, resetValue: 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "TransmitEmptyRawInterrupt")
                .WithFlag(1, FieldMode.Read, name: "TransmitOverflowRawInterrupt")
                .WithFlag(2, FieldMode.Read, name: "ReceiveUnderflowRawInterrupt")
                .WithFlag(3, FieldMode.Read, name: "ReceiveOverflowRawInterrupt")
                .WithFlag(4, FieldMode.Read, name: "ReceiveFifoFullRawInterrupt")
                .WithFlag(5, FieldMode.Read, name: "MultiMasterRawInterrupt")
                .WithReservedBits(6, 26);

            // Transmit FIFO Overflow Interrupt Clear at 0x38
            Registers.TransmitOverflowInterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "TransmitOverflowInterruptClear");

            // Receive FIFO Overflow Interrupt Clear at 0x3C
            Registers.ReceiveOverflowInterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ReceiveOverflowInterruptClear");

            // Receive FIFO Underflow Interrupt Clear at 0x40
            Registers.ReceiveUnderflowInterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "ReceiveUnderflowInterruptClear");

            // Master Sync Interrupt Clear at 0x44
            Registers.MasterSyncInterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "MasterSyncInterruptClear");

            // Interrupt Clear Register at 0x48
            Registers.InterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "InterruptClear");

            // DMA Control Register at 0x4C
            Registers.DmaControl.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "ReceiveDmaEnable")
                .WithFlag(1, name: "TransmitDmaEnable");

            // DMA TX Data Level at 0x50
            Registers.DmaTransmitDataLevel.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "DmaTransmitDataLevel");

            // DMA RX Data Level at 0x54
            Registers.DmaReceiveDataLevel.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "DmaReceiveDataLevel");

            // Identification Register at 0x58
            Registers.Identification.Define(this, resetValue: 0x6117A000)
                .WithValueField(0, 32, FieldMode.Read, name: "Identification");

            // Version Register at 0x5C
            Registers.Version.Define(this, resetValue: 0x3230302A)
                .WithValueField(0, 32, FieldMode.Read, name: "Version");

            // Data Register at 0x60
            Registers.Data.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Data",
                    writeCallback: (_, value) => OnDataWrite((uint)value),
                    valueProviderCallback: _ => OnDataRead());

            // RX Sample Delay at 0xF0
            Registers.RxSampleDelay.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 8, name: "RxSampleDelay");
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
            Control0 = 0x00,
            Control1 = 0x04,
            SpiEnable = 0x08,
            MasterAndSlaveSelect = 0x0C,
            SlaveEnable = 0x10,
            BaudRateDivisor = 0x14,
            TransmitFifoThreshold = 0x18,
            ReceiveFifoThreshold = 0x1C,
            TransmitFifoLevel = 0x20,
            ReceiveFifoLevel = 0x24,
            Status = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            RawInterruptStatus = 0x34,
            TransmitOverflowInterruptClear = 0x38,
            ReceiveOverflowInterruptClear = 0x3C,
            ReceiveUnderflowInterruptClear = 0x40,
            MasterSyncInterruptClear = 0x44,
            InterruptClear = 0x48,
            DmaControl = 0x4C,
            DmaTransmitDataLevel = 0x50,
            DmaReceiveDataLevel = 0x54,
            Identification = 0x58,
            Version = 0x5C,
            Data = 0x60,
            RxSampleDelay = 0xF0,
        }
    }
}
