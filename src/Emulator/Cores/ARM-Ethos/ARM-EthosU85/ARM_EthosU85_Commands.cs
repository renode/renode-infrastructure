//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
    private void ExecuteCommandStream()
    {
        if(isRunning.Value)
        {
            this.WarningLog("Ignoring command submission while running");
            return;
        }
        // These status bits can only be cleared by reset (U85 TRM 3.3.1.2).
        if(commandParseError.Value || busStatus.Value || branchFault.Value)
        {
            this.WarningLog("Ignoring command submission while the NPU is in an error state");
            return;
        }

        // After an NPU_OP_STOP, continue at QBASE+QREAD unless QBASE or QSIZE
        // have been written since (U85 TRM 3.3.1.3). Otherwise start at QBASE.
        if(!resumeAfterStop)
        {
            queueReadOffset.Value = 0;
        }
        resumeAfterStop = false;
        isRunning.Value = true;

        // QBASE must be four-byte aligned (U85 TRM 3.3.1.5).
        if((QueueBaseAddress & 0x3) != 0)
        {
            ParseError("QBASE is not four-byte aligned");
            return;
        }

        // QSIZE is a multiple of four in the range 0-16 MB (U85 TRM 3.3.1.8).
        // Zero is valid: an empty stream ends straight away.
        if(queueSize.Value > MaximumCommandBytes || (queueSize.Value & 0b11) != 0)
        {
            this.WarningLog("QSIZE 0x{0:X} is outside the TRM range", queueSize.Value);
            ParseError("Invalid QSIZE");
            return;
        }

        var bytes = sysbus.ReadBytes(QueueBaseAddress, (int)queueSize.Value);

        // QREAD points at the command being executed. Earlier commands are complete (U85 TRM 3.3.1.6).
        var lastCommandLength = 0U;
        var executedCommands = 0UL;
        while(queueReadOffset.Value < (ulong)bytes.Length)
        {
            if(++executedCommands > MaximumExecutedCommands)
            {
                ParseError("Command limit exceeded, possibly an endless branch loop");
                return;
            }

            queueReadOffset.Value += lastCommandLength;
            if(queueReadOffset.Value == (ulong)bytes.Length)
            {
                break;
            }

            var commandBits = BitConverter.ToUInt32(bytes.AsSpan((int)queueReadOffset.Value, 4));
            var commandEncoding = BitHelper.GetValue(commandBits, 10, 6);
            if(commandEncoding != Cmd0Encoding && commandEncoding != Cmd1Encoding)
            {
                ParseError($"Invalid command encoding: 0x{commandEncoding:X}");
                return;
            }
            var isCmd0 = commandEncoding == Cmd0Encoding;
            var length = isCmd0 ? 4U : 8U;
            if(queueReadOffset.Value + length > (ulong)bytes.Length)
            {
                ParseError("Truncated CMD1 payload");
                return;
            }
            lastCommandLength = length;

            var opcode = BitHelper.GetValue(commandBits, 0, 10);
            var parameter = (ushort)BitHelper.GetValue(commandBits, 16, 16);

            if(isCmd0)
            {
                this.NoisyLog("U85 CMD0 at +0x{0:X}: opcode=0x{1:X}, parameter=0x{2:X}", queueReadOffset.Value, opcode, parameter);

                if(opcode >= (uint)CommandOpcode.SetIfmPadTop)
                {
                    if(!SetRegister((CommandOpcode)opcode, parameter))
                    {
                        ParseError($"Unknown CMD0 SET opcode +0x{opcode:X}");
                        return;
                    }
                    continue;
                }

                // Handle actual operation commands
                switch((CommandOpcode)opcode)
                {
                case CommandOpcode.OpStop:
                    queueReadOffset.Value += length;
                    // cmd_end_reached means QREAD == QSIZE, so only when the STOP
                    // was the last command in the stream.
                    commandEndReached.Value = queueReadOffset.Value == (ulong)bytes.Length;
                    resumeAfterStop = true;
                    StopAndRaiseIrq(parameter);
                    return;
                case CommandOpcode.OpIrq:
                    // Raise an IRQ and keep running (U85 TRM 3.10.7).
                    irqHistoryMask.Value |= parameter;
                    irqRaised.Value = true;
                    UpdateInterrupts();
                    continue;
                case CommandOpcode.OpDmaWait:
                    // DMA transfers are instant, don't wait
                    continue;
                case CommandOpcode.OpKernelWait:
                    // Command execution is instant, don't wait
                    continue;
                case CommandOpcode.OpPmuMask:
                    // PMU is not implemented
                    continue;
                case CommandOpcode.OpElementwise:
                    if(!ExecuteElementwise(parameter))
                    {
                        ParseError("Elementwise operation failed");
                        return;
                    }
                    continue;
                case CommandOpcode.OpDmaStart:
                    if(!ExecuteDmaStart())
                    {
                        ParseError("DMA_START operation failed");
                        return;
                    }
                    continue;
                case CommandOpcode.OpConv:
                case CommandOpcode.OpDepthwise:
                case CommandOpcode.OpPool:
                case CommandOpcode.OpResize:
                    // TODO
                    ParseError($"opcode +0x{opcode:X} is not implemented");
                    return;
                default:
                    ParseError($"Unknown CMD0 opcode +0x{opcode:X}");
                    return;
                }
            }
            else // cmd1
            {
                // With the exception of `NPU_OP_BRANCH`, cmd1 contains only set register commands
                var payload = BitConverter.ToUInt32(bytes.AsSpan((int)queueReadOffset.Value + 4, 4));
                this.NoisyLog("U85 CMD1 at +0x{0:X}: opcode=0x{1:X}, parameter=0x{2:X}, payload=0x{3:X8}",
                    queueReadOffset.Value, opcode, parameter, payload);
                if((PayloadCommandOpcode)opcode == PayloadCommandOpcode.OpBranch)
                {
                    if(!ExecuteBranch(parameter, payload, out var taken))
                    {
                        return;
                    }
                    if(taken)
                    {
                        // QREAD is already the branch target, so don't advance past it.
                        lastCommandLength = 0;
                    }
                    continue;
                }
                if(!SetPayloadRegister((PayloadCommandOpcode)opcode, payload, parameter))
                {
                    ParseError($"Unknown CMD1 opcode +0x{opcode:X}");
                    return;
                }
            }
        }

        // Ran out of commands (QREAD == QSIZE): a normal stop (U85 TRM 3.5.2, 3.3.1.2).
        commandEndReached.Value = true;
        StopAndRaiseIrq();
    }

    // U85 TRM 3.11.2 NPU_OP_BRANCH
    private bool ExecuteBranch(ushort parameter, uint branchTarget, out bool taken)
    {
        taken = false;
        // branch_cond (header bit 16): 0 = always, 1 = when COND_STATUS.result_flag is set.
        var conditional = (parameter & 0x1) != 0;

        if((branchTarget & 0x3) != 0)
        {
            ParseError($"Branch target 0x{branchTarget:X} is not four-byte aligned");
            return false;
        }
        if(branchTarget >= queueSize.Value)
        {
            branchFault.Value = true;
            this.ErrorLog("U85 branch fault at +0x{0:X}: target 0x{1:X} is not below QSIZE 0x{2:X}",
                queueReadOffset.Value, branchTarget, queueSize.Value);
            StopAndRaiseIrq();
            return false;
        }

        if(!conditional || conditionResultFlag.Value)
        {
            queueReadOffset.Value = branchTarget;
            taken = true;
        }
        return true;
    }

    // U85 TRM 3.10.4 NPU_OP_DMA_START
    private bool ExecuteDmaStart()
    {
        // DMA transfers are modeled as instant, so the DMA can always accept new operations

        var srcRegion = (int)BitHelper.GetValue(commandRegisters.Dma0SrcRegion, 0, 3);
        var srcInternalRegionMode = BitHelper.IsBitSet(commandRegisters.Dma0SrcRegion, 8);
        var srcStrideMode = (DmaStride)BitHelper.GetValue(commandRegisters.Dma0SrcRegion, 9, 2);
        var srcIndexModeEnabled = BitHelper.IsBitSet(commandRegisters.Dma0SrcRegion, 11);

        var dstRegion = (int)BitHelper.GetValue(commandRegisters.Dma0DstRegion, 0, 3);
        var dstInternalRegionMode = BitHelper.IsBitSet(commandRegisters.Dma0DstRegion, 8);
        var dstIndexModeEnabled = BitHelper.IsBitSet(commandRegisters.Dma0DstRegion, 11);

        var length = commandRegisters.Dma0Len;
        var width = commandRegisters.Dma0Size0;
        var height = commandRegisters.Dma0Size1;

        var srcP = commandRegisters.Dma0Src;
        var dstP = commandRegisters.Dma0Dst;

        var idxP = commandRegisters.Dma0Idx;
        var idxRegion = commandRegisters.Dma0IdxRegion;
        var idxMax = commandRegisters.Dma0IdxMax;

        var srcStride0 = commandRegisters.Dma0SrcStride0;
        var srcStride1 = commandRegisters.Dma0SrcStride1;
        var dstStride0 = commandRegisters.Dma0DstStride0;
        var dstStride1 = commandRegisters.Dma0DstStride1;
        var idxSkip1 = commandRegisters.Dma0IdxSkip1;

        if(srcInternalRegionMode)
        {
            this.ErrorLog("Only external source region mode is supported");
            return false;
        }

        if(srcIndexModeEnabled && dstIndexModeEnabled)
        {
            this.ErrorLog("Source and destination index modes cannot be enabled simultaneously");
            return false;
        }

        if(dstInternalRegionMode)
        {
            dstRegion = -1; // Copy into LUT
        }

        switch(srcStrideMode)
        {
        case DmaStride.Dimensions1:
            width = 1;
            goto case DmaStride.Dimensions2; // fallthrough
        case DmaStride.Dimensions2:
            height = 1;
            break;
        case DmaStride.Dimensions3:
            break; // Nothing to do
        case DmaStride.Reserved:
            this.ErrorLog("Invalid {0} value: {1}", nameof(srcStrideMode), srcStrideMode);
            return false;
        }

        using(sysbus.EnterDelayedInvalidationContext())
        {
            for(ulong y = 0; y < height; y++)
            {
                for(ulong x = 0; x < width; x++)
                {
                    var copyValid = true;
                    var srcIdx = x;
                    var dstIdx = x;

                    if(srcIndexModeEnabled || dstIndexModeEnabled)
                    {
                        if(!TryGetBufferAddress(idxRegion, idxP, out var idxAddress, "DMA index read"))
                        {
                            return false;
                        }
                        var idx = (int)sysbus.ReadDoubleWord(idxAddress);
                        idxP += 4;
                        if(idx < 0 || idx > idxMax)
                        {
                            copyValid = false;
                        }
                        if(srcIndexModeEnabled)
                        {
                            srcIdx = unchecked((ulong)idx);
                        }
                        else
                        {
                            dstIdx = unchecked((ulong)idx);
                        }
                    }

                    if(copyValid)
                    {
                        var srcPlace = GetDmaPlace(srcRegion, srcP + srcIdx * (ulong)srcStride0, "DMA source address", allowLut: false);
                        var dstPlace = GetDmaPlace(dstRegion, dstP + dstIdx * (ulong)dstStride0, "DMA destination address", allowLut: true);
                        if(srcPlace is null || dstPlace is null)
                        {
                            return false;
                        }

                        var request = new Request(
                            source: srcPlace,
                            destination: dstPlace,
                            size: (int)length,
                            readTransferType: TransferType.Byte,
                            writeTransferType: TransferType.Byte,
                            incrementReadAddress: true,
                            incrementWriteAddress: true
                        );
                        dmaEngine.IssueCopy(request);
                    }
                }
                srcP += (ulong)srcStride1;
                dstP += (ulong)dstStride1;
                if(srcIndexModeEnabled || dstIndexModeEnabled)
                {
                    idxP += (ulong)idxSkip1;
                }
            }
        }
        return true;
    }

    private void StopAndRaiseIrq(ushort irqMask = 0)
    {
        isRunning.Value = false;
        irqHistoryMask.Value |= irqMask;
        irqRaised.Value = true;
        UpdateInterrupts();
    }

    private void ParseError(string reason)
    {
        // The NPU stops, sets cmd_parse_error and raises an IRQ (U85 TRM 3.3.1.2).
        commandParseError.Value = true;
        this.ErrorLog("U85 command parse error at +0x{0:X}: {1}", queueReadOffset.Value, reason);
        StopAndRaiseIrq();
    }

    private Place? GetDmaPlace(int region, ulong offset, string description, bool allowLut)
    {
        if(region == -1)
        {
            if(allowLut)
            {
                if(offset >= (ulong)lookupTable.Length)
                {
                    this.ErrorLog("LUT access at {0} is out of bounds of {1} for {2}", offset, lookupTable.Length, description);
                    return null;
                }
                return new Place(lookupTable, (int)offset);
            }
            this.ErrorLog("LUT access is not allowed for {0}", description);
            return null;
        }

        if(TryGetBufferAddress(region, offset, out var address, description))
        {
            return new Place(address);
        }
        return null;
    }

    private const uint MaximumCommandBytes = 0x1000000; // 16 MB

    // Model safeguard, not from the TRM: a branch loop runs inside the CMD write,
    // so an endless loop would otherwise hang the emulation.
    private const ulong MaximumExecutedCommands = 10_000_000;

    private const uint Cmd0Encoding = 0b000000;
    private const uint Cmd1Encoding = 0b010000;

    private enum DmaStride
    {
        Dimensions1 = 0b00,
        Dimensions2 = 0b01,
        Dimensions3 = 0b10,
        Reserved = 0b11,
    }
}
