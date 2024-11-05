//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.SPI
{
    // This peripheral only implements the generic operation mode
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte)]
    public class OpenTitan_SpiDevice : BasicDoubleWordPeripheral, IBytePeripheral, ISPIPeripheral, IKnownSize
    {
        public OpenTitan_SpiDevice(IMachine machine) : base(machine)
        {
            underlyingSramMemory = new ArrayMemory(BufferWindowSizeInDoublewords * 4);

            DefineRegisters();
            rxFifo = new SRAMCircularFifoRange(0u, InitialRxFifoBoundary, underlyingSramMemory);
            txFifo = new SRAMCircularFifoRange(InitialRxFifoBoundary + 1, InitialTxFifoBoundary, underlyingSramMemory);

            GenericRxFull = new GPIO();
            GenericRxWatermark = new GPIO();
            GenericTxWatermark = new GPIO();
            GenericRxError = new GPIO();
            GenericRxOverflow = new GPIO();
            GenericTxUnderflow = new GPIO();
            UploadCmdFifoNotEmpty = new GPIO();
            UploadPayloadNotEmpty = new GPIO();
            UploadPayloadOverflow = new GPIO();
            ReadBufferWatermark = new GPIO();
            ReadBufferFlip = new GPIO();
            TPMHeaderNotEmpty = new GPIO();

            FatalAlert = new GPIO();
            Reset();
        }

        // Byte accesses are needed for the fifo SRAM interface
        public void WriteByte(long offset, byte value)
        {
            if(offset >= (long)Registers.Buffer)
            {
                var bufferOffset = offset - (long)Registers.Buffer;
                if(IsOffsetInRxFifoRange(bufferOffset))
                {
                    this.Log(LogLevel.Debug, "Accesses the Rx fifo contents directly using the direct memory write");
                }
                // We may use direct sram memory write as the driver is responsible to handle the pointers in that case
                underlyingSramMemory.WriteByte(bufferOffset, value);
            }
            else
            {
                this.Log(LogLevel.Error, "Byte interface should only be used on the buffer. Ignoring write of value 0x{0:X}, to offset 0x{1:X}", value, offset);
            }
        }

        public byte ReadByte(long offset)
        {
            if(offset >= (long)Registers.Buffer)
            {
                var bufferOffset = offset - (long)Registers.Buffer;
                return underlyingSramMemory.ReadByte(bufferOffset);
            }
            else
            {
                this.Log(LogLevel.Error, "Byte interface should only be used on the buffer. Ignoring read from offset 0x{1:X}", offset);
                return 0;
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= (long)Registers.Buffer)
            {
                // Access to the sram buffer
                var bufferOffset = offset - (long)Registers.Buffer;
                if(IsOffsetInTxFifoRange(bufferOffset))
                {
                    var dataInBytes = BitHelper.GetBytesFromValue(value, sizeof(uint));
                    if(!txOrderLsbFirst.Value)
                    {
                        Array.Reverse(dataInBytes);
                    }
                    foreach(var b in dataInBytes)
                    {
                        var txFifoStatus = txFifo.WriteByte(b);
                    }
                }
                else
                {
                    underlyingSramMemory.WriteDoubleWord(offset - (long)Registers.Buffer, value);
                }
            }
            else
            {
                base.WriteDoubleWord(offset, value);
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(offset >= (long)Registers.Buffer)
            {
                // Acces to the sram buffer
                this.Log(LogLevel.Debug, "Reading from sram memory; value at addr {0}", offset - 0x1000);
                var readValue = underlyingSramMemory.ReadDoubleWord(offset - (long)Registers.Buffer);
                if(IsOffsetInRxFifoRange(offset))
                {
                    readValue = Misc.ByteArrayRead(0, BitHelper.GetBytesFromValue(readValue, sizeof(uint)));
                }
                return readValue;
            }
            else
            {
                return base.ReadDoubleWord(offset);
            }
        }

        public override void Reset()
        {
            base.Reset();
            FatalAlert.Unset();
        }

        public byte Transmit(byte data)
        {
            byte output = 0;
            SRAMCircularFifoRange.FifoStatus rxFifoStatus, txFifoStatus = SRAMCircularFifoRange.FifoStatus.Empty;

            if(txFifo.CurrentDepth > 0)
            {
                txFifoStatus = txFifo.ReadByte(out output);

                if(txFifo.CurrentDepth < txFifoWatermarkLevel.Value)
                {
                    txWatermarkInterruptState.Value = true;
                }
            }
            else
            {
                txFifoStatus = SRAMCircularFifoRange.FifoStatus.Underflow;
            }

            rxFifoStatus = rxFifo.WriteByte(data);

            if(rxFifo.CurrentDepth > rxFifoWatermarkLevel.Value)
            {
                rxWatermarkInterruptState.Value = true;
            }

            HandleFifoStatusesAndUpdateInterrupts(rxFifoStatus, txFifoStatus);

            return output;
        }

        private bool IsOffsetInRxFifoRange(long bufferOffset)
        {
            return (bufferOffset >= rxFifo.Base) && (bufferOffset <= rxFifo.Limit);
        }

        private bool IsOffsetInTxFifoRange(long offset)
        {
            return (offset >= txFifo.Base) && (offset <= txFifo.Limit);
        }

        /* Translates the fifo states into interrupts */
        private void HandleFifoStatusesAndUpdateInterrupts(SRAMCircularFifoRange.FifoStatus rxFifoStatus, SRAMCircularFifoRange.FifoStatus txFifoStatus)
        {
            switch(rxFifoStatus)
            {
                case SRAMCircularFifoRange.FifoStatus.Overflow:
                    rxOverflowInterruptState.Value = true;
                    break;
                case SRAMCircularFifoRange.FifoStatus.Full:
                    rxFullInterruptState.Value = true;
                    break;
                default:
                    break;
            }
            switch(txFifoStatus)
            {
                case SRAMCircularFifoRange.FifoStatus.Underflow:
                    txUnderflowInterruptState.Value = true;
                    break;
                default:
                    break;
            }
            UpdateInterrupts();
        }

        public void FinishTransmission()
        {
            // Intentionaly left blank
        }

        // Common Interrupt Offsets
        public GPIO GenericRxFull { get; }
        public GPIO GenericRxWatermark { get; }
        public GPIO GenericTxWatermark { get; }
        public GPIO GenericRxError { get; }
        public GPIO GenericRxOverflow { get; }
        public GPIO GenericTxUnderflow { get; }
        public GPIO UploadCmdFifoNotEmpty { get; }
        public GPIO UploadPayloadNotEmpty { get; }
        public GPIO UploadPayloadOverflow { get; }
        public GPIO ReadBufferWatermark { get; }
        public GPIO ReadBufferFlip { get; }
        public GPIO TPMHeaderNotEmpty { get; }
        public GPIO FatalAlert { get; private set; }

        public long Size => 0x2000;

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out rxFullInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_rx_full")
                .WithFlag(1, out rxWatermarkInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_rx_watermark")
                .WithFlag(2, out txWatermarkInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_tx_watermark")
                .WithFlag(3, out rxErrorInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_rx_error")
                .WithFlag(4, out rxOverflowInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_rx_overflow")
                .WithFlag(5, out txUnderflowInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "generic_tx_underflow")
                .WithFlag(6, out cmdfifoNotEmptyInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "upload_cmdfifo_not_empty")
                .WithFlag(7, out payloadNotEmptyInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "upload_payload_not_empty")
                .WithFlag(8, out payloadOverflowInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "upload_payload_overflow")
                .WithFlag(9, out readbufWatermarkInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "readbuf_watermark")
                .WithFlag(10, out readbufFlipInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "readbuf_flip")
                .WithFlag(11, out tpmHeaderNotEmptyInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "tpm_header_not_empty")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out rxFullInterruptEnable, name: "generic_rx_full")
                .WithFlag(1, out rxWatermarkInterruptEnable, name: "generic_rx_watermark")
                .WithFlag(2, out txWatermarkInterruptEnable, name: "generic_tx_watermark")
                .WithFlag(3, out rxErrorInterruptEnable, name: "generic_rx_error")
                .WithFlag(4, out rxOverflowInterruptEnable, name: "generic_rx_overflow")
                .WithFlag(5, out txUnderflowInterruptEnable, name: "generic_tx_underflow")
                .WithFlag(6, out cmdfifoNotEmptyInterruptEnable, name: "upload_cmdfifo_not_empty")
                .WithFlag(7, out payloadNotEmptyInterruptEnable, name: "upload_payload_not_empty")
                .WithFlag(8, out payloadOverflowInterruptEnable, name: "upload_payload_overflow")
                .WithFlag(9, out readbufWatermarkInterruptEnable, name: "readbuf_watermark")
                .WithFlag(10, out readbufFlipInterruptEnable, name: "readbuf_flip")
                .WithFlag(11, out tpmHeaderNotEmptyInterruptEnable, name: "tpm_header_not_empty")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { rxFullInterruptState.Value = val; }, name: "generic_rx_full")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { rxWatermarkInterruptState.Value = val; }, name: "generic_rx_watermark")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { txWatermarkInterruptState.Value = val; }, name: "generic_tx_watermark")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { rxErrorInterruptState.Value = val; }, name: "generic_rx_error")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { rxOverflowInterruptState.Value = val; }, name: "generic_rx_overflow")
                .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { txUnderflowInterruptState.Value = val; }, name: "generic_tx_underflow")
                .WithFlag(6, FieldMode.Write, writeCallback: (_, val) => { cmdfifoNotEmptyInterruptState.Value = val; }, name: "upload_cmdfifo_not_empty")
                .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { payloadNotEmptyInterruptState.Value = val; }, name: "upload_payload_not_empty")
                .WithFlag(8, FieldMode.Write, writeCallback: (_, val) => { payloadOverflowInterruptState.Value = val; }, name: "upload_payload_overflow")
                .WithFlag(9, FieldMode.Write, writeCallback: (_, val) => { readbufWatermarkInterruptState.Value = val; }, name: "readbuf_watermark")
                .WithFlag(10, FieldMode.Write, writeCallback: (_, val) => { readbufFlipInterruptState.Value = val; }, name: "readbuf_flip")
                .WithFlag(11, FieldMode.Write, writeCallback: (_, val) => { tpmHeaderNotEmptyInterruptState.Value = val; }, name: "tpm_header_not_empty")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts(); });

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                .WithReservedBits(1, 31);

            Registers.Control.Define(this, 0x80000010)
                .WithFlag(0, name: "ABORT")
                .WithReservedBits(1, 3)
                .WithEnumField<DoubleWordRegister, DeviceMode>(4, 2, out mode,
                    writeCallback: (_, val) =>
                    {
                        if(val != DeviceMode.Fw)
                        {
                            this.Log(LogLevel.Error, "{0} operation mode is not implemented. Setting mode back to FwMode", val);
                            mode.Value = DeviceMode.Fw;
                        }
                    }, name: "MODE")
                .WithReservedBits(6, 9)
                .WithFlag(16, writeCallback: (_, val) => { if(val) txFifo.ResetPointers(); }, name: "rst_txfifo")
                .WithFlag(17, writeCallback: (_, val) => { if(val) rxFifo.ResetPointers(); }, name: "rst_rxfifo")
                .WithReservedBits(18, 13)
                .WithTaggedFlag("sram_clk_en", 31);

            Registers.Configuration.Define(this, 0x7f00)
                .WithTaggedFlag("CPOL", 0)
                .WithTaggedFlag("CPHA", 1)
                .WithFlag(2, out txOrderLsbFirst, name:"tx_order")
                .WithFlag(3, out rxOrderLsbFirst, name:"rx_order")
                .WithReservedBits(4, 4)
                .WithTag("timer_v", 8, 8)
                .WithTaggedFlag("addr_4b_en", 16)
                .WithTaggedFlag("mailbox_en", 24)
                .WithReservedBits(25, 7);

            Registers.FifoLevel.Define(this, 0x80)
                // Despite the misleading name this register holds the watermark levels - at least thats what the spec suggests
                .WithValueField(0, 16, out rxFifoWatermarkLevel, name: "rxlvl")    // "If the RX SRAM FIFO level exceeds this value, it triggers interrupt."
                .WithValueField(16, 16, out txFifoWatermarkLevel, name: "txlvl");  // "If the TX SRAM FIFO level drops below this value, it triggers interrupt."

            Registers.AsyncFifoLevel.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => rxFifo.CurrentDepth, name: "rxlvl")
                .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => txFifo.CurrentDepth, name: "txlvl")
                .WithReservedBits(24, 8);

            Registers.SPIDevicestatus.Define(this, 0x3a)
                .WithFlag(0, FieldMode.Read, valueProviderCallback : _ => rxFifo.IsFull, name: "rxf_full")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => rxFifo.IsEmpty, name: "rxf_empty")
                .WithFlag(2, FieldMode.Read, valueProviderCallback : _ => txFifo.IsFull, name: "txf_full")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => txFifo.IsEmpty, name: "txf_empty")
                .WithFlag(4, FieldMode.Read, name: "abort_done")
                .WithFlag(5, FieldMode.Read, name: "csb")
                .WithReservedBits(6, 26);

            Registers.ReceiverFifoSramPointers.Define(this)
                .WithValueField(0, 16, valueProviderCallback: _ => rxFifo.ReadPointerWithPhaseBit, changeCallback: (_, val) =>
                    {
                        this.Log(LogLevel.Debug, "Setting the read pointer to {0:x}", val);
                        if(rxFifo.TrySetReadPointer(val))
                        {
                            this.Log(LogLevel.Error, "Pointer outside of the range. This will be ignored");
                        }
                    }, name: "RPTR")
                .WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => rxFifo.WritePointerWithPhaseBit, name: "WPTR");

            Registers.TransmitterFifoSramPointers.Define(this)
                .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ =>  txFifo.ReadPointerWithPhaseBit, name: "RPTR")
                .WithValueField(16, 16, valueProviderCallback: _ => txFifo.WritePointerWithPhaseBit, changeCallback: (_, val) =>
                    {
                        this.Log(LogLevel.Debug, "Setting the write pointer to {0:x}", val);
                        if(txFifo.TrySetWritePointer(val))
                        {
                            this.Log(LogLevel.Error, "Pointer outside of the range. This will be ignored");
                        }
                    }, name: "WPTR");

            Registers.ReceiverFifoSramAddresses.Define(this, 0x1fc0000)
                .WithValueField(0, 16, out rxFifoBase, name: "base")
                .WithValueField(16, 16, out rxFifoLimit, name: "limit")
                .WithWriteCallback((_, __) =>
                    {
                        if(!rxFifo.TryUpdateParameters((uint)rxFifoBase.Value, (uint)rxFifoLimit.Value))
                        {
                            this.Log(LogLevel.Error, "Parameters were rejected as an invalid range");
                        }
                    });

            Registers.TransmitterFifoSramAddresses.Define(this, 0x3fc0200)
                .WithValueField(0, 16, out txFifoBase, name: "base")
                .WithValueField(16, 16, out txFifoLimit, name: "limit")
                .WithWriteCallback((_, __) =>
                    {
                        if(!txFifo.TryUpdateParameters((uint)txFifoBase.Value, (uint)txFifoLimit.Value))
                        {
                            this.Log(LogLevel.Error, "Parameters were rejected as an invalid range");
                        }
                    });

            Registers.InterceptEnable.Define(this)
                .WithTaggedFlag("status", 0)
                .WithTaggedFlag("jedec", 1)
                .WithTaggedFlag("sfdp", 2)
                .WithTaggedFlag("mbx", 3)
                .WithReservedBits(4, 28);

            Registers.LastReadAddress.Define(this)
                .WithTag("addr", 0, 32);

            Registers.FlashStatus.Define(this)
                .WithTaggedFlag("busy", 0)
                .WithTag("status", 1, 23)
                .WithReservedBits(24, 8);

            Registers.JedecCc.Define(this, 0x7f)
                .WithTag("cc", 0, 8)
                .WithTag("num_cc", 8, 8)
                .WithReservedBits(16, 16);

            Registers.JedecId.Define(this)
                .WithTag("id", 0, 16)
                .WithTag("mf", 16, 8)
                .WithReservedBits(24, 8);

            Registers.ReadThreshold.Define(this)
                .WithTag("threshold", 0, 10)
                .WithReservedBits(10, 22);

            Registers.MailboxAddress.Define(this)
                .WithTag("addr", 0, 32);

            Registers.UploadStatus.Define(this)
                .WithTag("cmdfifo_depth", 0, 5)
                .WithTaggedFlag("cmdfifo_notempty", 7)
                .WithTag("addrfifo_depth", 8, 5)
                .WithTaggedFlag("addrfifo_notempty", 5)
                .WithReservedBits(16, 16);

            Registers.UploadStatus2.Define(this)
                .WithTag("payload_depth", 0, 9)
                .WithTag("payload_start_idx", 16, 8)
                .WithReservedBits(24, 8);

            Registers.UploadCmdFifo.Define(this)
                .WithTag("data", 0, 8)
                .WithReservedBits(8, 24);

            Registers.UploadAddrFifo.Define(this)
                .WithTag("data", 0, 32);

            Registers.CommandFilter.DefineMany(this, SpiDevicesCount, (register, idx) =>
                {
                    register
                    .WithTaggedFlag($"filter_{0 + 32 * idx}", 0)
                    .WithTaggedFlag($"filter_{1 + 32 * idx}", 1)
                    .WithTaggedFlag($"filter_{2 + 32 * idx}", 2)
                    .WithTaggedFlag($"filter_{3 + 32 * idx}", 3)
                    .WithTaggedFlag($"filter_{4 + 32 * idx}", 4)
                    .WithTaggedFlag($"filter_{5 + 32 * idx}", 5)
                    .WithTaggedFlag($"filter_{6 + 32 * idx}", 6)
                    .WithTaggedFlag($"filter_{7 + 32 * idx}", 7)
                    .WithTaggedFlag($"filter_{8 + 32 * idx}", 8)
                    .WithTaggedFlag($"filter_{9 + 32 * idx}", 9)
                    .WithTaggedFlag($"filter_{10 + 32 * idx}", 10)
                    .WithTaggedFlag($"filter_{11 + 32 * idx}", 11)
                    .WithTaggedFlag($"filter_{12 + 32 * idx}", 12)
                    .WithTaggedFlag($"filter_{13 + 32 * idx}", 13)
                    .WithTaggedFlag($"filter_{14 + 32 * idx}", 14)
                    .WithTaggedFlag($"filter_{15 + 32 * idx}", 15)
                    .WithTaggedFlag($"filter_{16 + 32 * idx}", 16)
                    .WithTaggedFlag($"filter_{17 + 32 * idx}", 17)
                    .WithTaggedFlag($"filter_{18 + 32 * idx}", 18)
                    .WithTaggedFlag($"filter_{19 + 32 * idx}", 19)
                    .WithTaggedFlag($"filter_{20 + 32 * idx}", 20)
                    .WithTaggedFlag($"filter_{21 + 32 * idx}", 21)
                    .WithTaggedFlag($"filter_{22 + 32 * idx}", 22)
                    .WithTaggedFlag($"filter_{23 + 32 * idx}", 23)
                    .WithTaggedFlag($"filter_{24 + 32 * idx}", 24)
                    .WithTaggedFlag($"filter_{25 + 32 * idx}", 25)
                    .WithTaggedFlag($"filter_{26 + 32 * idx}", 26)
                    .WithTaggedFlag($"filter_{27 + 32 * idx}", 27)
                    .WithTaggedFlag($"filter_{28 + 32 * idx}", 28)
                    .WithTaggedFlag($"filter_{29 + 32 * idx}", 29)
                    .WithTaggedFlag($"filter_{30 + 32 * idx}", 30)
                    .WithTaggedFlag($"filter_{31 + 32 * idx}", 31);
                });

            Registers.AddressSwapMask.Define(this)
                .WithTag("mask", 0, 32);

            Registers.AddressSwapData.Define(this)
                .WithTag("data", 0, 32);

            Registers.PayloadSwapMask.Define(this)
                .WithTag("mask", 0, 32);

            Registers.PayloadSwapData.Define(this)
                .WithTag("data", 0, 32);

            Registers.CommandInfo.DefineMany(this, DeviceCmdInfoCount, (register, idx) =>
                {
                    register
                    .WithTag("opcode_{idx}", 0, 8)
                    .WithTag("addr_mode_{idx}", 8, 2)
                    .WithTaggedFlag($"addr_swap_en_{idx}", 10)
                    .WithTaggedFlag($"mbyte_en_{idx}", 11)
                    .WithTag("dummy_size_{idx}", 12, 3)
                    .WithTaggedFlag($"dummy_en_{idx}", 15)
                    .WithTag("payload_en_{idx}", 16, 4)
                    .WithTaggedFlag($"payload_dir_{idx}", 20)
                    .WithTaggedFlag($"payload_swap_en_{idx}", 21)
                    .WithReservedBits(22, 2)
                    .WithTaggedFlag($"upload_{idx}", 24)
                    .WithTaggedFlag($"busy_{idx}", 25)
                    .WithReservedBits(26, 5)
                    .WithTaggedFlag($"valid_{idx}", 31);
                });

            Registers.CommandInfoEn4b.Define(this)
                .WithTag("opcode", 0, 8)
                .WithTaggedFlag("valid", 31);

            Registers.OpcodeForEX4B.Define(this)
                .WithTag("opcode", 0, 8)
                .WithTaggedFlag("valid", 31);

            Registers.OpcodeforWriteEnable.Define(this)
                .WithTag("opcode", 0, 8)
                .WithTaggedFlag("valid", 31);

            Registers.OpcodeForWriteDisable.Define(this)
                .WithTag("opcode", 0, 8)
                .WithTaggedFlag("valid", 31);

            // TPM capabilities not implemented
            Registers.TPMCapability.Define(this, 0x20100)
                .WithTag("rev", 0, 8)
                .WithTaggedFlag("locality", 8)
                .WithTag("max_xfer_size", 16, 3)
                .WithReservedBits(19, 13);

            Registers.TPMConfig.Define(this)
                .WithTaggedFlag("en", 0)
                .WithTaggedFlag("tpm_mode", 1)
                .WithTaggedFlag("hw_reg_dis", 2)
                .WithTaggedFlag("tpm_reg_chk_dis", 3)
                .WithTaggedFlag("invalid_locality", 4)
                .WithReservedBits(5, 27);

            Registers.TPMStatus.Define(this)
                .WithTaggedFlag("cmdaddr_notempty", 0)
                .WithTaggedFlag("rdfifo_notempty", 1)
                .WithTag("rdfifo_depth", 4, 3)
                .WithTag("wrfifo_depth", 8, 3)
                .WithReservedBits(11, 21);

            Registers.TPMAccess0.Define(this)
                .WithTag("access_0", 0, 8)
                .WithTag("access_1", 8, 8)
                .WithTag("access_2", 16, 8)
                .WithTag("access_3", 24, 8);

            Registers.TPMAccess1.Define(this)
                .WithTag("access_4", 0, 8)
                .WithReservedBits(8, 24);

            Registers.TPMSts.Define(this)
                .WithTag("sts", 0, 32);

            Registers.TPMIntfCapability.Define(this)
                .WithTag("intf_capability", 0, 32);

            Registers.TPMIntCapability.Define(this)
                .WithTag("int_enable", 0, 32);

            Registers.TPMIntVector.Define(this)
                .WithTag("int_vector", 0, 8)
                .WithReservedBits(8, 24);

            Registers.TPMIntStatus.Define(this)
                .WithTag("int_status", 0, 32);

            Registers.TPMDidVid.Define(this)
                .WithTag("vid", 0, 16)
                .WithTag("did", 16, 16);

            Registers.TPMRid.Define(this)
                .WithTag("rid", 0, 8)
                .WithReservedBits(8, 24);

            Registers.TPMCommandAndAddressBuffer.Define(this)
                .WithTag("addr", 0, 24)
                .WithTag("cmd", 24, 8);

            Registers.TPMReadFifo.Define(this)
                .WithTag("value", 0, 8)
                .WithReservedBits(8, 24);

            Registers.TPMWriteFifo.Define(this)
                .WithTag("value", 0, 8)
                .WithReservedBits(8, 24);

            Registers.Buffer.DefineMany(this, BufferWindowSizeInDoublewords, (register, idx) =>
                {
                    // This range is handled by the read functions
                    register.WithTag($"SPIinternalbuffer{idx}", 0, 3);
                });
        }

        private void UpdateInterrupts()
        {
            GenericRxFull.Set(rxFullInterruptState.Value && rxFullInterruptEnable.Value);
            GenericRxWatermark.Set(rxWatermarkInterruptState.Value && rxWatermarkInterruptEnable.Value);
            GenericTxWatermark.Set(txWatermarkInterruptState.Value && txWatermarkInterruptEnable.Value);
            GenericRxOverflow.Set(rxOverflowInterruptState.Value && rxOverflowInterruptEnable.Value);
            GenericTxUnderflow.Set(txUnderflowInterruptState.Value && txUnderflowInterruptEnable.Value);
            // Below interrupts are not implemented and are here just for the interrupt test sake
            GenericRxError.Set(rxErrorInterruptState.Value && rxErrorInterruptEnable.Value);
            UploadCmdFifoNotEmpty.Set(cmdfifoNotEmptyInterruptState.Value && cmdfifoNotEmptyInterruptEnable.Value);
            UploadPayloadNotEmpty.Set(payloadNotEmptyInterruptState.Value && payloadNotEmptyInterruptEnable.Value);
            UploadPayloadOverflow.Set(payloadOverflowInterruptState.Value && payloadOverflowInterruptEnable.Value);
            ReadBufferWatermark.Set(readbufWatermarkInterruptState.Value && readbufWatermarkInterruptEnable.Value);
            ReadBufferFlip.Set(readbufFlipInterruptState.Value && readbufFlipInterruptEnable.Value);
            TPMHeaderNotEmpty.Set(tpmHeaderNotEmptyInterruptState.Value && tpmHeaderNotEmptyInterruptEnable.Value);
        }

        // Sram Entries. Word size is 32bit width.
        private const uint BufferWindowSizeInDoublewords = 1024;
        private const uint InitialRxFifoBoundary = 2047;
        private const uint InitialTxFifoBoundary = 4095;

        // Define the number of Command Info slots.
        private const uint DeviceCmdInfoCount = 24;

        // Define the number of SPI_DEVICE
        private const uint SpiDevicesCount = 8;

        // The number of locality TPM module supports.
        private const uint SpiDeviceNumLocality = 5;

        private IFlagRegisterField rxFullInterruptState;
        private IFlagRegisterField rxWatermarkInterruptState;
        private IFlagRegisterField txWatermarkInterruptState;
        private IFlagRegisterField rxErrorInterruptState;
        private IFlagRegisterField rxOverflowInterruptState;
        private IFlagRegisterField txUnderflowInterruptState;
        private IFlagRegisterField cmdfifoNotEmptyInterruptState;
        private IFlagRegisterField payloadNotEmptyInterruptState;
        private IFlagRegisterField payloadOverflowInterruptState;
        private IFlagRegisterField readbufWatermarkInterruptState;
        private IFlagRegisterField readbufFlipInterruptState;
        private IFlagRegisterField tpmHeaderNotEmptyInterruptState;
        private IFlagRegisterField rxFullInterruptEnable;
        private IFlagRegisterField rxWatermarkInterruptEnable;
        private IFlagRegisterField txWatermarkInterruptEnable;
        private IFlagRegisterField rxErrorInterruptEnable;
        private IFlagRegisterField rxOverflowInterruptEnable;
        private IFlagRegisterField txUnderflowInterruptEnable;
        private IFlagRegisterField cmdfifoNotEmptyInterruptEnable;
        private IFlagRegisterField payloadNotEmptyInterruptEnable;
        private IFlagRegisterField payloadOverflowInterruptEnable;
        private IFlagRegisterField readbufWatermarkInterruptEnable;
        private IFlagRegisterField readbufFlipInterruptEnable;
        private IFlagRegisterField tpmHeaderNotEmptyInterruptEnable;
        private IFlagRegisterField txOrderLsbFirst;
        private IFlagRegisterField rxOrderLsbFirst;
        private IEnumRegisterField<DeviceMode> mode;

        private IValueRegisterField rxFifoBase;
        private IValueRegisterField rxFifoLimit;
        private IValueRegisterField txFifoBase;
        private IValueRegisterField txFifoLimit;
        private IValueRegisterField rxFifoWatermarkLevel;
        private IValueRegisterField txFifoWatermarkLevel;

        private SRAMCircularFifoRange rxFifo;
        private SRAMCircularFifoRange txFifo;
        private ArrayMemory underlyingSramMemory;

        private enum DeviceMode
        {
            Fw = 0x0,
            Flash = 0x1,
            Passthrough = 0x2,
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            Control = 0x10,
            Configuration = 0x14,
            FifoLevel = 0x18,
            AsyncFifoLevel = 0x1c,
            SPIDevicestatus = 0x20,
            ReceiverFifoSramPointers = 0x24,
            TransmitterFifoSramPointers = 0x28,
            ReceiverFifoSramAddresses = 0x2c,
            TransmitterFifoSramAddresses = 0x30,
            InterceptEnable = 0x34,
            LastReadAddress = 0x38,
            FlashStatus = 0x3c,
            JedecCc = 0x40,
            JedecId = 0x44,
            ReadThreshold = 0x48,
            MailboxAddress = 0x4c,
            UploadStatus = 0x50,
            UploadStatus2 = 0x54,
            UploadCmdFifo = 0x58,
            UploadAddrFifo = 0x5c,
            CommandFilter = 0x60,
            AddressSwapMask = 0x80,
            AddressSwapData = 0x84,
            PayloadSwapMask = 0x88,
            PayloadSwapData = 0x8c,
            CommandInfo = 0x90,
            CommandInfoEn4b = 0xf0,
            OpcodeForEX4B = 0xf4,
            OpcodeforWriteEnable = 0xf8,
            OpcodeForWriteDisable = 0xfc,
            TPMCapability = 0x800,
            TPMConfig = 0x804,
            TPMStatus = 0x808,
            TPMAccess0 = 0x80c,
            TPMAccess1 = 0x810,
            TPMSts = 0x814,
            TPMIntfCapability = 0x818,
            TPMIntCapability = 0x81c,
            TPMIntVector = 0x820,
            TPMIntStatus = 0x824,
            TPMDidVid = 0x828,
            TPMRid = 0x82c,
            TPMCommandAndAddressBuffer = 0x830,
            TPMReadFifo = 0x834,
            TPMWriteFifo = 0x838,
            Buffer = 0x1000,
        }

        private class SRAMCircularFifoRange
        {
            public SRAMCircularFifoRange(uint baseOffset, uint limit, ArrayMemory underlyingMemory)
            {
                this.underlyingMemory = underlyingMemory;
                if(!TryUpdateParameters(baseOffset, limit))
                {
                    throw new ArgumentException("SRAMCircularFifo constructor parameters were rejected." + 
                                                " The range does not fit into the underlying memory or base is bigger than limit");
                }
            }

            public bool TryUpdateParameters(uint baseOffset, uint limitOffset)
            {
                var baseOffsetInvalid = baseOffset >= (underlyingMemory.Size  * 8);
                var limitOffsetInvalid = limitOffset >= (underlyingMemory.Size * 8); 
                var addressesUnordered = baseOffset > limitOffset; 

                if(baseOffsetInvalid || limitOffsetInvalid || addressesUnordered)
                {
                    return false;
                }
                UpdateParameters((ushort)baseOffset, (ushort)limitOffset);
                return true;
            }

            public void ResetPointers()
            {
                readPointer = 0;
                readPhase = false;
                writePointer = 0;
                writePhase = false;

                status = FifoStatus.Empty;
            }

            public bool TrySetWritePointer(ulong newValue)
            {
                ExtractPhaseAndPointer(newValue, out var phase, out var pointer);
                if(PointerInDefinedRange(pointer))
                {
                    writePointer = checked((ushort)pointer);
                    writePhase = phase;
                    return true;
                }
                return false;
            }

            public FifoStatus WriteByte(byte data)
            {
                underlyingMemory.WriteByte(baseOffset + writePointer, data);

                if(writePointer == limitOffset)
                {
                    writePointer = baseOffset;
                    writePhase = !writePhase;
                }
                else
                {
                    writePointer++;
                }
                UpdateStatus();
                return status;
            }

            public FifoStatus ReadByte(out byte value)
            {
                value = underlyingMemory.ReadByte(baseOffset + readPointer);
                readPointer += 1;

                if(readPointer == limitOffset)
                {
                    readPointer = baseOffset;
                    readPhase = !readPhase;
                }
                UpdateStatus();
                return status;
            }

            public bool TrySetReadPointer(ulong newValue)
            {
                ExtractPhaseAndPointer(newValue, out var phase, out var pointer);
                if(PointerInDefinedRange(pointer))
                {
                    readPointer = checked((ushort)pointer);
                    readPhase = phase;
                    return true;
                }
                return false;
            }

            // As the phase bit is separated from the address, we need to put it back in place
            public uint ReadPointerWithPhaseBit => readPointer | (uint)((readPhase ? 1 : 0) << 11);
            public uint WritePointerWithPhaseBit => writePointer | (uint)((writePhase ? 1 : 0) << 11);
            public bool IsEmpty => (writePointer == readPointer) && (writePhase == readPhase);
            public bool IsFull => (writePointer == readPointer) && (writePhase != readPhase);
            public uint Base => baseOffset;
            public uint Limit => limitOffset;

            public uint CurrentDepth
            {
                get
                {
                    // The depth is guaranteed to be positive as we don't ever read on empty buffer
                    var depth = writePointer - readPointer;
                    var phasesDiffer = writePhase != readPhase;

                    if(phasesDiffer)
                    {
                        depth += limitOffset + baseOffset;
                    }
                    return (uint)depth;
                }
            }

            private void UpdateParameters(ushort baseOffset, ushort limitOffset)
            {
                this.baseOffset = baseOffset;
                this.limitOffset = limitOffset;
                ResetPointers();
            }

            private void ExtractPhaseAndPointer(ulong value, out bool phase, out ushort pointer)
            {
                phase = BitHelper.IsBitSet(value, PointerBitsCount + 1);
                pointer = checked((ushort)(BitHelper.GetValue(value, 0, PointerBitsCount)));
            }

            private bool PointerInDefinedRange(uint newValue)
            {
                return (newValue < (limitOffset - baseOffset));
            }

            private void UpdateStatus()
            {
                var phasesDifferent = (readPhase != writePhase);
                var fifoSize = limitOffset - baseOffset + 1;
                var addresessDiff = (int)((writePointer - readPointer) + (phasesDifferent ? (fifoSize) : 0));
                var lastStatus = status;

                status = FifoStatus.Normal;

                if(addresessDiff == 0)
                {
                    status = FifoStatus.Empty;
                }
                else if(addresessDiff == fifoSize)
                {
                    status = FifoStatus.Full;
                }
                else if(addresessDiff > fifoSize)
                {
                    status = FifoStatus.Overflow;
                }
                else if(addresessDiff < 0)
                {
                    status = FifoStatus.Underflow;
                }
            }

            private ushort baseOffset, limitOffset;
            private ushort readPointer;
            private ushort writePointer;

            // This two denotes the state of the buffer pointers. They are flipped on every overflow
            private bool readPhase;
            private bool writePhase;
            private FifoStatus status;

            private readonly ArrayMemory underlyingMemory;

            private const int PointerBitsCount = 11;

            public enum FifoStatus
            {
                Empty,
                Normal,
                Full,
                Overflow,
                Underflow,
            }
        }
    } // End class OpenTitan_SpiDevice
}
