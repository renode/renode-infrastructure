//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32LDMA : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IDMA, IGPIOReceiver
    {
        public STM32LDMA(IMachine machine)
        {
            engine = new DmaEngine(machine.GetSystemBus(this));
            channels = new Channel[this.NumberOfChannels];
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i] = new Channel(this, i);
            }
        }

        public void Reset()
        {
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i].Reset();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= channels.Length)
            {
                this.WarningLog("Attempted to signal DMA channel {0}. Maximum value is {1}", number, channels.Length - 1);
                return;
            }
            channels[number].OnGPIO(value);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0x08 && offset <= 0x8C)
            {
                return channels[(offset - 0x08) / 0x14].Read(offset - 0x08 - ((offset - 0x08) / 0x14) * 0x14);
            }
            switch((Offset)offset)
            {
            case Offset.InterruptStatus:
                return HandleInterruptStatusRead();
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= 0x08 && offset <= 0x8C)
            {
                var channelNo = (offset - 0x08) / 0x14;
                channels[channelNo].Write(offset - 0x08 - channelNo * 0x14, value);
                return;
            }
            switch((Offset)offset)
            {
            case Offset.InterruptClear:
                HandleClearInterrupt(value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void RequestTransfer(int channel)
        {
            if(channel > 0 && channel <= channels.Length)
            {
                channels[channel - 1].DoTransfer();
            }
            else
            {
                this.Log(LogLevel.Warning, "Invalid channel {0}, no transfer performed.");
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get { var i = 0; return channels.ToDictionary(x => i++, y => (IGPIO)y.IRQ); } }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public int NumberOfChannels { get => 8; }

        private uint HandleInterruptStatusRead()
        {
            var returnValue = 0u;
            for(var i = 0; i < channels.Length; i++)
            {
                returnValue |= channels[i].IRQ.IsSet ? (3u << i * 4) : 0u;
            }
            return returnValue;
        }

        private void HandleClearInterrupt(uint value)
        {
            for(var i = 0; i < channels.Length; i++)
            {
                var ourBit1 = 4 * i;
                var ourBit2 = ourBit1 + 1;
                if((value & (1 << ourBit1)) != 0 || (value & (1 << ourBit2)) != 0)
                {
                    channels[i].ClearInterrupt();
                }
            }
        }

        private readonly DmaEngine engine;
        private readonly Channel[] channels;

        private sealed class Channel
        {
            public Channel(STM32LDMA parent, int channelNo)
            {
                this.parent = parent;
                memoryTransferType = TransferType.Byte;
                peripheralTransferType = TransferType.Byte;
                IRQ = new GPIO();
                this.channelNo = channelNo;
            }

            public uint Read(long offset)
            {
                switch((Offset)offset)
                {
                case Offset.Configuration:
                    return HandleConfigurationRead();
                case Offset.NumberOfData:
                    return (uint)numberOfData;
                case Offset.PeripheralAddress:
                    return peripheralAddress;
                case Offset.MemoryAddress:
                    return memoryAddress;
                default:
                    parent.Log(LogLevel.Warning, "Channel {0}: unhandled read from 0x{1:X}.", channelNo, offset);
                    return 0;
                }
            }

            public void Write(long offset, uint value)
            {
                switch((Offset)offset)
                {
                case Offset.Configuration:
                    HandleConfigurationWrite(value);
                    break;
                case Offset.NumberOfData:
                    numberOfData = (int)value;
                    break;
                case Offset.PeripheralAddress:
                    peripheralAddress = value;
                    break;
                case Offset.MemoryAddress:
                    memoryAddress = value;
                    break;
                default:
                    parent.Log(LogLevel.Warning, "Channel {0}: unhandled write 0x{1:X} to 0x{2:X}.", channelNo, offset);
                    break;
                }
            }

            public void ClearInterrupt()
            {
                IRQ.Unset();
            }

            public void Reset()
            {
                IRQ.Unset();
                peripheralIncrement = false;
                peripheralAddress = 0u;
                memoryAddress = 0u;
                memoryIncrement = false;
                memoryTransferType = 0;
                peripheralTransferType = 0;
                completeInterruptEnabled = false;
                transferErrorInterruptEnabled = false;
                numberOfData = 0;
                priority = 0;
                direction = 0;
                circularMode = false;
                halfTransferInterrupt = false;
            }

            public void DoTransfer()
            {
                uint sourceAddress, destinationAddress;
                bool incrementSourceAddress, incrementDestinationAddress;
                TransferType sourceTransferType, destinationTransferType;

                if(direction == Direction.ReadFromMemory)
                {
                    sourceAddress = memoryAddress;
                    destinationAddress = peripheralAddress;
                    incrementSourceAddress = memoryIncrement;
                    incrementDestinationAddress = peripheralIncrement;
                    sourceTransferType = memoryTransferType;
                    destinationTransferType = peripheralTransferType;
                }
                else
                {
                    sourceAddress = peripheralAddress;
                    destinationAddress = memoryAddress;
                    incrementSourceAddress = peripheralIncrement;
                    incrementDestinationAddress = memoryIncrement;
                    sourceTransferType = peripheralTransferType;
                    destinationTransferType = memoryTransferType;
                }

                var request = new Request(sourceAddress, destinationAddress, numberOfData * (int)sourceTransferType, sourceTransferType, destinationTransferType,
                                  incrementSourceAddress, incrementDestinationAddress);
                parent.engine.IssueCopy(request);
                if(completeInterruptEnabled)
                {
                    IRQ.Set();
                }
            }

            public void OnGPIO(bool value)
            {
                if(!value)
                {
                    return;
                }

                DoTransfer();
            }

            public GPIO IRQ { get; private set; }

            private uint HandleConfigurationRead()
            {
                var returnValue = 0u;
                returnValue |= completeInterruptEnabled ? (1u << 1) : 0u;
                returnValue |= halfTransferInterrupt ? (1u << 2) : 0u;
                returnValue |= transferErrorInterruptEnabled ? (1u << 3) : 0u;
                returnValue |= ((uint)direction) << 4;
                returnValue |= circularMode ? (1u << 5) : 0u;
                returnValue |= peripheralIncrement ? (1u << 6) : 0u;
                returnValue |= memoryIncrement ? (1u << 7) : 0u;
                returnValue |= ((uint)peripheralTransferType >> 1) << 8;
                returnValue |= ((uint)memoryTransferType >> 1) << 10;
                returnValue |= (uint)(priority << 12);
                return returnValue;
            }

            private void HandleConfigurationWrite(uint value)
            {
                bool previousHalfTransferInterrupt = halfTransferInterrupt;

                completeInterruptEnabled = (value & (1 << 1)) != 0;
                halfTransferInterrupt = (value & (1 << 2)) != 0;
                transferErrorInterruptEnabled = (value & (1 << 3)) != 0;
                direction = (Direction)((value >> 4) & 1);
                circularMode = (value & (1 << 5)) != 0;
                peripheralIncrement = (value & (1 << 6)) != 0;
                memoryIncrement = (value & (1 << 7)) != 0;
                HandleConfigureWriteSizes(value);
                priority = (byte)((value >> 12) & 3);

                if((value & ~0x3FFF) != 0)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: some unhandled bits were written to configuration register. Value is 0x{1:X}.", channelNo, value);
                }
                if(halfTransferInterrupt && !previousHalfTransferInterrupt)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: half transfer interrupt not supported.", channelNo);
                }

                if((value & 1) != 0)
                {
                    DoTransfer();
                }
            }

            private void HandleConfigureWriteSizes(uint value)
            {
                if((value & 1) == 0) // MSIZE and PSIZE are read-only if EN=1
                {
                    int size = 0;
                    if(DecodeConfigurationSize(value, 8, out size))
                    {
                        peripheralTransferType = (TransferType)(1 << size);
                    }
                    if(DecodeConfigurationSize(value, 10, out size))
                    {
                        memoryTransferType = (TransferType)(1 << size);
                    }
                }
            }

            private bool DecodeConfigurationSize(uint value, int offset, out int size)
            {
                size = (int)(value >> offset) & 3;
                if(size == 3)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: Invalid reserved value for size", channelNo);
                }
                return size < 3;
            }

            private Direction direction;
            private byte priority;
            private int numberOfData;
            private bool transferErrorInterruptEnabled;
            private bool completeInterruptEnabled;
            private TransferType memoryTransferType;
            private uint memoryAddress;
            private uint peripheralAddress;
            private bool peripheralIncrement;
            private bool circularMode;
            private bool halfTransferInterrupt;

            private bool memoryIncrement;
            private TransferType peripheralTransferType;
            private readonly STM32LDMA parent;
            private readonly int channelNo;

            private enum Offset
            {
                Configuration = 0x0,
                NumberOfData = 0x4,
                PeripheralAddress = 0x8,
                MemoryAddress = 0xC
            }
        }

        private enum Offset
        {
            InterruptStatus = 0x0,
            InterruptClear = 0x4
        }

        private enum Direction
        {
            ReadFromPeripheral = 0,
            ReadFromMemory = 1
        }
    }
}