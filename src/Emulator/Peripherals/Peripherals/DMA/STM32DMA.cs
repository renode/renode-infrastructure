//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32DMA : IDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public STM32DMA(IMachine machine)
        {
            streamFinished = new bool[NumberOfStreams];
            streams = new Stream[NumberOfStreams];
            for(var i = 0; i < streams.Length; i++)
            {
                streams[i] = new Stream(this, i);
            }
            this.machine = machine;
            engine = new DmaEngine(machine.GetSystemBus(this));
            Reset();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections
        {
           get
           {
              var i = 0;
              return streams.ToDictionary(x => i++, y => (IGPIO)y.IRQ);
           }
        }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.LowInterruptStatus:
            case Registers.HighInterruptStatus:
                return HandleInterruptRead((int)(offset/4));
            default:
                if(offset >= StreamOffsetStart && offset <= StreamOffsetEnd)
                {
                    offset -= StreamOffsetStart;
                    return streams[offset / StreamSize].Read(offset % StreamSize);
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.LowInterruptClear:
            case Registers.HighInterruptClear:
                HandleInterruptClear((int)((offset - 8)/4), value);
                break;
            default:
                if(offset >= StreamOffsetStart && offset <= StreamOffsetEnd)
                {
                    offset -= StreamOffsetStart;
                    streams[offset / StreamSize].Write(offset % StreamSize, value);
                }
                else
                {
                    this.LogUnhandledWrite(offset, value);
                }
                break;
            }
        }

        public void Reset()
        {
            streamFinished.Initialize();
            foreach(var stream in streams)
            {
                stream.Reset();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= streams.Length)
            {
                this.Log(LogLevel.Error, "Attempted to start non-existing DMA stream number: {0}. Maximum value is {1}", number, streams.Length);
                return;
            }

            if(value)
            {
                this.Log(LogLevel.Debug, "DMA peripheral request on stream {0} {1}", number, value);
                if(streams[number].Enabled)
                {
                    streams[number].DoPeripheralTransfer();
                }
                else
                {
                    this.Log(LogLevel.Warning, "DMA peripheral request on stream {0} ignored", number);
                }
            }
        }

        private uint HandleInterruptRead(int offset)
        {
            lock(streamFinished)
            {
                var returnValue = 0u;
                for(var i = 4 * offset; i < 4 * (offset + 1); i++)
                {
                    if(streamFinished[i])
                    {
                        returnValue |= 1u << BitNumberForStream(i - 4 * offset);
                    }
                }
                return returnValue;
            }
        }

        private void HandleInterruptClear(int offset, uint value)
        {
            lock(streamFinished)
            {
                for(var i = 4 * offset; i < 4 * (offset + 1); i++)
                {
                    var bitNo = BitNumberForStream(i - 4 * offset);
                    if((value & (1 << bitNo)) != 0)
                    {
                        streamFinished[i] = false;
                        streams[i].IRQ.Unset();
                    }
                }
            }
        }

        private static int BitNumberForStream(int streamNo)
        {
            switch(streamNo)
            {
            case 0:
                return 5;
            case 1:
                return 11;
            case 2:
                return 21;
            case 3:
                return 27;
            default:
                throw new InvalidOperationException("Should not reach here.");
            }
        }

        private readonly bool[] streamFinished;
        private readonly Stream[] streams;
        private readonly DmaEngine engine;
        private readonly IMachine machine;

        private const int NumberOfStreams = 8;
        private const int StreamOffsetStart = 0x10;
        private const int StreamOffsetEnd = 0xCC;
        private const int StreamSize = 0x18;

        private enum Registers
        {
            LowInterruptStatus = 0x0, // DMA_LISR
            HighInterruptStatus = 0x4, // DMA_HISR
            LowInterruptClear = 0x8, //DMA_LIFCR
            HighInterruptClear = 0xC // DMA_HIFCR
        }

        private class Stream
        {
            public Stream(STM32DMA parent, int streamNo)
            {
                this.parent = parent;
                this.streamNo = streamNo;
                IRQ = new GPIO();
            }

            public uint Read(long offset)
            {
                switch((Registers)offset)
                {
                case Registers.Configuration:
                    return HandleConfigurationRead();
                case Registers.NumberOfData:
                    return (uint)numberOfData;
                case Registers.PeripheralAddress:
                    return peripheralAddress;
                case Registers.Memory0Address:
                    return memory0Address;
                case Registers.Memory1Address:
                    return memory1Address;
                default:
                    parent.Log(LogLevel.Warning, "Unexpected read access from not implemented register (offset 0x{0:X}).", offset);
                    return 0;
                }
            }

            public void Write(long offset, uint value)
            {
                switch((Registers)offset)
                {
                case Registers.Configuration:
                    HandleConfigurationWrite(value);
                    break;
                case Registers.NumberOfData:
                    numberOfData = (int)value;
                    break;
                case Registers.PeripheralAddress:
                    peripheralAddress = value;
                    break;
                case Registers.Memory0Address:
                    memory0Address = value;
                    break;
                case Registers.Memory1Address:
                    memory1Address = value;
                    break;
                default:
                    parent.Log(LogLevel.Warning, "Unexpected write access to not implemented register (offset 0x{0:X}, value 0x{1:X}).", offset, value);
                    break;
                }
            }

            public GPIO IRQ { get; private set; }

            public void Reset()
            {
                memory0Address = 0u;
                memory1Address = 0u;
                numberOfData = 0;
                transferredSize = 0;
                memoryTransferType = TransferType.Byte;
                peripheralTransferType = TransferType.Byte;
                memoryIncrementAddress = false;
                peripheralIncrementAddress = false;
                direction = Direction.PeripheralToMemory;
                interruptOnComplete = false;
                Enabled = false;
            }

            private Request CreateRequest(int? size = null, int? destinationOffset = null)
            {
                var sourceAddress = 0u;
                var destinationAddress = 0u;
                switch(direction)
                {
                case Direction.PeripheralToMemory:
                case Direction.MemoryToMemory:
                    sourceAddress = peripheralAddress;
                    destinationAddress = memory0Address;
                    break;
                case Direction.MemoryToPeripheral:
                    sourceAddress = memory0Address;
                    destinationAddress = peripheralAddress;
                    break;
                }

                var sourceTransferType = direction == Direction.PeripheralToMemory ? peripheralTransferType : memoryTransferType;
                var destinationTransferType = direction == Direction.MemoryToPeripheral ? peripheralTransferType : memoryTransferType;
                var incrementSourceAddress = direction == Direction.PeripheralToMemory ? peripheralIncrementAddress : memoryIncrementAddress;
                var incrementDestinationAddress = direction == Direction.MemoryToPeripheral ? peripheralIncrementAddress : memoryIncrementAddress;
                return new Request(sourceAddress, (uint)(destinationAddress + (destinationOffset ?? 0)), size ?? numberOfData, sourceTransferType, destinationTransferType,
                        incrementSourceAddress, incrementDestinationAddress);
            }

            public void DoTransfer()
            {
                var request = CreateRequest(numberOfData * (int)memoryTransferType);
                if(request.Size > 0)
                {
                    lock(parent.streamFinished)
                    {
                        parent.engine.IssueCopy(request);
                        parent.streamFinished[streamNo] = true;
                        if(interruptOnComplete)
                        {
                            parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => IRQ.Set());
                        }
                    }
                }
            }

            public void DoPeripheralTransfer()
            {
                var request = CreateRequest((int)memoryTransferType, transferredSize);
                transferredSize += (int)memoryTransferType;
                if(request.Size > 0)
                {
                    lock(parent.streamFinished)
                    {
                        parent.engine.IssueCopy(request);
                        if(transferredSize == numberOfData * (int)memoryTransferType)
                        {
                            transferredSize = 0;
                            parent.streamFinished[streamNo] = true;
                            Enabled = false;
                            if(interruptOnComplete)
                            {
                                parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => IRQ.Set());
                            }
                        }
                    }
                }
            }

            public bool Enabled { get; private set; }

            private uint HandleConfigurationRead()
            {
                var returnValue = 0u;
                returnValue |= (uint)(channel << 25);
                returnValue |= (uint)(priority << 16);

                returnValue |= FromTransferType(memoryTransferType) << 13;
                returnValue |= FromTransferType(peripheralTransferType) << 11;
                returnValue |= memoryIncrementAddress ? (1u << 10) : 0u;
                returnValue |= peripheralIncrementAddress ? (1u << 9) : 0u;
                returnValue |= ((uint)direction) << 6;
                returnValue |= interruptOnComplete ? (1u << 4) : 0u;
                // regarding enable bit - our transfer is always finished
                return returnValue;
            }

            private void HandleConfigurationWrite(uint value)
            {
                // we ignore channel selection and priority
                channel = (byte)((value >> 25) & 7);
                priority = (byte)((value >> 16) & 3);

                memoryTransferType = ToTransferType(value >> 13);
                peripheralTransferType = ToTransferType(value >> 11);
                memoryIncrementAddress = (value & (1 << 10)) != 0;
                peripheralIncrementAddress = (value & (1 << 9)) != 0;
                direction = (Direction)((value >> 6) & 3);
                interruptOnComplete = (value & (1 << 4)) != 0;
                // we ignore transfer error interrupt enable as we never post errors
                if((value & ~0xE037ED5) != 0)
                {
                    parent.Log(LogLevel.Warning, "Channel {0}: unsupported bits written to configuration register. Value is 0x{1:X}.", streamNo, value);
                }

                if((value & 1) != 0)
                {
                    if(direction != Direction.PeripheralToMemory)
                    {
                        DoTransfer();
                    }
                    else
                    {
                        Enabled = true;
                    }
                }
            }

            private TransferType ToTransferType(uint dataSize)
            {
                dataSize &= 3;
                switch(dataSize)
                {
                case 0:
                    return TransferType.Byte;
                case 1:
                    return TransferType.Word;
                case 2:
                    return TransferType.DoubleWord;
                default:
                    parent.Log(LogLevel.Warning, "Stream {0}: Non existitng possible value written as data size.", streamNo);
                    return TransferType.Byte;
                }
            }

            private static uint FromTransferType(TransferType transferType)
            {
                switch(transferType)
                {
                case TransferType.Byte:
                    return 0;
                case TransferType.Word:
                    return 1;
                case TransferType.DoubleWord:
                    return 2;
                }
                throw new InvalidOperationException("Should not reach here.");
            }

            private uint memory0Address;
            private uint memory1Address;
            private uint peripheralAddress;
            private int numberOfData;
            private int transferredSize;
            private TransferType memoryTransferType;
            private TransferType peripheralTransferType;
            private bool memoryIncrementAddress;
            private bool peripheralIncrementAddress;
            private Direction direction;
            private bool interruptOnComplete;
            private byte channel;
            private byte priority;

            private readonly STM32DMA parent;
            private readonly int streamNo;

            private enum Registers
            {
                Configuration = 0x0, // DMA_SxCR
                NumberOfData = 0x4, // DMA_SxNDTR
                PeripheralAddress = 0x8, // DMA_SxPAR
                Memory0Address = 0xC, // DMA_SxM0AR
                Memory1Address = 0x10, // DMA_SxM1AR
                FIFOControl = 0x14, // DMA_SxFCR
            }

            private enum Direction : byte
            {
                PeripheralToMemory = 0,
                MemoryToPeripheral = 1,
                MemoryToMemory = 2
            }
        }
    }
}

