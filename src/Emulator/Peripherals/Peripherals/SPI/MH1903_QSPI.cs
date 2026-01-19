using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class MH1903_QSPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public MH1903_QSPI(IMachine machine) : base(machine)
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

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.FcuCmd.Define(this, resetValue: 0x00000000)
                .WithValueField(24, 8, out commandCode, name: "command_code")
                .WithReservedBits(10, 14)
                .WithValueField(8, 2, out busMode, name: "bus_mode")
                .WithValueField(4, 4, out cmdFormat, name: "cmd_format")
                .WithFlag(3, out commandDone, name: "done", mode: FieldMode.Read)
                .WithFlag(2, out commandBusy, name: "busy", mode: FieldMode.Read)
                .WithFlag(1, out accessAck, name: "access_ack", mode: FieldMode.Read)
                .WithFlag(0, name: "access_req", writeCallback: (_, value) => OnAccessRequest(value));

            Registers.Address.Define(this, resetValue: 0x00000000)
                .WithValueField(8, 24, out addressField, name: "address")
                .WithValueField(0, 8, out m7_0, name: "m7_0");

            Registers.ByteNum.Define(this, resetValue: 0x00000000)
                .WithReservedBits(29, 3)
                .WithValueField(16, 13, out writeByteCount, name: "wr_byte_num")
                .WithReservedBits(13, 3)
                .WithValueField(0, 13, out readByteCount, name: "rd_byte_num");

            Registers.WrFifo.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "wr_data", mode: FieldMode.Write, writeCallback: (_, value) => OnWriteFifo((uint)value));

            Registers.RdFifo.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rd_data", mode: FieldMode.Read, valueProviderCallback: _ => OnReadFifo());

            Registers.DevicePara.Define(this, resetValue: 0x00A80283)
                .WithValueField(16, 16, name: "one_us_count")
                .WithValueField(15, 1, name: "sample_dly")
                .WithValueField(14, 1, name: "sample_pha")
                .WithReservedBits(9, 5)
                .WithValueField(8, 1, name: "protocol")
                .WithValueField(4, 4, name: "dummy_cycles")
                .WithValueField(3, 1, name: "flash_ready")
                .WithReservedBits(2, 1)
                .WithValueField(0, 2, name: "freq_sel");

            Registers.RegWdata.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, out regWdata, name: "reg_wdata");

            Registers.RegRdata.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, out regRdata, name: "reg_rdata", mode: FieldMode.Read);

            Registers.IntMask.Define(this)
                .WithReservedBits(7, 25)
                .WithFlag(6, name: "tx_fifo_data_mask")
                .WithFlag(5, name: "rx_fifo_data_mask")
                .WithFlag(4, name: "tx_fifo_of_mask")
                .WithFlag(3, name: "tx_fifo_uf_mask")
                .WithFlag(2, name: "rx_fifo_of_mask")
                .WithFlag(1, name: "rx_fifo_uf_mask")
                .WithFlag(0, name: "done_int_mask");

            Registers.IntUmask.Define(this)
                .WithReservedBits(7, 25)
                .WithFlag(6, name: "tx_fifo_data_unmask")
                .WithFlag(5, name: "rx_fifo_data_unmask")
                .WithFlag(4, name: "tx_fifo_of_unmask")
                .WithFlag(3, name: "tx_fifo_uf_unmask")
                .WithFlag(2, name: "rx_fifo_of_unmask")
                .WithFlag(1, name: "rx_fifo_uf_unmask")
                .WithFlag(0, name: "done_int_unmask");

            Registers.IntMaskStatus.Define(this)
                .WithReservedBits(7, 25)
                .WithFlag(6, FieldMode.Read, name: "tx_fifo_data_mask")
                .WithFlag(5, FieldMode.Read, name: "rx_fifo_data_mask")
                .WithFlag(4, FieldMode.Read, name: "tx_fifo_of_mask")
                .WithFlag(3, FieldMode.Read, name: "tx_fifo_uf_mask")
                .WithFlag(2, FieldMode.Read, name: "rx_fifo_of_mask")
                .WithFlag(1, FieldMode.Read, name: "rx_fifo_uf_mask")
                .WithFlag(0, FieldMode.Read, name: "done_int_mask");

            Registers.IntStatus.Define(this)
                .WithReservedBits(7, 25) // Bits 31:7 are reserved (RO)
                .WithFlag(6, FieldMode.Read, name: "tx_fifo_data_status")
                .WithFlag(5, FieldMode.Read, name: "rx_fifo_data_status")
                .WithFlag(4, FieldMode.Read, name: "tx_fifo_of_status")
                .WithFlag(3, FieldMode.Read, name: "tx_fifo_uf_status")
                .WithFlag(2, FieldMode.Read, name: "rx_fifo_of_status")
                .WithFlag(1, FieldMode.Read, name: "rx_fifo_uf_status")
                .WithFlag(0, FieldMode.Read, name: "done_int_status");

            Registers.IntRawstatus.Define(this)
                .WithReservedBits(7, 25) // Bits 31:7 are reserved (RO)
                .WithFlag(6, FieldMode.Read, name: "tx_fifo_data_rawstatus")
                .WithFlag(5, FieldMode.Read, name: "rx_fifo_data_rawstatus")
                .WithFlag(4, FieldMode.Read, name: "tx_fifo_of_rawstatus")
                .WithFlag(3, FieldMode.Read, name: "tx_fifo_uf_rawstatus")
                .WithFlag(2, FieldMode.Read, name: "rx_fifo_of_rawstatus")
                .WithFlag(1, FieldMode.Read, name: "rx_fifo_uf_rawstatus")
                .WithFlag(0, FieldMode.Read, name: "done_int_rawstatus");

            Registers.IntClear.Define(this, resetValue: 0x00000000)
                .WithReservedBits(7, 25)
                .WithFlag(6, FieldMode.Read, name: "clr_tx_fifo_data") // Clear TX FIFO data interrupt
                .WithFlag(5, FieldMode.Read, name: "clr_rx_fifo_data") // Clear RX FIFO data interrupt
                .WithFlag(4, FieldMode.Read, name: "clr_tx_fifo_of") // Clear TX FIFO overflow interrupt
                .WithFlag(3, FieldMode.Read, name: "clr_tx_fifo_uf") // Clear TX FIFO underflow interrupt
                .WithFlag(2, FieldMode.Read, name: "clr_rx_fifo_of") // Clear RX FIFO overflow interrupt
                .WithFlag(1, FieldMode.Read, name: "clr_rx_fifo_uf") // Clear RX FIFO underflow interrupt
                .WithFlag(0, FieldMode.Read, name: "clr_done_int"); // Clear command completion interrupt

            Registers.CacheIntfCmd.Define(this, resetValue: 0xABB92BEB)
                .WithValueField(24, 8,
                    name: "cache_intf_reldscmd") // Bits 31:24 - Cache interface release/disable command
                .WithValueField(16, 8, name: "cache_intf_dscmd") // Bits 23:16 - Cache interface disable command
                .WithReservedBits(14, 2)
                .WithValueField(12, 2,
                    name: "cache_intf_rdcmd_bus_mode") // Bits 13:12 - Cache interface read command bus mode
                .WithValueField(8, 4,
                    name: "cache_intf_rdcmd_format") // Bits 11:8  - Cache interface read command format
                .WithValueField(0, 8, name: "cache_intf_rdcmd"); // Bits 7:0   - Cache interface read command

            Registers.DmaCntl.Define(this, resetValue: 0x00000000)
                .WithReservedBits(1, 31)
                .WithFlag(0, name: "tx_dma_en"); // Bit 0 - TX DMA Enable

            Registers.FifoCntl.Define(this, resetValue: 0x00000000)
                .WithFlag(31, name: "tx_fifo_flush", writeCallback: (_, value) => { if(value) txFifo.Clear(); }) // Bit 31 - TX FIFO Flush (RW)
                .WithReservedBits(22, 9) // Bits 30:22 - Reserved
                .WithFlag(21, FieldMode.Read, name: "tx_fifo_empty", valueProviderCallback: _ => txFifo.Count == 0) // Bit 21 - TX FIFO Empty (RO)
                .WithFlag(20, FieldMode.Read, name: "tx_fifo_full", valueProviderCallback: _ => txFifo.Count >= MaxFifoSize) // Bit 20 - TX FIFO Full (RO)
                .WithValueField(16, 4, FieldMode.Read, name: "tx_fifo_level", valueProviderCallback: _ => (uint)txFifo.Count) // Bits 19:16 - TX FIFO Level (RO)
                .WithFlag(15, name: "rx_fifo_flush", writeCallback: (_, value) => { if(value) rxFifo.Clear(); }) // Bit 15 - RX FIFO Flush (RW)
                .WithReservedBits(6, 9) // Bits 14:6 - Reserved
                .WithFlag(5, FieldMode.Read, name: "rx_fifo_empty", valueProviderCallback: _ => rxFifo.Count == 0) // Bit 5 - RX FIFO Empty (RO)
                .WithFlag(4, FieldMode.Read, name: "rx_fifo_full", valueProviderCallback: _ => rxFifo.Count >= MaxFifoSize) // Bit 4 - RX FIFO Full (RO)
                .WithValueField(0, 4, FieldMode.Read, name: "rx_fifo_level", valueProviderCallback: _ => (uint)rxFifo.Count); // Bits 3:0 - RX FIFO Level (RO)
        }

        private void OnAccessRequest(bool enabled)
        {
            if(!enabled)
                return;

            commandBusy.Value = true;
            accessAck.Value = true;
            try
            {
                ExecuteFcuCommand();
            }
            finally
            {
                commandBusy.Value = false;
                commandDone.Value = true;
                accessAck.Value = false;
            }
        }

        private void ExecuteFcuCommand()
        {
            ISPIPeripheral slave = null;
            if(ChildCollection.Count > 0)
            {
                slave = ChildCollection.Values.First();
            }

            if(slave == null)
            {
                this.Log(LogLevel.Warning, "QSPI: No SPI slave connected");
                return;
            }

            var cmd = (byte)commandCode.Value;
            var format = (uint)cmdFormat.Value;
            var addr = (uint)addressField.Value;
            var wdata = (uint)regWdata.Value;

            this.Log(LogLevel.Debug, "FCU Command: cmd=0x{0:X2}, format={1}, addr=0x{2:X6}", cmd, format, addr);

            // Send command byte
            slave.Transmit(cmd);

            // Handle different command formats according to datasheet
            switch(format)
            {
            case 0x0: // 8bit cmd only
                break;

            case 0x1: // 8bit cmd + 8bit read reg data
                regRdata.Value = slave.Transmit(0x00);
                break;

            case 0x2: // 8bit cmd + 16bit read reg data
            {
                uint data = (uint)(slave.Transmit(0x00) << 8);
                data |= slave.Transmit(0x00);
                regRdata.Value = data;
            }
            break;

            case 0x3: // 8bit cmd + 24bit read reg data
            {
                uint data = (uint)(slave.Transmit(0x00) << 16);
                data |= (uint)(slave.Transmit(0x00) << 8);
                data |= slave.Transmit(0x00);
                regRdata.Value = data;
                this.Log(LogLevel.Debug, "Read 24-bit data: 0x{0:X6}", data);
            }
            break;

            case 0x7: // 8bit cmd + 8bit write reg data
                slave.Transmit((byte)(wdata & 0xFF));
                break;

            case 0x8: // 8bit cmd + 16bit write reg data
                slave.Transmit((byte)((wdata >> 8) & 0xFF));
                slave.Transmit((byte)(wdata & 0xFF));
                break;

            case 0x9: // 8bit cmd + 24bit address
                slave.Transmit((byte)((addr >> 16) & 0xFF));
                slave.Transmit((byte)((addr >> 8) & 0xFF));
                slave.Transmit((byte)(addr & 0xFF));
                break;

            case 0xA: // 8bit cmd + 24bit addr + read data (use FIFO)
            case 0xB: // 8bit cmd + 24bit addr + dummy + read data (use FIFO)
            case 0xD: // 8bit cmd + 24bit addr + program data (use FIFO)
                      // Send address
                slave.Transmit((byte)((addr >> 16) & 0xFF));
                slave.Transmit((byte)((addr >> 8) & 0xFF));
                slave.Transmit((byte)(addr & 0xFF));

                // Use FIFO for data transfer
                ExecuteFifoTransaction(slave);
                break;

            default:
                this.Log(LogLevel.Warning, "Unsupported FCU command format: 0x{0:X}", format);
                break;
            }

            slave.FinishTransmission();
        }

        private void ExecuteFifoTransaction(ISPIPeripheral slave)
        {
            uint writeCount = (uint)writeByteCount.Value;
            uint readCount = (uint)readByteCount.Value;

            for(uint i = 0; i < Math.Max(writeCount, readCount); i++)
            {
                byte writeData = 0;
                if(i < writeCount && txFifo.Count > 0)
                {
                    writeData = txFifo.Dequeue();
                }

                byte readData = slave.Transmit(writeData);

                if(i < readCount)
                {
                    if(rxFifo.Count < MaxFifoSize)
                    {
                        rxFifo.Enqueue(readData);
                    }
                }
            }
        }

        private void OnWriteFifo(uint value)
        {
            if(txFifo.Count < MaxFifoSize)
            {
                txFifo.Enqueue((byte)((value >> 24) & 0xFF));
                if(txFifo.Count < MaxFifoSize)
                    txFifo.Enqueue((byte)((value >> 16) & 0xFF));
                if(txFifo.Count < MaxFifoSize)
                    txFifo.Enqueue((byte)((value >> 8) & 0xFF));
                if(txFifo.Count < MaxFifoSize)
                    txFifo.Enqueue((byte)(value & 0xFF));
            }
        }

        private uint OnReadFifo()
        {
            uint value = 0;
            for(int i = 0; i < 4 && rxFifo.Count > 0; i++)
            {
                byte data = rxFifo.Dequeue();
                value = (value << 8) | data;
            }
            return value;
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            txFifo.Clear();
            rxFifo.Clear();
            commandBusy.Value = false;
            commandDone.Value = false;
            accessAck.Value = false;
        }

        private IFlagRegisterField commandDone;
        private IFlagRegisterField commandBusy;
        private IFlagRegisterField accessAck;
        private IValueRegisterField writeByteCount;
        private IValueRegisterField readByteCount;
        private IValueRegisterField commandCode;
        private IValueRegisterField busMode;
        private IValueRegisterField cmdFormat;
        private IValueRegisterField addressField;
        private IValueRegisterField m7_0;
        private IValueRegisterField regWdata;
        private IValueRegisterField regRdata;

        private readonly Queue<byte> txFifo = new Queue<byte>();
        private readonly Queue<byte> rxFifo = new Queue<byte>();
        private const int MaxFifoSize = 16;

        private enum Registers : long
        {
            FcuCmd = 0x0,
            Address = 0x4,
            ByteNum = 0x8,
            WrFifo = 0xC,
            RdFifo = 0x10,
            DevicePara = 0x14,
            RegWdata = 0x18,
            RegRdata = 0x1C,
            IntMask = 0x20,
            IntUmask = 0x24,
            IntMaskStatus = 0x28,
            IntStatus = 0x2C,
            IntRawstatus = 0x30,
            IntClear = 0x34,
            CacheIntfCmd = 0x38,
            DmaCntl = 0x3C,
            FifoCntl = 0x40
        }
    }
}