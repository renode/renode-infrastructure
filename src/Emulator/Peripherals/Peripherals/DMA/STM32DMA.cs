//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class STM32DMA : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public STM32DMA(Machine machine)
        {
            streamFinished = new bool[NumberOfStreams];
            streams = new Stream[NumberOfStreams];
            for(var i = 0; i < streams.Length; i++)
            {
                streams[i] = new Stream(this, i);
            }
            this.machine = machine;
            engine = new DmaEngine(machine);
            Reset();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get { var i = 0; return streams.ToDictionary(x => i++, y => (IGPIO)y.IRQ); } }

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
                    return streams[offset / 0x14].Read(offset % 0x14);
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
                    streams[offset / 0x18].Write(offset % 0x18, value);
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
        private readonly Machine machine;

        private const int NumberOfStreams = 8;
        private const int StreamOffsetStart = 0x10;
        private const int StreamOffsetEnd = 0xCC;

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
                    parent.Log(LogLevel.Warning, "Unexpected read access to not implemented register (offset 0x{0:X}, value 0x{1:X}).", offset);
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
                memoryTransferType = TransferType.Byte;
                peripheralTransferType = TransferType.Byte;
                memoryIncrementAddress = false;
                peripheralIncrementAddress = false;
                direction = Direction.PeripheralToMemory;
                interruptOnComplete = false;
            }

            private void DoTransfer()
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
                var request = new Request(sourceAddress, destinationAddress, numberOfData, sourceTransferType, destinationTransferType,
                                  incrementSourceAddress, incrementDestinationAddress);
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
                    DoTransfer();
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

