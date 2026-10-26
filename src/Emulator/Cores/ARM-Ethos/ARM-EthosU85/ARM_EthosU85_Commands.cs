//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

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
                case CommandOpcode.OpConv:
                case CommandOpcode.OpDepthwise:
                case CommandOpcode.OpPool:
                case CommandOpcode.OpElementwise:
                case CommandOpcode.OpResize:
                case CommandOpcode.OpDmaStart:
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
                    // TODO
                    ParseError("NPU_OP_BRANCH is not implemented");
                    return;
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

    private const uint MaximumCommandBytes = 0x1000000; // 16 MB

    private const uint Cmd0Encoding = 0b000000;
    private const uint Cmd1Encoding = 0b010000;
}