//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
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
            decoderRoot.AddOpcode(0b01011100, 8, () => new DMAADNH(this, isDestinationAddressRegister: false));
            decoderRoot.AddOpcode(0b01011110, 8, () => new DMAADNH(this, isDestinationAddressRegister: true));
            
            decoderRoot.AddOpcode(0b00000000, 8, () => new DMAEND(this));
            // FLUSHP is part of Peripheral Request Interface. Leaving unimplemented for now
            decoderRoot.AddOpcode(0b00110101, 8, () => new DMAFLUSHP(this));
            
            decoderRoot.AddOpcode(0b10100000, 8, () => new DMAGO(this, nonSecure: false));
            decoderRoot.AddOpcode(0b10100010, 8, () => new DMAGO(this, nonSecure: true));

            // KILL and SEV are currently unimplemented, and are placeholders
            decoderRoot.AddOpcode(0b00000001, 8, () => new DMAKILL(this));
            decoderRoot.AddOpcode(0b00110100, 8, () => new DMASEV(this));
            decoderRoot.AddOpcode(0b00011000, 8, () => new DMANOP(this));

            decoderRoot.AddOpcode(0b00000100, 8, () => new DMALD(this, isConditional: false));
            decoderRoot.AddOpcode(0b00000101, 8, () => new DMALD(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00000111, 8, () => new DMALD(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst));

            decoderRoot.AddOpcode(0b00001000, 8, () => new DMAST(this, isConditional: false));
            decoderRoot.AddOpcode(0b00001001, 8, () => new DMAST(this, isConditional: true, transactionType: Channel.ChannelRequestType.Single));
            decoderRoot.AddOpcode(0b00001011, 8, () => new DMAST(this, isConditional: true, transactionType: Channel.ChannelRequestType.Burst));

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

            protected Instruction(PL330_DMA parent, uint Length = 1, bool usableByChannel = true, bool usableByManager = false)
            {
                this.usableByManager = usableByManager;
                this.usableByChannel = usableByChannel;
                this.Length = Length;
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
                    return Length;
                }
                if(threadType == DMAThreadType.Channel && !usableByChannel)
                {
                    Parent.Log(LogLevel.Error, "Thread is a channel, but instruction {0} not usable by channel", this.ToString());
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
            public DMAADH_base(PL330_DMA parent, bool isDestinationAddressRegister, bool negative) : base(parent, Length: 3)
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
                    Parent.channels[channelIndex.Value].Status = Channel.ChannelStatus.Stopped;
                    Parent.channels[channelIndex.Value].localMFIFO.Clear();
                }
                return Length;
            }
        }

        private class DMAFLUSHP : Instruction
        {
            public DMAFLUSHP(PL330_DMA parent) : base(parent) {}
            
            protected override void ParseCompleteAction()
            {
                peripheral = (byte)(instructionBytes[1] >> 3);
            }

            private byte peripheral;
        }

        private class DMAGO : Instruction
        {
            public DMAGO(PL330_DMA parent, bool nonSecure) : base(parent, Length: 6, usableByChannel: false, usableByManager: true)
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
        }

        private abstract class DMA_LD_ST_base : Instruction
        {
            public DMA_LD_ST_base(PL330_DMA parent, bool IsConditional, Channel.ChannelRequestType TransactionType) : base(parent)
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

            protected abstract void DoTransfer(int channelIndex, bool ignoreBurst);

            private readonly bool isConditional;
            private readonly Channel.ChannelRequestType transactionType;
        }

        private class DMALD : DMA_LD_ST_base
        {
            public DMALD(PL330_DMA parent, bool isConditional, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single)  : base(parent, isConditional, transactionType) {}

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                var readLength = selectedChannel.SourceReadSize;

                for(var burst = 0; burst < (ignoreBurst ? 1 : selectedChannel.SourceBurstLength); ++burst)
                {
                    var byteArray = Parent.machine.GetSystemBus(Parent).ReadBytes(selectedChannel.SourceAddress, readLength, context: Parent.GetCurrentCPUOrNull());
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
            public DMAST(PL330_DMA parent, bool isConditional, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single) : base(parent, isConditional, transactionType) {}

            protected override void DoTransfer(int channelIndex, bool ignoreBurst)
            {
                var selectedChannel = Parent.channels[channelIndex];
                var writeLength = selectedChannel.DestinationWriteSize;

                for(var burst = 0; burst < (ignoreBurst ? 1 : selectedChannel.DestinationBurstLength); ++burst)
                {
                    var byteArray = selectedChannel.localMFIFO.DequeueRange(writeLength);
                    if(byteArray.Length != writeLength)
                    {
                        Parent.Log(LogLevel.Error, "Underflow in channel queue, will write {0} bytes, instead of {1} expected", byteArray.Length, writeLength);
                    }
                    Parent.machine.GetSystemBus(Parent).WriteBytes(byteArray, selectedChannel.DestinationAddress, context: Parent.GetCurrentCPUOrNull());

                    if(selectedChannel.DestinationIncrementingAddress)
                    {
                        selectedChannel.DestinationAddress += (uint)writeLength;
                    }
                }
            }
        }

        private class DMANOP : Instruction
        {
            public DMANOP(PL330_DMA parent) : base(parent, usableByManager: true) {}

            protected override ulong ExecuteInner(DMAThreadType threadType, int? channelIndex = null)
            {
                // NOP - intentionally no action
                return Length;
            }
        }

        private class DMASEV : Instruction
        {
            public DMASEV(PL330_DMA parent) : base(parent, Length: 2, usableByManager: true) {}
            
            protected override void ParseCompleteAction()
            {
                eventNumber = instructionBytes[1] >> 3;
            }

            private int eventNumber;
        }

        private class DMAMOV : Instruction
        {
            public DMAMOV(PL330_DMA parent) : base(parent, Length: 6) {}

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
                switch(registerNumber)
                {
                    case 0b000: // SAR
                        Parent.channels[channelIndex.Value].SourceAddress = immediate;
                        break;
                    case 0b001: // CCR
                        Parent.channels[channelIndex.Value].ChannelControlRawValue = immediate;
                        break;
                    case 0b010: // DAR
                        Parent.channels[channelIndex.Value].DestinationAddress = immediate;
                        break;
                    default:
                        Parent.Log(LogLevel.Error, "Invalid destination bits: {0}", registerNumber);
                        break;
                }
                return Length;
            }

            private uint immediate;
            private int registerNumber;
        }

        private class DMALP : Instruction
        {
            public DMALP(PL330_DMA parent, int loopCounterIndex) : base(parent, Length: 2)
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
            public DMALPEND(PL330_DMA parent, bool isConditional = false, bool isForever = false, int loopCounterIndex = 0, Channel.ChannelRequestType transactionType = Channel.ChannelRequestType.Single) : base(parent, Length: 2)
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
    }
}