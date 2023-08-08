//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class TegraDma : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public TegraDma(IMachine machine)
        {
            dmaEngine = new DmaEngine(machine.GetSystemBus(this));
            channels = new Channel[ChannelNo];

            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i] = new Channel(this, i);
                innerConnections[i] = channels[i].IRQ;
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            Reset();
        }

        public long Size
        {
            get
            {
                return 0x2000;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0x1000 && offset < 0x1000 + 32*ChannelNo)
            {
                return channels[(offset - 0x1000)/32].Read(offset % 32);
            }
            switch((Register)offset)
            {
            case Register.Counter:
                return counterRegister;
            case Register.Status:
                // currently we're never busy
                return activeChannelInterrupts;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= 0x1000 && offset < 0x1000 + 32*ChannelNo)
            {
                channels[(offset - 0x1000)/32].Write(offset % 32, value);
                return;
            }
            switch((Register)offset)
            {
            case Register.Command:
                enabled = ((1 << 31) & value) != 0;
                break;
            case Register.Counter:
                // throttle down is ignored by us, we only keep the value for the reads
                counterRegister = value;
                break;
            case Register.IrqMaskSet:
                irqMask |= value;
                break;
            case Register.IrqMaskClear:
                irqMask &= ~value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            enabled = false;
            counterRegister = 0;
            irqMask = 0;
            activeChannelInterrupts = 0;
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i].Reset();
            }
        }

        private enum Register
        {
            Command = 0x0,
            Status = 0x4,
            Counter = 0x10,
            IrqMaskSet = 0x20,
            IrqMaskClear = 0x24
        }

        private sealed class Channel
        {
            public Channel(TegraDma parent, int number)
            {
                this.parent = parent;
                channelNumber = number;
                IRQ = new GPIO();
            }

            public void Reset()
            {
                apbStartingAddress = 0;
                ahbStartingAddress =0;
                interruptEnabled = false;
                apbIsSource = false;
                apbTransferType = TransferType.Byte;
                ahbTransferType= TransferType.Byte;
            }

            public GPIO IRQ { get; private set; }

            public uint Read(long offset)
            {
                switch((ChannelRegister)offset)
                {
                case ChannelRegister.Status:
                    if((parent.activeChannelInterrupts & (1 << channelNumber)) != 0)
                    {
                        return 1 << 30;
                    }
                    return 0;
                default:
                    parent.Log(LogLevel.Warning, "Unhandled read from 0x{1:X} (channel {0}).", offset, channelNumber);
                    return 0;
                }
            }

            public void Write(long offset, uint value)
            {
                switch((ChannelRegister)offset)
                {
                case ChannelRegister.Control:
                    HandleControlWrite(value);
                    break;
                case ChannelRegister.Status:
                    if((value & (1 << 30)) == 0)
                    {
                        break;
                    }
                    parent.activeChannelInterrupts &= (ushort)~(1 << channelNumber);
                    IRQ.Unset();
                    break;
                case ChannelRegister.AhbStartingAddress:
                    ahbStartingAddress = value;
                    break;
                case ChannelRegister.ApbStartingAddress:
                    apbStartingAddress = 0x70000000;
                    apbStartingAddress |= value & 0xFFFF;
                    break;
                case ChannelRegister.ApbAddressSequencer:
                    // TODO: currently we ignore the send interrupt to COP option
                    apbTransferType = GetTransferType(value);
                    if((value & ~0xF0010000) != 0)
                    {
                        parent.Log(LogLevel.Warning, "Channel {0}: unsupported APB sequencer flags, value written is 0x{1:X}.", channelNumber, value);
                    }
                    break;
                case ChannelRegister.AhbAddressSequencer:
                    // TODO: currently we ignore the send interrupt to COP option
                    ahbTransferType = GetTransferType(value);
                    if((value & ~0xF4000000) != 0)
                    {
                        parent.Log(LogLevel.Warning, "Channel {0}: unsupported AHB sequencer flags, value written is 0x{1:X}.", channelNumber, value);
                    }
                    break;
                default:
                    parent.Log(LogLevel.Warning, "Unhandler write to offset 0x{1:X}, value 0x{2:X} (channel {0}).", channelNumber, offset, value);
                    break;
                }
            }

            private TransferType GetTransferType(uint value)
            {
                var busWidth = value >> 28;
                switch(busWidth)
                {
                case 0:
                    return TransferType.Byte;
                case 1:
                    return TransferType.Word;
                default:
                    return TransferType.DoubleWord;
                }
            }

            private void HandleControlWrite(uint value)
            {
                interruptEnabled = ((1 << 30) & value) != 0;
                var start = ((1 << 31) & value) != 0;
                apbIsSource = ((1 << 28) & value) == 0;
                if(((1 << 27) & value) == 0)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: unsupported multi block transfer.", channelNumber);
                }
                // check whether we have some not supported flags
                // note that we also ignore bit 21, i.e. flow control enable
                // D020 = enabled bits 21, 27, 28, 30 and 31
                if((value & (~0xD820FFFF)) != 0)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: unsupported flags in channel control register (value written was 0x{1:X}.", channelNumber, value);
                }
                if(start && parent.enabled)
                {
                    var sourceAddress = apbIsSource ? apbStartingAddress : ahbStartingAddress;
                    var destinationAddress = apbIsSource ? ahbStartingAddress : apbStartingAddress;
                    var sourceTransferType = apbIsSource ? apbTransferType : ahbTransferType;
                    var destinationTransferType = apbIsSource ? ahbTransferType : apbTransferType;
                    var size = ((((int)value & 0xFFFF) >> 2) + 1) << 2;
                    parent.DebugLog("Channel {0}: issuing copy: 0x{1:X} ({2}) ---> 0x{3:X} ({4}), size {5}B.", channelNumber, sourceAddress, sourceTransferType,
                                    destinationAddress, destinationTransferType, size);
                    parent.dmaEngine.IssueCopy(new Request(sourceAddress, destinationAddress, size, sourceTransferType, destinationTransferType));
                    parent.activeChannelInterrupts |= (ushort)(1 << channelNumber);
                    if(interruptEnabled)
                    {
                        IRQ.Set();
                    }
                }
            }

            private enum ChannelRegister
            {
                Control = 0x0,
                Status = 0x4,
                AhbStartingAddress = 0x10,
                AhbAddressSequencer = 0x14,
                ApbStartingAddress = 0x18,
                ApbAddressSequencer = 0x1c,
            }

            private uint apbStartingAddress;
            private uint ahbStartingAddress;
            private readonly TegraDma parent;
            private bool interruptEnabled;
            private readonly int channelNumber;
            private bool apbIsSource;
            private TransferType apbTransferType;
            private TransferType ahbTransferType;
        }

        private bool enabled;
        private uint counterRegister;
        private uint irqMask;
        private readonly Channel[] channels;
        private readonly DmaEngine dmaEngine;
        private ushort activeChannelInterrupts;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private const int ChannelNo = 16;
    }
}

