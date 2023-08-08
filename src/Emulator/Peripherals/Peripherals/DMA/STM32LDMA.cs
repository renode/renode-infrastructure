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
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32LDMA : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public STM32LDMA(IMachine machine)
        {
            engine = new DmaEngine(machine.GetSystemBus(this));
            channels = new Channel[8];
            for(var i = 0; i < channels.Length; i++)
            {
                channels[i] = new Channel(this, i);
            }
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

        public IReadOnlyDictionary<int, IGPIO> Connections { get { var i = 0; return channels.ToDictionary(x => i++, y => (IGPIO)y.IRQ); } }
            
        public void Reset()
        {
            // TODO
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

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

            public GPIO IRQ { get; private set; }

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
            }

            private uint HandleConfigurationRead()
            {
                var returnValue = 0u;
                returnValue |= completeInterruptEnabled ? (1u << 1) : 0u;
                returnValue |= transferErrorInterruptEnabled ? (1u << 3) : 0u;
                returnValue |= ((uint)direction) << 4;
                returnValue |= peripheralIncrement ? (1u << 6) : 0u;
                returnValue |= memoryIncrement ? (1u << 7) : 0u;
                returnValue |= (uint)(priority << 12);
                return returnValue;
            }

            private void HandleConfigurationWrite(uint value)
            {
                completeInterruptEnabled = (value & (1 << 1)) != 0;
                transferErrorInterruptEnabled = (value & (1 << 3)) != 0;
                direction = (Direction)((value >> 4) & 1);
                peripheralIncrement = (value & (1 << 6)) != 0;
                memoryIncrement = (value & (1 << 7)) != 0;
                priority = (byte)((value >> 12) & 3);

                if((value & ~0x30DB) != 0)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: some unhandled bits were written to configuration register. Value is 0x{1:X}.", channelNo, value);
                }

                if((value & 1) != 0)
                {
                    DoTransfer();
                }
            }

            private void DoTransfer()
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
               
                var request = new Request(sourceAddress, destinationAddress, numberOfData, sourceTransferType, destinationTransferType,
                                  incrementSourceAddress, incrementDestinationAddress);
                parent.engine.IssueCopy(request);
                IRQ.Set();
            }

            private enum Offset
            {
                Configuration = 0x0,
                NumberOfData = 0x4,
                PeripheralAddress = 0x8,
                MemoryAddress = 0xC
            }

            private bool memoryIncrement;
            private bool peripheralIncrement;
            private uint peripheralAddress;
            private uint memoryAddress;
            private TransferType memoryTransferType;
            private TransferType peripheralTransferType;
            private bool completeInterruptEnabled;
            private bool transferErrorInterruptEnabled;
            private int numberOfData;
            private byte priority;
            private Direction direction;
            private readonly STM32LDMA parent;
            private readonly int channelNo;
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

        private readonly DmaEngine engine;
        private readonly Channel[] channels;
    }
}

