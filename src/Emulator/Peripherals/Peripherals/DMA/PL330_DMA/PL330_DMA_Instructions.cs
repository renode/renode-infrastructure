//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public partial class PL330_DMA
    {
        private void RegisterInstructions()
        {
            decoderRoot.AddOpcode(0b01010100, 8, () => new DMAADH(this, isDestinationAddressRegister: false));
            decoderRoot.AddOpcode(0b01010110, 8, () => new DMAADH(this, isDestinationAddressRegister: true));

            // DMAADNH should not be present in product revision r0p0
            if(Revision > 0x0)
            {
                decoderRoot.AddOpcode(0b01011100, 8, () => new DMAADNH(this, isDestinationAddressRegister: false));
                decoderRoot.AddOpcode(0b01011110, 8, () => new DMAADNH(this, isDestinationAddressRegister: true));
            }

            decoderRoot.AddOpcode(0b00000000, 8, () => new DMAEND(this));

            decoderRoot.AddOpcode(0b10100000, 8, () => new DMAGO(this, nonSecure: false));
            decoderRoot.AddOpcode(0b10100010, 8, () => new DMAGO(this, nonSecure: true));

            decoderRoot.AddOpcode(0b00000001, 8, () => new DMAKILL(this));
            decoderRoot.AddOpcode(0b00110100, 8, () => new DMASEV(this));
            decoderRoot.AddOpcode(0b00011000, 8, () => new DMANOP(this));
            decoderRoot.AddOpcode(0b00110110, 8, () => new DMAWFE(this));
            decoderRoot.AddOpcode(0b00010011, 8, () => new DMAWMB(this));
            decoderRoot.AddOpcode(0b00010010, 8, () => new DMARMB(this));

            decoderRoot.AddOpcode(0b00000100, 8, () => new DMALD(this, isConditional: false));
            decoderRoot.AddOpcode(0b00000101, 8, () => new DMALD(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00000111, 8, () => new DMALD(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst));

            decoderRoot.AddOpcode(0b00001000, 8, () => new DMAST(this, isConditional: false));
            decoderRoot.AddOpcode(0b00001001, 8, () => new DMAST(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00001011, 8, () => new DMAST(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst));
            decoderRoot.AddOpcode(0b00001100, 8, () => new DMASTZ(this));

            decoderRoot.AddOpcode(0b10111100, 8, () => new DMAMOV(this));

            decoderRoot.AddOpcode(0b00100000, 8, () => new DMALP(this, loopCounterIndex: 0));
            decoderRoot.AddOpcode(0b00100010, 8, () => new DMALP(this, loopCounterIndex: 1));

            decoderRoot.AddOpcode(0b00111000, 8, () => new DMALPEND(this, isConditional: false));
            decoderRoot.AddOpcode(0b00111100, 8, () => new DMALPEND(this, isConditional: false, loopCounterIndex: 1));
            decoderRoot.AddOpcode(0b00101100, 8, () => new DMALPEND(this, isConditional: false, loopCounterIndex: 1, isForever: true));
            decoderRoot.AddOpcode(0b00111001, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00111101, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single, loopCounterIndex: 1));
            decoderRoot.AddOpcode(0b00101101, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single, loopCounterIndex: 1, isForever: true));
            decoderRoot.AddOpcode(0b00111011, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst));
            decoderRoot.AddOpcode(0b00111111, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst, loopCounterIndex: 1));
            decoderRoot.AddOpcode(0b00101111, 8, () => new DMALPEND(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst, loopCounterIndex: 1 , isForever: true));

            decoderRoot.AddOpcode(0b00110101, 8, () => new DMAFLUSHP(this));
            decoderRoot.AddOpcode(0b00110001, 8, () => new DMAWFP(this, isPeripheralDriven: true));
            decoderRoot.AddOpcode(0b00110000, 8, () => new DMAWFP(this, isPeripheralDriven: false, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00110010, 8, () => new DMAWFP(this, isPeripheralDriven: false, transactionType: Channel.ChannelRequestType.Burst));

            decoderRoot.AddOpcode(0b00100101, 8, () => new DMALDP(this, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00100111, 8, () => new DMALDP(this, transactionType: Channel.ChannelRequestType.Burst));

            decoderRoot.AddOpcode(0b00101001, 8, () => new DMASTP(this, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00101011, 8, () => new DMASTP(this, transactionType: Channel.ChannelRequestType.Burst));
        }

        private SimpleInstructionDecoder<Instruction> decoderRoot = new SimpleInstructionDecoder<Instruction>();

        private abstract class Instruction
        {
            public void ParseAll(ulong value)
            {
                if(Length > sizeof(ulong))
                {
                    // Quite unlikely, since the instructions have at most 48 bits total
                    throw new ArgumentException($"Expected instruction length: {Length} is greater than the size of provided value");
                }

                while(!IsFinished)
                {
                    Parse((byte)value);
                    value >>= 8;
                }
            }

            public void Parse(byte value)
            {
                if(IsFinished)
                {
                    return;
                }

                currentByteCount++;
                instructionBytes.Add(value);

                if(IsFinished)
                {
                    ParseCompleteAction();
                }
            }

            public void Execute(DMAThreadType threadType, int? channelIndex = null, bool suppressAdvance = false)
            {
                if(threadType == DMAThreadType.Manager && channelIndex != null)
                {
                    throw new InvalidOperationException("Thread is a manager, but channel given");
                }
                if(threadType == DMAThreadType.Channel && channelIndex == null)
                {
                    throw new InvalidOperationException("Thread is a channel, but no channel given");
                }

                ulong offset = ExecuteIfCorrectThread(threadType, channelIndex);

                // Automatically advance PC by offset (usually instruction length) if offset > 0
                // Don't do it by length automatically, since there exist instructions that change PC explicitly
                if(!suppressAdvance && offset > 0)
                {
                    if(threadType == DMAThreadType.Channel)
                    {
                        Parent.channels[channelIndex.Value].PC += offset;
                    }
                    else
                    {
                        Parent.Log(LogLevel.Error, "DMA Manager thread behavior is unimplemented.");
                    }
                }
            }

            public override string ToString()
            {
                StringBuilder bits = new StringBuilder("");
                foreach(var b in instructionBytes.Reverse())
                {
                    bits.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
                }
                return $"{Name}" + (bits.Length > 0 ? $" [{bits}]" : "");
            }

            public string Name { get; }
            public bool IsFinished
            {
                get => currentByteCount == Length;
            }

            protected Instruction(PL330_DMA parent, uint length = 1, bool usableByChannel = true, bool usableByManager = false)
            {
                this.usableByManager = usableByManager;
                this.usableByChannel = usableByChannel;
                this.Length = length;
                this.Parent = parent;

                Name = GetType().Name;
            }

            protected virtual void ParseCompleteAction() {}

            protected virtual ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                Parent.Log(LogLevel.Error, "Instruction \"{0}\" is not implemented. Skipping it!", Name);
                return Length;
            }

            private ulong ExecuteIfCorrectThread(DMAThreadType threadType, int? channelIndex)
            {
                if(threadType == DMAThreadType.Manager && !usableByManager)
                {
                    Parent.Log(LogLevel.Error, "Thread is a manager, but instruction {0} not usable by manager", this.ToString());
                    // TODO: We should abort manager thread here, but its logic is unimplemented now
                    return Length;
                }
                if(threadType == DMAThreadType.Channel && !usableByChannel)
                {
                    Parent.Log(LogLevel.Error, "Thread is a channel, but instruction {0} not usable by channel", this.ToString());
                    // The docs are not clear about what happens here, but UndefinedInstruction seems logical
                    Parent.channels[channelIndex.Value].SignalChannelAbort(Channel.ChannelFaultReason.UndefinedInstruction);
                    return Length;
                }
                return ExecuteInner(threadType, channelIndex);
            }

            protected readonly uint Length;

            protected int currentByteCount;
            protected IList<byte> instructionBytes = new List<byte>();

            protected readonly PL330_DMA Parent;
            private readonly bool usableByManager;
            private readonly bool usableByChannel;
        }

        private abstract class DMAADH_base : Instruction
        {
            public DMAADH_base(PL330_DMA parent, bool isDestinationAddressRegister, bool negative) : base(parent, length: 3)
            {
                this.negative = negative;
                this.isDestinationAddressRegister = isDestinationAddressRegister;
            }

            protected override void ParseCompleteAction()
            {
                immediate = (ushort)((instructionBytes[1] << 8) | instructionBytes[0]);
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                unchecked
                {
                    if(isDestinationAddressRegister)
                    {
                        if(!negative)
                        {
                            Parent.channels[channelIndex.Value].DestinationAddress += immediate;
                        }
                        else
                        {
                            Parent.channels[channelIndex.Value].DestinationAddress -= immediate;
                        }
                    }
                    else
                    {
                        if(!negative)
                        {
                            Parent.channels[channelIndex.Value].SourceAddress += immediate;
                        }
                        else
                        {
                            Parent.channels[channelIndex.Value].SourceAddress -= immediate;
                        }
                    }
                }
                return Length;
            }

            private ushort immediate;

            private readonly bool negative;
            private readonly bool isDestinationAddressRegister;
        }

        private class DMAADH : DMAADH_base
        {
            public DMAADH(PL330_DMA parent, bool isDestinationAddressRegister) : base(parent, isDestinationAddressRegister, negative: false) {}
        }

        private class DMAADNH : DMAADH_base
        {
            public DMAADNH(PL330_DMA parent, bool isDestinationAddressRegister) : base(parent, isDestinationAddressRegister, negative: true) {}
        }

        private class DMAEND : Instruction
        {
            public DMAEND(PL330_DMA parent) : base(parent, usableByManager: true) {}

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(threadType == DMAThreadType.Manager)
                {
                    Parent.Log(LogLevel.Error, "DMAEND is currently not supported for manager");
                }
                else
                {
                    var selectedChannel = Parent.channels[channelIndex.Value];
                    selectedChannel.Status = Channel.ChannelStatus.Stopped;
                    selectedChannel.localMFIFO.Clear();
                    selectedChannel.Peripheral = null;
                }
                return Length;
            }
        }

        private class DMAGO : Instruction
        {
            public DMAGO(PL330_DMA parent, bool nonSecure) : base(parent, length: 6, usableByChannel: false, usableByManager: true)
            {
                this.nonSecure = nonSecure;
            }
            
            protected override void ParseCompleteAction()
            {
                channelNumber = instructionBytes[1] & 0b111;

                foreach(var b in instructionBytes.Reverse().Take(4))
                {
                    programCounter <<= 8;
                    programCounter |= b;
                }
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                var channel = Parent.channels[channelNumber];

                if(channel.Status != Channel.ChannelStatus.Stopped)
                {
                    Parent.Log(LogLevel.Debug, "This channel is in state: {0}, DMAGO treated as NOP.", channel.Status.ToString());
                    return Length;
                }
                if(nonSecure)
                {
                    Parent.Log(LogLevel.Warning, "Non-secure bit is ignored, value: {0}", nonSecure);
                }

                channel.PC = programCounter;
                channel.Status = Channel.ChannelStatus.Executing;

                return 0;
            }

            private int channelNumber;
            // The immediate value, used to set nth's channel's PC
            private uint programCounter;
            // Used to force channel into Non-Secure mode. We don't support secure/non-secure mode, so it's ignored
            private readonly bool nonSecure;
        }

        private class DMAKILL : Instruction
        {
            public DMAKILL(PL330_DMA parent) : base(parent, usableByManager: true) {}

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(threadType == DMAThreadType.Manager)
                {
                    Parent.Log(LogLevel.Error, "KILL on Manager thread is currently unsupported");
                }
                else
                {
                    var selectedChannel = Parent.channels[channelIndex.Value];
                    selectedChannel.localMFIFO.Clear();
                    selectedChannel.Status = Channel.ChannelStatus.Stopped;
                    selectedChannel.Peripheral = null;
                }
                return Length;
            }
        }

        private abstract class DMA_LD_ST_base : Instruction
        {
            public DMA_LD_ST_base(PL330_DMA parent, bool IsConditional, Channel.ChannelRequestType TransactionType, uint length = 1)
                : base(parent, length)
            {
                this.isConditional = IsConditional;
                this.transactionType = TransactionType;
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(!isConditional)
                {
                    DoTransfer(channelIndex.Value, false);
                    return Length;
                }

                var requestType = Parent.channels[channelIndex.Value].RequestType;
                if(requestType == transactionType)
                {
                    DoTransfer(channelIndex.Value, requestType == Channel.ChannelRequestType.Single);
                    return Length;
                }

                // Treat as NOP
                return Length;
            }

            protected void DoEndianSwap(Channel selectedChannel, int dataLengthToSwap)
            {
                byte[] bytesToSwap = selectedChannel.localMFIFO.DequeueRange(dataLengthToSwap);
                byte[] bytesRemaining = selectedChannel.localMFIFO.DequeueAll();

                if(bytesToSwap.Length % selectedChannel.EndianSwapSize != 0)
                {
                    // Not sure what happens here - the docs recommend to avoid this state
                    // but there is no mention if DMA should abort now
                    Parent.Log(LogLevel.Error, "Number of bytes requested for transfer: {0} is not a multiple of EndianSwapSize: {1}", bytesToSwap.Length, selectedChannel.EndianSwapSize);
                }

                for(int i = 0; i < bytesToSwap.Length; i += selectedChannel.EndianSwapSize)
                {
                    Array.Reverse(bytesToSwap, i, Math.Min(selectedChannel.EndianSwapSize, bytesToSwap.Length - i));
                }
                // Restore contents of the thread's buffer
                selectedChannel.localMFIFO.EnqueueRange(bytesToSwap);
                selectedChannel.localMFIFO.EnqueueRange(bytesRemaining);
            }

            protected abstract void DoTransfer(int channelIndex, bool ignoreBurst);

            private readonly bool isConditional;
            private readonly Channel.ChannelRequestType transactionType;
        }

        private class DMALD : DMA_LD_ST_base
        {
            public DMALD(PL330_DMA parent, bool isConditional, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single)  
                : base(parent, isConditional, transactionType) {}

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                var readLength = selectedChannel.SourceReadSize;

                for(var burst = 0; burst < (ignoreBurst ? 1 : selectedChannel.SourceBurstLength); ++burst)
                {
                    var byteArray = Parent.machine.GetSystemBus(Parent).ReadBytes(selectedChannel.SourceAddress, readLength, context: Parent.context);
                    selectedChannel.localMFIFO.EnqueueRange(byteArray);

                    if(selectedChannel.SourceIncrementingAddress)
                    {
                        selectedChannel.SourceAddress += (uint)readLength;
                    }
                }
            }
        }

        private class DMAST : DMA_LD_ST_base
        {
            public DMAST(PL330_DMA parent, bool isConditional, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single) 
                : base(parent, isConditional, transactionType) {}

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                var writeLength = selectedChannel.DestinationWriteSize;
                var burstLength = ignoreBurst ? 1 : selectedChannel.DestinationBurstLength;

                // If requested, swap endianness of data in buffer, just before the transmission
                if(selectedChannel.EndianSwapSize > 1)
                {
                    DoEndianSwap(selectedChannel, writeLength * burstLength);
                }

                for(var burst = 0; burst < burstLength; ++burst)
                {
                    byte[] byteArray = selectedChannel.localMFIFO.DequeueRange(writeLength);
                    if(byteArray.Length != writeLength)
                    {
                        Parent.Log(LogLevel.Error, "Underflow in channel queue, {0} bytes remaining in FIFO, but requested to write {1}. Aborting thread.", byteArray.Length, writeLength);
                        selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.NotEnoughStoredDataInMFIFO);
                        return;
                    }
                    Parent.machine.GetSystemBus(Parent).WriteBytes(byteArray, selectedChannel.DestinationAddress, context: Parent.context);

                    if(selectedChannel.DestinationIncrementingAddress)
                    {
                        selectedChannel.DestinationAddress += (uint)writeLength;
                    }
                }
            }
        }

        private class DMASTZ : DMA_LD_ST_base
        {
            public DMASTZ(PL330_DMA parent) : base(parent, IsConditional: false, TransactionType: Channel.ChannelRequestType.Single) {}

            protected override void DoTransfer(int channelIndex, bool _)
            {
                var selectedChannel = Parent.channels[channelIndex];
                var writeLength = selectedChannel.DestinationWriteSize;

                for(var burst = 0; burst < selectedChannel.DestinationBurstLength; ++burst)
                {
                    Parent.sysbus.WriteBytes(Enumerable.Repeat((byte)0, writeLength).ToArray(), selectedChannel.DestinationAddress, context: Parent.context);

                    if(selectedChannel.DestinationIncrementingAddress)
                    {
                        selectedChannel.DestinationAddress += (uint)writeLength;
                    }
                }
            }
        }


        private abstract class DMANOP_base : Instruction
        {
            public DMANOP_base(PL330_DMA parent, uint length, bool usableByChannel = true, bool usableByManager = false) 
                : base(parent, length, usableByChannel, usableByManager)
            {}

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                // NOP - intentionally no action
                return Length;
            }
        }

        private class DMANOP : DMANOP_base
        {
            public DMANOP(PL330_DMA parent) : base(parent, length: 1, usableByManager: true) {}
        }

        private class DMAWMB : DMANOP_base
        {
            // Write memory barrier - treat as NOP
            // for us each load/store instruction has immediate result
            // so there should be no need for a barrier operation
            public DMAWMB(PL330_DMA parent) : base(parent, length: 1) {}
        }

        private class DMARMB : DMANOP_base
        {
            // See: DMAWMB for explanation
            public DMARMB(PL330_DMA parent) : base(parent, length: 1) {}
        }


        private class DMASEV : Instruction
        {
            public DMASEV(PL330_DMA parent) : base(parent, length: 2, usableByManager: true) {}
            
            protected override void ParseCompleteAction()
            {
                eventNumber = (uint)instructionBytes[1] >> 3;
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(threadType == DMAThreadType.Manager)
                {
                    Parent.Log(LogLevel.Error, "SEV on Manager thread is currently unsupported");
                }
                else
                {
                    if(!Parent.SignalEventOrInterrupt(eventNumber))
                    {
                        Parent.channels[channelIndex.Value].SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    }
                }
                return Length;
            }

            private uint eventNumber;
        }


        private class DMAWFE : Instruction
        {
            public DMAWFE(PL330_DMA parent) : base(parent, length: 2, usableByManager: true) {}
            
            protected override void ParseCompleteAction()
            {
                eventNumber = (uint)instructionBytes[1] >> 3;
                invalid = ((instructionBytes[1] >> 1) & 0x1) == 1;
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(threadType == DMAThreadType.Manager)
                {
                    Parent.Log(LogLevel.Error, "WFE on Manager thread is currently unsupported");
                }
                else
                {
                    var selectedChannel = Parent.channels[channelIndex.Value];

                    if(!Parent.IsEventOrInterruptValid(eventNumber))
                    {
                        selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                        return Length;
                    }

                    if(Parent.eventActive[eventNumber])
                    {
                        // If the event was pending before, let's deactivate it and continue execution normally
                        Parent.eventActive[eventNumber] = false;
                    }
                    else
                    {
                        // If the event is not active, then let's wait
                        selectedChannel.WaitingEventOrPeripheralNumber = eventNumber;
                        selectedChannel.Status = Channel.ChannelStatus.WaitingForEvent;
                        Parent.Log(LogLevel.Noisy, "DMAWFE: Channel {0} is waiting for event: {1}", selectedChannel.Id, eventNumber);
                    }
                }
                return Length;
            }

            private uint eventNumber;
            // Invalid bit is used to force DMAC to invalidate its icache - we don't have any cache implemented
            private bool invalid;
        }

        private class DMAMOV : Instruction
        {
            public DMAMOV(PL330_DMA parent) : base(parent, length: 6) {}

            protected override void ParseCompleteAction()
            {
                registerNumber = instructionBytes[1] & 0b111;
                foreach(var b in instructionBytes.Reverse().Take(4))
                {
                    immediate <<= 8;
                    immediate |= b;
                }
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                var selectedChannel = Parent.channels[channelIndex.Value];
                switch(registerNumber)
                {
                    case 0b000: // SAR
                        selectedChannel.SourceAddress = immediate;
                        break;
                    case 0b001: // CCR
                        selectedChannel.ChannelControlRawValue = immediate;
                        break;
                    case 0b010: // DAR
                        selectedChannel.DestinationAddress = immediate;
                        break;
                    default:
                        Parent.Log(LogLevel.Error, "Invalid destination bits: {0}", registerNumber);
                        selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                        break;
                }
                return Length;
            }

            private uint immediate;
            private int registerNumber;
        }

        private class DMALP : Instruction
        {
            public DMALP(PL330_DMA parent, int loopCounterIndex) : base(parent, length: 2)
            {
                this.loopCounterIndex = loopCounterIndex;
            }

            protected override void ParseCompleteAction()
            {
                loopIterations = instructionBytes[1];
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                // There is no need to save loop start address here, it's done by assembler at code generation time in DMALPEND
                var selectedChannel = Parent.channels[channelIndex.Value];
                selectedChannel.LoopCounter[loopCounterIndex] = loopIterations;
                return Length;
            }

            private byte loopIterations;

            private readonly int loopCounterIndex;
        }

        private class DMALPEND : Instruction
        {
            public DMALPEND(PL330_DMA parent, bool isConditional = false, bool isForever = false, int loopCounterIndex = 0, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single) : base(parent, length: 2)
            {
                this.isConditional = isConditional;
                this.isForever = isForever;
                this.loopCounterIndex = loopCounterIndex;
                this.transactionType = transactionType;
            }

            protected override void ParseCompleteAction()
            {
                backwardsJump = instructionBytes[1];
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                if(!isConditional)
                {
                    return DoJump(channelIndex.Value);
                }
                
                var requestType = Parent.channels[channelIndex.Value].RequestType;
                if(requestType == transactionType)
                {
                    return DoJump(channelIndex.Value);
                }

                // Treat as NOP
                return Length;
            }

            private ulong DoJump(int channelIndex)
            {
                var channel = Parent.channels[channelIndex];

                bool shouldExitLoop = (!isForever && (channel.LoopCounter[loopCounterIndex] == 0)) 
                                        || (isForever && channel.RequestLast);
                if(shouldExitLoop)
                {
                    return Length;
                }

                channel.PC -= backwardsJump;

                if(!isForever)
                {
                    --channel.LoopCounter[loopCounterIndex];
                }
                return 0;
            }

            private byte backwardsJump;

            private readonly bool isConditional;
            private readonly bool isForever;
            private readonly int loopCounterIndex;
            private readonly Channel.ChannelRequestType transactionType;
        }

        private class DMAFLUSHP : Instruction
        {
            // We don't model FLUSHP requests to the peripheral
            // but let's use it to bind a peripheral to a channel
            // this information will be cleared on END or KILL
            public DMAFLUSHP(PL330_DMA parent) : base(parent, length: 2) {}

            protected override void ParseCompleteAction()
            {
                peripheral = (byte)(instructionBytes[1] >> 3);
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                var selectedChannel = Parent.channels[channelIndex.Value];

                if(!Parent.IsPeripheralInterfaceValid(peripheral))
                {
                    selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    return Length;
                }
                selectedChannel.Peripheral = peripheral;

                return Length;
            }

            private byte peripheral;
        }

        private class DMAWFP : Instruction
        {
            public DMAWFP(PL330_DMA parent, bool isPeripheralDriven = false, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single) : base(parent, length: 2)
            {
                this.isPeripheralDriven = isPeripheralDriven;
                this.transactionType = transactionType;
            }

            protected override void ParseCompleteAction()
            {
                peripheral = (byte)(instructionBytes[1] >> 3);
            }

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                var selectedChannel = Parent.channels[channelIndex.Value];

                if(!Parent.IsPeripheralInterfaceValid(peripheral))
                {
                    selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    return Length;
                }
                selectedChannel.Peripheral = peripheral;

                if(isPeripheralDriven)
                {
                    // This feature is not yet implemented in this model
                    // treat it as invalid operand case, so a driver can kill the DMA thread and potentially recover
                    Parent.Log(LogLevel.Error, "DMAWFP: Channel {0}, waiting for peripheral: {1}, cannot be peripheral driven (periph bit set) - this is not yet supported. Aborting thread.", selectedChannel.Id, peripheral);
                    selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    return Length;
                }

                // Wait for peripheral here
                selectedChannel.WaitingEventOrPeripheralNumber = peripheral;
                selectedChannel.Status = Channel.ChannelStatus.WaitingForPeripheral;

                // Since we don't support `periph` we operate under the simplified assumption
                // that we will be woken up by the correct transfer type from the peripheral
                // this might not always be correct
                selectedChannel.RequestType = transactionType;
                selectedChannel.RequestLast = false;

                Parent.Log(LogLevel.Noisy, "DMAWFP: Channel {0} is waiting for peripheral: {1}", selectedChannel.Id, peripheral);
                return Length;
            }

            private byte peripheral;

            private readonly bool isPeripheralDriven;
            private readonly Channel.ChannelRequestType transactionType;
        }

        private class DMALDP : DMA_LD_ST_base
        {
            public DMALDP(PL330_DMA parent, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single)
                : base(parent, true, transactionType, length: 2)
            {
                this.transactionType = transactionType;
            }

            protected override void ParseCompleteAction()
            {
                peripheral = (byte)(instructionBytes[1] >> 3);
            }

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                if(!Parent.IsPeripheralInterfaceValid(peripheral))
                {
                    selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    return;
                }
                selectedChannel.Peripheral = peripheral;

                var dmaEngine = new DmaEngine(Parent.machine.GetSystemBus(Parent));
                var readLengthInBytes = selectedChannel.SourceBurstLength * selectedChannel.SourceReadSize;

                var bufferPlace = new Place(new byte[readLengthInBytes], 0);

                var request = new Request(
                    selectedChannel.SourceAddress,
                    bufferPlace,
                    readLengthInBytes,
                    (TransferType)selectedChannel.SourceReadSize,
                    (TransferType)selectedChannel.DestinationWriteSize,
                    selectedChannel.SourceIncrementingAddress,
                    selectedChannel.DestinationIncrementingAddress
                );
                var response = dmaEngine.IssueCopy(request, Parent.context);

                // Update address, if it was incrementing
                selectedChannel.SourceAddress = (uint)response.ReadAddress.Value;
                selectedChannel.localMFIFO.EnqueueRange(bufferPlace.Array);
            }

            private byte peripheral;

            private readonly Channel.ChannelRequestType transactionType;
        }

        private class DMASTP : DMA_LD_ST_base
        {
            public DMASTP(PL330_DMA parent, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single)
                : base(parent, true, transactionType, length: 2)
            {
                this.transactionType = transactionType;
            }

            protected override void ParseCompleteAction()
            {
                peripheral = (byte)(instructionBytes[1] >> 3);
            }

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                if(!Parent.IsPeripheralInterfaceValid(peripheral))
                {
                    selectedChannel.SignalChannelAbort(Channel.ChannelFaultReason.InvalidOperand);
                    return;
                }
                selectedChannel.Peripheral = peripheral;

                var dmaEngine = new DmaEngine(Parent.machine.GetSystemBus(Parent));
                var writeLengthInBytes = selectedChannel.DestinationBurstLength * selectedChannel.DestinationWriteSize;

                // If requested, swap endianness of data in buffer, just before the transmission
                if(selectedChannel.EndianSwapSize > 1)
                {
                    DoEndianSwap(selectedChannel, writeLengthInBytes);
                }

                var bufferPlace = new Place(selectedChannel.localMFIFO.DequeueRange(writeLengthInBytes), 0);

                var request = new Request(
                    bufferPlace,
                    selectedChannel.DestinationAddress,
                    writeLengthInBytes,
                    (TransferType)selectedChannel.SourceReadSize,
                    (TransferType)selectedChannel.DestinationWriteSize,
                    selectedChannel.SourceIncrementingAddress,
                    selectedChannel.DestinationIncrementingAddress
                );
                var response = dmaEngine.IssueCopy(request, Parent.context);

                // Update address, if it was incrementing
                selectedChannel.DestinationAddress = (uint)response.WriteAddress.Value;
            }

            private byte peripheral;

            private readonly Channel.ChannelRequestType transactionType;
        }

    }
}
