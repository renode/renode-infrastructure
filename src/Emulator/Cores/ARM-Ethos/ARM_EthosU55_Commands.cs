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
    private void IssueDmaTransfer()
    {
        // Bit[8] - internal
        if((((commandRegisters.Dma0SrcRegion | commandRegisters.Dma0DstRegion) >> 8) & 0b1) != 0)
        {
            ParseError($"Internal DMA memory is not supported");
        }
        var sourceRegion = commandRegisters.Dma0SrcRegion & 0xFF;
        var sourceOffset = commandRegisters.Dma0Src;
        var destinationRegion = commandRegisters.Dma0DstRegion & 0xFF;
        var destinationOffset = commandRegisters.Dma0Dst;
        var length = commandRegisters.Dma0Len;

        if(sourceRegion >= BasePointerCount / 2 || destinationRegion >= BasePointerCount / 2)
        {
            ParseError($"Invalid DMA0 region: src region {sourceRegion}, dst region {destinationRegion}");
            return;
        }

        // Ignore stride mode, U55 only implements 1D
        var sourceBase = (basePointers[(2 * sourceRegion) + 1].Value << 32) | basePointers[2 * sourceRegion].Value;
        var destinationBase = (basePointers[(2 * destinationRegion) + 1].Value << 32) | basePointers[2 * destinationRegion].Value;

        if(sourceBase > ulong.MaxValue - sourceOffset || destinationBase > ulong.MaxValue - destinationOffset)
        {
            ParseError($"DMA address overflow: source base 0x{sourceBase:X} + offset 0x{sourceOffset:X}, destination base 0x{destinationBase:X} + offset 0x{destinationOffset:X}");
            return;
        }

        var sourceAddress = sourceBase + sourceOffset;
        var destinationAddress = destinationBase + destinationOffset;

        var source = new Place(sourceAddress);
        var destination = new Place(destinationAddress);

        var request = new Request(
            source, destination, (int)length, TransferType.DoubleWord, TransferType.DoubleWord
        );

        this.Log(LogLevel.Debug, "Issuing DMA transfer: {0}", request);
        dmaEngine.IssueCopy(request);
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
                    IssueDmaTransfer();
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
                case CommandOpcode.OpConv:
                case CommandOpcode.OpDepthwise:
                case CommandOpcode.OpPool:
                case CommandOpcode.OpElementwise:
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
