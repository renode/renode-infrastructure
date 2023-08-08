//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Threading;
using System.Threading.Tasks;


namespace Antmicro.Renode.Peripherals.DMA
{
    public class VybridDma : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {
        public VybridDma(Machine mach)
        {
            machine = mach;
            engine = new DmaEngine(machine.GetSystemBus(this));

            channels = new Channel[32];
            for (var i = 0; i < 32; i++) 
            {
               channels[i] = new Channel(this, i);
            }

            IRQ = new GPIO();
        }

        public ushort ReadWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteWord(long offset, ushort value)
        {
            if (offset < 0x1000) {
                this.LogUnhandledWrite(offset, value);
                return;
            }
            uint channel = (uint)((offset - 0x1000) / 0x20);
            var operation = (offset - 0x1000) - (channel * 0x20);
            if (channel > 31) {
                this.Log(LogLevel.Error, "Channel is greater than 31");
                return;
            }
            channels[channel].WriteWord(operation, value);
            UpdateIRQ();
        }

        public byte ReadByte(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            switch((Register)offset)
            {
            case Register.SetEnableRequest:
                EnableRequestRegister |= (uint)(1<<value);
                DoCopy();
                break;
            case Register.ClearInterruptRequest:
                InterruptRequestRegister &= (uint)~(1<<value);
                break;
            case Register.ClearEnableRequest:
                EnableRequestRegister &= (uint)~(1<<value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
            UpdateIRQ();
        }

        public uint ReadDoubleWord(long address)
        {
            if (address < 0x1000) {
                    switch ((Register)address)
                    {
                        case Register.InterruptRequest:
                                return InterruptRequestRegister;
                    }
            }
            this.LogUnhandledRead(address);
            return 0;
        }

        public void WriteDoubleWord(long address, uint value)
        {
            if (address < 0x1000) {
                    switch ((Register)address)
                    {
                        default:
                                this.LogUnhandledWrite(address, value);
                                break;
                    }
                    return;
            }
            
            var channel = (address - 0x1000) / 0x20;
            var operation = (address - 0x1000) - (channel * 0x20);

            if (channel > 31) {
                this.Log(LogLevel.Error, "Channel is greater than 31");
                return;
            }

            channels[channel].WriteDoubleWord(operation, value);
            UpdateIRQ();
        }

        public bool DoCopy()
        {
            bool res = false;
            if (EnableRequestRegister == 0) return false;
            for (int i = 0; i < 32; i++) {
                    if ((EnableRequestRegister & (1 << i)) == 0) continue;
                    var channel = channels[i];
                    if (!channel.DoCopy()) continue;
                    res = true;
        
                    // TODO: also move those to channel.DoCopy() ?
                    if ((channel.TCD_ControlAndStatus & (1u << 3)) != 0)
                    {
                        EnableRequestRegister &= ~(1u << channel.channelNumber);
                    }
        
                    if ((channel.TCD_ControlAndStatus & (1u << 1)) != 0)
                    {
                        InterruptRequestRegister |= (1u << channel.channelNumber);
                    }
            }
            if (res) UpdateIRQ();
            return res;
        }

        public void Reset()
        {
            InterruptRequestRegister = 0;
            UpdateIRQ();
        }

        private void UpdateIRQ() {
                if (InterruptRequestRegister != 0) {
                        this.NoisyLog("IRQ is on, val = {0:X},enable={1:X}", InterruptRequestRegister, EnableRequestRegister);
                        IRQ.Set();
                } else {
                        IRQ.Unset();
                }
        }

        //private uint EnableErrorInterruptRegister;
        private uint EnableRequestRegister;
        private uint InterruptRequestRegister;
        //private uint HardwareRequestStatusRegister;
        //private uint ControlRegister;
        //private uint ErrorRegister;

        private readonly IMachine machine;
        private readonly DmaEngine engine;

        public GPIO IRQ { get; private set; }

        private enum Register : long
        {
            Control                     = 0x00,
            EnableErrorInterrupt        = 0x14,
            ClearEnableErrorInterupt    = 0x18,
            SetEnableErrorInterrupt     = 0x19,
            ClearEnableRequest          = 0x1A,
            SetEnableRequest            = 0x1B,
            ClearDONEStatusBit          = 0x1C,
            ClearError                  = 0x1E,
            ClearInterruptRequest       = 0x1F,
            InterruptRequest            = 0x24,
            Error                       = 0x2C,
            HardwareRequestStatus       = 0x34,
        }

        private sealed class Channel
        {
                public Channel(VybridDma parent, int number) {
                        this.parent = parent;
                        channelNumber = number;
                }

                public void WriteDoubleWord(long offset, uint value) {
                    switch ((ChannelRegister)offset) {
                         case ChannelRegister.TCD_MinorByteCount:
                            TCD_MinorByteCount = value;
                            break;
                         case ChannelRegister.TCD_SourceAddress:
                            TCD_SourceAddress = value;
                            break;
                         case ChannelRegister.TCD_DestinationAddress:
                            TCD_DestinationAddress = value;
                            break;
                         default:
                            parent.Log(LogLevel.Noisy, "Unhandled DWORD write val=0x{2:X} offset 0x{0:X} on channel {1}",offset,channelNumber,value);
                            break;
                    }
                }

                public void WriteWord(long offset, ushort value) {
                    switch((ChannelRegister)offset) {
                        case ChannelRegister.TCD_CurrentMajorLoopCount:
                            TCD_CurrentMajorLoopCount = value;
                            break;
                        case ChannelRegister.TCD_ControlAndStatus:
                            TCD_ControlAndStatus = value;
                            break;
                        default:
                            parent.Log(LogLevel.Noisy, "Unhandled WORD write val=0x{2:X} offset 0x{0:X} on channel {1}",offset,channelNumber,value);
                            break;
                    }
                }

                public bool DoCopy() {
		    if ((TCD_ControlAndStatus & (1u << 7)) > 0) return false;
                    uint size = (uint)(TCD_MinorByteCount * TCD_CurrentMajorLoopCount);
                    parent.Log(LogLevel.Noisy, "Channel {0} : copying data size {1} from 0x{2:X} to 0x{3:X}", channelNumber, size, TCD_SourceAddress, TCD_DestinationAddress);
                    if (size == 0) {
                        parent.Log(LogLevel.Error, "Error: size is 0 - stopping copy request (minor={0},major={1})", TCD_MinorByteCount, TCD_CurrentMajorLoopCount);
                        return false;
                    }
                    var request = new Request(TCD_SourceAddress, TCD_DestinationAddress, (int)size, TransferType.Byte, TransferType.Byte, incrementWriteAddress: false);
                    parent.engine.IssueCopy(request);

                    TCD_ControlAndStatus |= (1u << 7);
                    return true;
                }

                public readonly int channelNumber;
                private readonly VybridDma parent;

                uint TCD_SourceAddress;
                //uint TCD_SignedSourceAddressOffset;
                //uint TCD_TransferAttributes;
                uint TCD_MinorByteCount;
                //uint TCD_LastSourceAddressAdjustment;
                uint TCD_DestinationAddress;
                //uint TCD_SignedDestinationAddressOffset;
                uint TCD_CurrentMajorLoopCount;
                //uint TCD_LastDestinationAddressAdjustment;
                public uint TCD_ControlAndStatus;
        
                private enum ChannelRegister : long
                {
                    TCD_SourceAddress                       = 0x00,
                    TCD_SignedSourceAddressOffset           = 0x04,
                    TCD_TransferAttributes                  = 0x06,
                    TCD_MinorByteCount                      = 0x08,
                    TCD_LastSourceAddressAdjustment         = 0x0C,
                    TCD_DestinationAddress                  = 0x10,
                    TCD_SignedDestinationAddressOffset      = 0x14,
                    TCD_CurrentMajorLoopCount               = 0x16,           
                    TCD_LastDestinationAddressAdjustment    = 0x18,
                    TCD_ControlAndStatus                    = 0x1C,
                }
        }

        private readonly Channel[] channels;
    }
}

