//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class NANDFlash : ISPIPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public NANDFlash(MappedMemory dataMemory, MappedMemory spareMemory,
                         uint pageSize = 2048, uint spareSize = 64,
                         uint pagesPerBlock = 64, uint blocksPerLun = 2048,
                         uint lunsPerChip = 1)
        {
            receiveStack = new Stack<byte>();
            sendQueue = new Queue<byte>();

            RegistersCollection = new ByteRegisterCollection(this);

            // Use separate data & spare areas so we can easily program
            // the data memory from a bin/hex file
            this.dataMemory = dataMemory;
            this.spareMemory = spareMemory;
            dataMemory.ResetByte = EmptySegment;
            spareMemory.ResetByte = EmptySegment;

            this.pageSize = pageSize;
            this.spareSize = spareSize;
            this.pagesPerBlock = pagesPerBlock;
            this.blocksPerLun = blocksPerLun;
            this.lunsPerChip = lunsPerChip;

            this.pageCache = new byte[pageSize + spareSize];

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            state = State.Idle;

            receiveStack.Clear();
            sendQueue.Clear();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Received byte: 0x{0:X} in state {1}", data, state);
            byte result = 0;

            switch(state)
            {
            case State.Idle:
                HandleCommand((Command)data);
                break;
            case State.Unsupported:
                break;
            case State.GetFeatureAddress:
            {
                var r = RegistersCollection.Read(data);
                sendQueue.Enqueue(r);
                this.Log(LogLevel.Noisy, "Reading register {0} = {1}", data, r);
                state = State.TransmitQueue;
                break;
            }
            case State.GetReadColumn:
                receiveStack.Push(data);
                result = sendQueue.Dequeue();
                if(sendQueue.Count == 0)
                {
                    column = RxDequeueAsUInt();
                    this.Log(LogLevel.Noisy, "Column set to {0}", column);
                    state = State.TransmitCache;
                    sendQueue.Enqueue(0);
                }
                break;
            case State.TransmitCache:
                sendQueue.Enqueue(pageCache[column++]);
                result = sendQueue.Dequeue();
                break;
            case State.SetFeatureAddress:
                receiveStack.Push(data);
                state = State.SetFeatureData;
                break;
            case State.SetFeatureData:
                if(receiveStack.Count == 0)
                {
                    this.Log(LogLevel.Error, "Trailing bytes on SetFeature command");
                }
                else
                {
                    var r = (long)receiveStack.Pop();
                    RegistersCollection.Write(r, data);
                    this.Log(LogLevel.Noisy, "Setting register {0} = {1}", (long)r, data);
                }
                break;
            case State.TransmitQueue:
                receiveStack.Push(data);
                if(sendQueue.Count == 0)
                {
                    this.Log(LogLevel.Warning, "Nothing left in send queue");
                }
                else
                {
                    result = sendQueue.Dequeue();
                }
                break;
            default:
                this.Log(LogLevel.Error, "Received byte 0x{0:X} in an unexpected state: {1}. Ignoring it...", data, state);
                break;
            }

            this.Log(LogLevel.Noisy, "Returning byte: 0x{0:X}", result);
            return result;
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Transmission finished");

            if(lastCommand == Command.PageRead)
            {
                if(receiveStack.Count != 3)
                {
                    this.Log(LogLevel.Error, "Incorrect byte count for PageRead {0}", receiveStack.Count);
                }
                else
                {
                    uint page = RxDequeueAsUInt();
                    this.Log(LogLevel.Noisy, "Loading page {0} from cache", page);
                    uint offset = page * pageSize;
                    dataMemory.ReadBytes(offset, (int)pageSize, pageCache, 0);
                    // TODO: Copy spareMemory to pageCache
                }
            }

            receiveStack.Clear();
            sendQueue.Clear();
            state = State.Idle;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            RegisterType.BlockLock.Define(this)
                .WithValueField(0, 8, name: "data")
            ;
            RegisterType.Config.Define(this)
                .WithValueField(0, 8, name: "config")
            ;
            RegisterType.Status.Define(this)
                .WithValueField(0, 8, name: "status")
            ;
            RegisterType.DieSelect.Define(this)
                .WithValueField(0, 8, name: "die_select")
            ;
        }

        private uint RxDequeueAsUInt()
        {
            uint output = 0;
            var count = Math.Min(sizeof(uint), receiveStack.Count);
            for(int i = 0; i < count; i++)
            {
                output |= (uint)(receiveStack.Pop() << (i * 8));
            }
            return output;
        }

        private void HandleCommand(Command command)
        {
            lastCommand = command;
            byte[] bytesToLoad = null;

            switch(command)
            {
            case Command.Reset:
                Reset();
                break;
            case Command.ReadId:
                bytesToLoad = ReadIdBytes;
                state = State.TransmitQueue;
                break;
            case Command.GetFeature:
                state = State.GetFeatureAddress;
                break;
            case Command.SetFeature:
                state = State.SetFeatureAddress;
                break;
            case Command.PageRead:
                bytesToLoad = PageReadBytes;
                state = State.TransmitQueue;
                break;
            case Command.ReadFromCache:
            case Command.FastReadFromCache:
                bytesToLoad = ReadFromCacheBytes;
                state = State.GetReadColumn;
                break;
            default:
                this.Log(LogLevel.Error, "Unsupported command 0x{0:X}", (byte)command);
                state = State.Unsupported;
                break;
            }

            if(bytesToLoad != null)
            {
                sendQueue.EnqueueRange(bytesToLoad);
            }
        }

        private readonly Queue<byte> sendQueue;
        private readonly Stack<byte> receiveStack;
        private readonly MappedMemory dataMemory;
        private readonly MappedMemory spareMemory;

        private readonly byte[] pageCache;
        private readonly uint pageSize;
        private readonly uint spareSize;
        private readonly uint pagesPerBlock;
        private readonly uint blocksPerLun;
        private readonly uint lunsPerChip;

        private State state;
        private Command lastCommand;

        private uint column;

        private static readonly byte[] ReadIdBytes = new byte[] { 0x0, 0xef, 0xbc };
        private static readonly byte[] PageReadBytes = new byte[] { 0x0, 0x0, 0x0 };
        private static readonly byte[] ReadFromCacheBytes = new byte[] { 0x0, 0x0 };

        private const byte EmptySegment = 0xff;

        private enum Command
        {
            Reset = 0xff,
            ReadId = 0x9f,
            GetFeature = 0x0f,
            SetFeature = 0x1f,
            PageRead = 0x13,
            ReadFromCache = 0x03,
            FastReadFromCache = 0x0b,
        }

        private enum State
        {
            Idle,
            TransmitQueue,
            TransmitCache,
            GetFeatureAddress,
            SetFeatureAddress,
            SetFeatureData,
            GetReadColumn,
            Unsupported
        }

        [RegisterMapper.RegistersDescription]
        private enum RegisterType
        {
            BlockLock = 0xa0,
            Config = 0xb0,
            Status = 0xc0,
            DieSelect = 0xd0
        }
    }
}
