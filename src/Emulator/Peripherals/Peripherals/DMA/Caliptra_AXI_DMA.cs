//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA;

public class Caliptra_AXI_DMA : BasicDoubleWordPeripheral, IKnownSize, IDMA
{
    public Caliptra_AXI_DMA(IMachine machine, ulong mailboxSramBase, ICPU requestContext = null) : base(machine)
    {
        Init(mailboxSramBase, requestContext);
    }

    public Caliptra_AXI_DMA(IMachine machine, IMemory mailboxSramBase, ICPU requestContext = null) : base(machine)
    {
        var registrationPoints = (machine.SystemBus as SystemBus)?.GetRegistrationPoints(mailboxSramBase, requestContext);
        if(registrationPoints is null || !registrationPoints.Any())
        {
            throw new ConstructionException($"No accessible registration point available for {nameof(mailboxSramBase)}");
        }
        if(registrationPoints.Count() > 1)
        {
            throw new ConstructionException($"More than one registration point available for {nameof(mailboxSramBase)}");
        }
        Init(registrationPoints.Single().StartingPoint, requestContext);
    }

    public void SetPayloadAvailable(bool status) => payloadAvailable.Value = status;

    public void SetImageActivated(bool status) => imageActivated.Value = status;

    public override void Reset()
    {
        internalFifo.Clear();
        transferRequest = null;
        base.Reset();
    }

    public void RequestTransfer(int channel)
    {
        if(transferRequest is null)
        {
            // No pending transfer, do nothing
            this.DebugLog("No configured transfer, request ignored");
            return;
        }

        var count = (int)byteCount.Value;
        if(writeRoute.Value == TransferRoute.InternalFIFO && count != (internalFifo.Count * sizeof(uint)))
        {
            // Delay transfer until data becomes available
            this.DebugLog("Not enough data to initiate transfer");
            return;
        }

        // There is transfer data that needs to be pushed
        if(writeRoute.Value == TransferRoute.InternalFIFO && internalFifo.Any())
        {
            var takenSlots = count - internalFifo.Count * sizeof(uint);
            var newData = PrepareTransferArray(internalFifo.Count * sizeof(uint));
            DebugHelper.Assert(newData.Length + takenSlots == transferRequest?.Source.Array.Length);

            newData.CopyTo(transferRequest.Value.Source.Array, 0);
        }

        // We do not model delays. If they are necessary, use e.g. `machine.LocalTimeSource.ExecuteInNearestSyncedState`
        this.Log(LogLevel.Debug, "Issuing previously configured DMA transfer");

        if(transferRequest is null)
        {
            this.Log(LogLevel.Error, "DMA state was cleared before the transfer could complete!");
            return;
        }

        var resp = engine.IssueCopy(transferRequest.Value, requestContext);

        // If there is a WriteData buffer present, we need to push it into internal read FIFO
        if(resp.WriteData is not null)
        {
            this.Log(LogLevel.Noisy, "DMA received data of size {0} bytes", resp.WriteData.Length);
            internalFifo.EnqueueRange(BitHelper.ToUInt32Array(resp.WriteData, true));
        }
        transferRequest = null;
        busy.Value = false;
    }

    public long Size => 0xa3c;

    public int NumberOfChannels => 0x1;

    public const int DefaultDmaChannel = 0x0;

    private void Init(ulong mailboxSramBase, ICPU requestContext = null)
    {
        this.requestContext = requestContext;
        this.mailboxBase = mailboxSramBase;
        this.engine = new DmaEngine(machine.SystemBus);

        DefineRegisters();
    }

    private void DefineRegisters()
    {
        Registers.Id.Define(this, 0x67768068)
            .WithValueField(0, 32, FieldMode.Read, name: "Identifier");

        Registers.Cap.Define(this)
            // Arbitrarily set large value
            .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: _ => CaliptraQueueSize, name: "fifo_max_depth")
            .WithReservedBits(12, 20);

        Registers.Ctrl.Define(this)
            .WithFlag(0, out go, FieldMode.Read | FieldMode.Set,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        if(busy.Value)
                        {
                            this.ErrorLog("Transfer is ongoing. Aborting new transfer");
                            return;
                        }
                        busy.Value = true;
                        PrepareTransfer();
                        go.Value = false;
                    }
                }, name: "go")
            .WithFlag(1,
                valueProviderCallback: _ => false,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        // We don't stop pending operations
                        // In HW this is also unreliable, since in-flight AXI transactions won't be completed
                        internalFifo.Clear();
                        transferRequest = null;
                        go.Value = false;
                        this.DebugLog("Flushed the DMA state");
                    }
                }, name: "flush")
            .WithTaggedFlag("aes_mode_en", 2)
            .WithTaggedFlag("aes_gcm_mode", 3)
            .WithReservedBits(4, 12)
            .WithEnumField(16, 2, out readRoute, name: "rd_route")
            .WithReservedBits(18, 2)
            .WithFlag(20, out rdFixed, name: "rd_fixed")
            .WithReservedBits(21, 3)
            .WithEnumField(24, 3, out writeRoute, name: "wr_route")
            .WithReservedBits(27, 1)
            .WithFlag(28, out wrFixed, name: "wr_fixed")
            .WithReservedBits(29, 3);

        Registers.Status0.Define(this)
            .WithFlag(0, out busy, FieldMode.Read, name: "busy")
            .WithFlag(1, out error, FieldMode.Read, name: "error")
            .WithReservedBits(2, 2)
            // Since our queue is unbound, cap the size at the maximum. In RTL, the FIFO would drive AXI READY signal low, until queue again has space
            // Here, we load all data immediately, without waiting for space in queue, so "fake" not having space
            .WithValueField(4, 12, FieldMode.Read, valueProviderCallback: _ => (ulong)(internalFifo.Count > CaliptraQueueSize ? CaliptraQueueSize : internalFifo.Count), name: "fifo_depth")
            .WithTag("axi_dma_fsm_ps", 16, 2)
            .WithFlag(18, out payloadAvailable, FieldMode.Read, name: "payload_available")
            .WithFlag(19, out imageActivated, FieldMode.Read, name: "image_activated")
            .WithTag("axi_dma_aes_fsm_ps", 20, 4)
            .WithReservedBits(24, 8);

        Registers.Status1.Define(this)
            .WithValueField(0, 32, FieldMode.Read, name: "bytes_remaining");

        Registers.SrcAddrL.Define(this)
            .WithValueField(0, 32, out srcAddrL, name: "addr_l");

        Registers.SrcAddrH.Define(this)
            .WithValueField(0, 32, out srcAddrH, name: "addr_h");

        Registers.DstAddrL.Define(this)
            .WithValueField(0, 32, out dstAddrL, name: "addr_l");

        Registers.DstAddrH.Define(this)
            .WithValueField(0, 32, out dstAddrH, name: "addr_h");

        Registers.ByteCount.Define(this)
            .WithValueField(0, 32, out byteCount, name: "count");

        Registers.BlockSize.Define(this)
            // Only is used in streaming boot, but we just send regular transfers
            // in Emulation, this is not needed to save time that the transfers would take
            .WithTag("size", 0, 12)
            .WithReservedBits(12, 20);

        Registers.WriteData.Define(this)
            .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) =>
            {
                internalFifo.Enqueue((uint)val);
                // DMA can wait for data to become available for transfer
                RequestTransfer(DefaultDmaChannel);
            }, name: "wdata");

        Registers.ReadData.Define(this)
            .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
            {
                if(!internalFifo.TryDequeue(out var result))
                {
                    this.WarningLog("Internal queue is empty, returning 0");
                    return 0;
                }
                return result;
            }, name: "rdata");
    }

    // This DMA buffers data in an internal FIFO in RTL
    // So the operation is
    // | DMA Write Route | -----> | Internal FIFO | -----> | DMA Read Route |
    // For this DMA, "Write Route" means "Write Route to the internal FIFO", NOT "Write into the destination place"
    // and "Read Route" means "Read from the internal FIFO", NOT "Read from the source place"
    // Also, Disabled doesn't mean that it won't do anything, it still pushes data to AXI bus
    // E.g. see: https://github.com/chipsalliance/caliptra-rtl/blob/v2.1.2/src/axi/rtl/axi_dma_ctrl.sv#L968-L986
    // and https://chipsalliance.github.io/caliptra-web/docs/2.1/subsystem/ss_hardware_spec.html?highlight=axi%20dma#routes
    private void PrepareTransfer()
    {
        var count = (int)byteCount.Value;
        if(count == 0)
        {
            this.Log(LogLevel.Warning, "DMA transfer triggered, but byte count is 0. Aborting");
            return;
        }

        Place srcPlace;
        Place dstPlace;
        ulong srcAddr = ((ulong)srcAddrH.Value << 32) | srcAddrL.Value;
        ulong dstAddr = ((ulong)dstAddrH.Value << 32) | dstAddrL.Value;
        // Offset by Mailbox SRAM in the MBOX mode
        if(writeRoute.Value == TransferRoute.Mailbox)
        {
            srcAddr += mailboxBase;
        }
        if(readRoute.Value == TransferRoute.Mailbox)
        {
            dstAddr += mailboxBase;
        }

        if(writeRoute.Value == TransferRoute.InternalFIFO)
        {
            byte[] array = PrepareTransferArray(count);
            srcPlace = new Place(array, 0);
        }
        // The RTL is written in such a way, that any other transfer type than KV, AHB or MBOX is interpreted
        // as an AXI transfer (even if transfer route is in DISABLED state)
        else
        {
            srcPlace = new Place(srcAddr);
        }

        if(readRoute.Value == TransferRoute.InternalFIFO)
        {
            byte[] array = new byte[count];
            dstPlace = new Place(array, 0);
        }
        else
        {
            dstPlace = new Place(dstAddr);
        }

        this.Log(LogLevel.Debug, "Configured DMA transfer of {0} bytes, src addr: 0x{1:X}, Read Route type: {2}, dest addr: 0x{3:X}, Write Route type: {4}", count, srcAddr, readRoute.Value, dstAddr, writeRoute.Value);

        transferRequest = new Request(
            srcPlace,
            dstPlace,
            count,
            TransferType.DoubleWord,
            TransferType.DoubleWord,
            // At this point I think that Caliptra docs got the description of these two wrong
            // in the whole complexity of read/write routes...
            sourceIncrementStep: rdFixed.Value ? 0UL : 4UL,
            destinationIncrementStep: wrFixed.Value ? 0UL : 4UL
        );

        RequestTransfer(DefaultDmaChannel);
    }

    private byte[] PrepareTransferArray(int count)
    {
        byte[] array = new byte[count];
        int idx = 0;
        while(internalFifo.TryDequeue(out var elem))
        {
            for(int i = 4; i > 0; --i)
            {
                array[idx++] = (byte)(elem & 0xFFFF);
                elem >>= 8;
                // Everything was written
                if(idx >= count)
                {
                    break;
                }
            }
        }
        return array;
    }

    private IFlagRegisterField go;
    // Busy is also asserted when waiting for enough data to be supplied to the transfer FIFO
    private IFlagRegisterField busy;
    private IFlagRegisterField error;

    private IFlagRegisterField imageActivated;
    private IFlagRegisterField payloadAvailable;

    private IEnumRegisterField<TransferRoute> readRoute;
    private IEnumRegisterField<TransferRoute> writeRoute;
    private IFlagRegisterField rdFixed;
    private IFlagRegisterField wrFixed;

    private IValueRegisterField srcAddrL;
    private IValueRegisterField srcAddrH;
    private IValueRegisterField dstAddrL;
    private IValueRegisterField dstAddrH;
    private IValueRegisterField byteCount;

    private DmaEngine engine;
    private IPeripheral requestContext;
    private ulong mailboxBase;

    private Request? transferRequest;

    private readonly Queue<uint> internalFifo = new();

    // 512 bytes is the default size, but divide by data size (4 bytes)
    private const int CaliptraQueueSize = 128;

    private enum TransferRoute
    {
        Disabled,
        Mailbox,
        InternalFIFO,
        AXIFabric
    }

    private enum Registers
    {
        Id = 0x0,
        Cap = 0x4,
        Ctrl = 0x8,
        Status0 = 0xC,
        Status1 = 0x10,
        SrcAddrL = 0x14,
        SrcAddrH = 0x18,
        DstAddrL = 0x1C,
        DstAddrH = 0x20,
        ByteCount = 0x24,
        BlockSize = 0x28,
        WriteData = 0x2C,
        ReadData = 0x30,

        // Interrupt block is a stub (non-functional)
        global_intr_en_r = 0x800,
        error_intr_en_r = 0x804,
        notif_intr_en_r = 0x808,
        error_global_intr_r = 0x80C,
        notif_global_intr_r = 0x810,
        error_internal_intr_r = 0x814,
        notif_internal_intr_r = 0x818,
        error_intr_trig_r = 0x81C,
        notif_intr_trig_r = 0x820,
        error_cmd_dec_intr_count_r = 0x900,
        error_axi_rd_intr_count_r = 0x904,
        error_axi_wr_intr_count_r = 0x908,
        error_mbox_lock_intr_count_r = 0x90C,
        error_sha_lock_intr_count_r = 0x910,
        error_fifo_oflow_intr_count_r = 0x914,
        error_fifo_uflow_intr_count_r = 0x918,
        error_aes_cif_intr_count_r = 0x91C,
        error_kv_rd_intr_count_r = 0x920,
        error_kv_rd_large_intr_count_r = 0x924,
        notif_txn_done_intr_count_r = 0x980,
        notif_fifo_empty_intr_count_r = 0x984,
        notif_fifo_not_empty_intr_count_r = 0x988,
        notif_fifo_full_intr_count_r = 0x98C,
        notif_fifo_not_full_intr_count_r = 0x990,
        error_cmd_dec_intr_count_incr_r = 0xa00,
        error_axi_rd_intr_count_incr_r = 0xa04,
        error_axi_wr_intr_count_incr_r = 0xa08,
        error_mbox_lock_intr_count_incr_r = 0xa0C,
        error_sha_lock_intr_count_incr_r = 0xa10,
        error_fifo_oflow_intr_count_incr_r = 0xa14,
        error_fifo_uflow_intr_count_incr_r = 0xa18,
        error_aes_cif_intr_count_incr_r = 0xa1C,
        error_kv_rd_intr_count_incr_r = 0xa20,
        error_kv_rd_large_intr_count_incr_r = 0xa24,
        notif_txn_done_intr_count_incr_r = 0xa28,
        notif_fifo_empty_intr_count_incr_r = 0xa2C,
        notif_fifo_not_empty_intr_count_incr_r = 0xa30,
        notif_fifo_full_intr_count_incr_r = 0xa34,
        notif_fifo_not_full_intr_count_incr_r = 0xa38,
    }
}
