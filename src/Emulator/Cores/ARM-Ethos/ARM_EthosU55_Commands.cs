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

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU55
{
    private bool IssueDmaTransfer()
    {
        // Bit[8] selects the internal shared buffer over the AXI ports
        var sourceInternal = ((commandRegisters.Dma0SrcRegion >> 8) & 0b1) != 0;
        var destinationInternal = ((commandRegisters.Dma0DstRegion >> 8) & 0b1) != 0;
        if(sourceInternal)
        {
            // U55 only permits external DMA sources (TRM 4.4, 0x130).
            ParseError("Internal DMA source is not supported on U55");
            return false;
        }
        if(destinationInternal)
        {
            return IssueInternalDmaTransfer();
        }

        // Ignore stride mode, U55 only implements 1D
        if(!TryGetBufferAddress(commandRegisters.Dma0SrcRegion & 0xFF, commandRegisters.Dma0Src, out var sourceAddress, "DMA0 source")
            || !TryGetBufferAddress(commandRegisters.Dma0DstRegion & 0xFF, commandRegisters.Dma0Dst, out var destinationAddress, "DMA0 destination"))
        {
            return false;
        }

        var source = new Place(sourceAddress);
        var destination = new Place(destinationAddress);

        var request = new Request(
            source, destination, (int)commandRegisters.Dma0Len, TransferType.DoubleWord, TransferType.DoubleWord
        );

        this.Log(LogLevel.Debug, "Issuing DMA transfer: {0}", request);
        dmaEngine.IssueCopy(request);
        return true;
    }

    private bool IssueInternalDmaTransfer()
    {
        // Validate the full length before narrowing it, as truncating a transfer
        // can leave a partially loaded LUT while reporting successful completion.
        var length = commandRegisters.Dma0Len;
        if(length > (ulong)sharedBuffer.Length || commandRegisters.Dma0Dst > (ulong)sharedBuffer.Length - length)
        {
            ParseError($"Internal DMA destination out of shared buffer bounds: 0x{commandRegisters.Dma0Dst:X}, length 0x{length:X}");
            return false;
        }
        // U55 requires both addresses and the length to be 16-byte aligned
        // (Vela register_command_stream_util.check_dma_op).
        if(((commandRegisters.Dma0Src | commandRegisters.Dma0Dst | length) & 0xF) != 0)
        {
            ParseError($"Internal DMA transfer is not 16-byte aligned: src 0x{commandRegisters.Dma0Src:X}, dst 0x{commandRegisters.Dma0Dst:X}, length 0x{length:X}");
            return false;
        }

        if(!TryGetBufferAddress(commandRegisters.Dma0SrcRegion & 0xFF, commandRegisters.Dma0Src, out var sourceAddress, "Internal DMA source", length))
        {
            return false;
        }
        sysbus.ReadBytes(sourceAddress, (int)length, sharedBuffer, (int)commandRegisters.Dma0Dst);

        this.DebugLog("Issued internal DMA transfer to 0x{0:X}, length 0x{1:X}", commandRegisters.Dma0Dst, length);
        return true;
    }

    private void ExecuteCommandStream()
    {
        if(isRunning.Value)
        {
            this.Log(LogLevel.Warning, "Ignoring command submission while running");
            return;
        }
        if(commandParseError.Value || busStatus.Value)
        {
            this.Log(LogLevel.Warning, "Ignoring command submission while the NPU is in an error state");
            return;
        }

        queueReadOffset.Value = 0;
        isRunning.Value = true;

        if((QueueBaseAddress & 0xF) != 0)
        {
            ParseError("QBASE address is not aligned");
            return;
        }

        if(queueSize.Value == 0 || queueSize.Value > MaximumCommandBytes || (queueSize.Value & 0b11) != 0)
        {
            ParseError("Invalid QSIZE");
            return;
        }

        var lastCommandLength = 0u;
        var bytes = sysbus.ReadBytes(QueueBaseAddress, (int)queueSize.Value);
        while(queueReadOffset.Value < (ulong)bytes.Length)
        {
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
            var length = isCmd0 ? 4u : 8u;
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
                this.Log(LogLevel.Noisy, "U55 CMD0 at +0x{0:X}: opcode=0x{1:X}, parameter=0x{2:X}", queueReadOffset.Value, opcode, parameter);

                // Opcode space is split into operations, starting from 0x0 and set commands, from 0x100
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
                var command = (CommandOpcode)opcode;
                switch(command)
                {
                case CommandOpcode.OpStop:
                    isRunning.Value = false;
                    commandEndReached.Value = true;

                    irqRaised.Value = true;
                    irqHistoryMask.Value |= parameter;
                    UpdateInterrupts();
                    return;
                case CommandOpcode.OpIrq:
                    irqRaised.Value = true;
                    irqHistoryMask.Value |= parameter;
                    UpdateInterrupts();
                    continue;
                case CommandOpcode.OpDmaStart:
                    if(!IssueDmaTransfer())
                    {
                        return;
                    }
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
                case CommandOpcode.OpPool:
                    if(!ExecutePool(parameter))
                    {
                        ParseError("POOL operation failed");
                        return;
                    }
                    continue;
                case CommandOpcode.OpElementwise:
                    if(!ExecuteElementwise(parameter))
                    {
                        ParseError("Elementwise operation failed");
                        return;
                    }
                    continue;
                case CommandOpcode.OpConv:
                case CommandOpcode.OpDepthwise:
                    this.Log(LogLevel.Warning, $"NPU operation {command} is not implemented");
                    isRunning.Value = false;
                    commandParseError.Value = true;
                    irqRaised.Value = true;
                    UpdateInterrupts();
                    return;
                default:
                    ParseError($"Unknown CMD0 opcode +0x{opcode:X}");
                    return;
                }
            }
            else
            {
                // cmd1 only contains set register commands
                var payload = BitConverter.ToUInt32(bytes.AsSpan((int)queueReadOffset.Value + 4, 4));
                this.Log(LogLevel.Noisy, "U55 CMD1 at 0x{0:X}: opcode=0x{1:X}, parameter=0x{2:X}, payload=0x{3:X8}",
                    queueReadOffset.Value, opcode, parameter, payload);
                if(!SetPayloadRegister((PayloadCommandOpcode)opcode, payload, parameter))
                {
                    ParseError($"Unknown CMD1 opcode +0x{opcode:X}");
                    return;
                }
            }
        }

        ParseError("Command stream has no NPU_OP_STOP");
    }

    private void ParseError(string reason)
    {
        isRunning.Value = false;
        commandParseError.Value = true;

        this.Log(LogLevel.Error, "U55 command parse error at +0x{0:X}: {1}", queueReadOffset.Value, reason);
    }

    private const uint MaximumCommandBytes = 0x10000;

    private const uint Cmd0Encoding = 0b000000;
    private const uint Cmd1Encoding = 0b010000;
}
