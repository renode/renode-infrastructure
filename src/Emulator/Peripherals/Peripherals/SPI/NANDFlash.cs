//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.Memory;

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

            DefineRegisters();

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
        }

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

        public void Reset()
        {
            RegistersCollection.Reset();
            state = State.Idle;

            receiveStack.Clear();
            sendQueue.Clear();
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

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Transmission finished");

            switch (lastCmd)
            {
            case Command.PageRead:
                if (receiveStack.Count != 3)
                {
                    this.Log(LogLevel.Error, "Incorrect byte count for PageRead {0}", receiveStack.Count);
                }
                else
                {
                    uint page = RxDequeueAsUInt();
                    this.Log(LogLevel.Info, "Loading page {0} from cache", page);
                    uint offset = page * pageSize;
                    dataMemory.ReadBytes(offset, (int)pageSize, pageCache, 0);
                    // TODO: Copy spareMemory to pageCache
                }
                break;
            default:
                break;
            }
            receiveStack.Clear();
            sendQueue.Clear();
            state = State.Idle;
        }

        private void HandleCommandByte(byte data)
        {
            var cmd = (Command)data;
            lastCmd = cmd;

            switch (cmd)
            {
            case Command.Reset:
                Reset();
                break;
            case Command.ReadId:
                sendQueue.Enqueue((byte)0);
                sendQueue.Enqueue((byte)0xef);
                sendQueue.Enqueue((byte)0xbc);
                state = State.TransmitQueue;
                break;
            case Command.GetFeature:
                state = State.GetFeatureAddress;
                break;
            case Command.SetFeature:
                state = State.SetFeatureAddress;
                break;
            case Command.PageRead:
                sendQueue.Enqueue((byte)0);
                sendQueue.Enqueue((byte)0);
                sendQueue.Enqueue((byte)0);
                state = State.TransmitQueue;
                break;
            case Command.ReadFromCache:
            case Command.FastReadFromCache:
                sendQueue.Enqueue((byte)0);
                sendQueue.Enqueue((byte)0);
                state = State.GetReadColumn;
                break;
            default:
                this.Log(LogLevel.Error, "Unsupported command 0x{0:X}", data);
                state = State.Unsupported;
                break;
            }
        }

        public byte Transmit(byte b)
        {
            this.Log(LogLevel.Noisy, "Received byte: 0x{0:X} in state {1}", b, state);
            byte result = 0;

            switch(state)
            {
            case State.Idle:
                HandleCommandByte(b);
                break;
            case State.Unsupported:
                break;
            case State.GetFeatureAddress:
                {
                var r = RegistersCollection.Read((long)b);
                sendQueue.Enqueue(r);
                this.Log(LogLevel.Info, "Reading register {0} = {1}", (long)b, r);
                state = State.TransmitQueue;
                break;
                }
            case State.GetReadColumn:
                receiveStack.Push(b);
                result = sendQueue.Dequeue();
                if (sendQueue.Count == 0)
                {
                    column = RxDequeueAsUInt();
                    this.Log(LogLevel.Info, "Column set to {0}", column);
                    state = State.TransmitCache;
                    sendQueue.Enqueue(0);
                }
                break;
            case State.TransmitCache:
                sendQueue.Enqueue(pageCache[column++]);
                result = sendQueue.Dequeue();
                break;
            case State.SetFeatureAddress:
                receiveStack.Push(b);
                state = State.SetFeatureData;
                break;
            case State.SetFeatureData:
                if (receiveStack.Count == 0)
                {
                    this.Log(LogLevel.Error, "Trailing bytes on SetFeature command");
                }
                else
                {
                    var r = (long)receiveStack.Pop();
                    RegistersCollection.Write(r, b);
                    this.Log(LogLevel.Info, "Setting register {0} = {1}", (long)r, b);
                }
                break;
            case State.TransmitQueue:
                receiveStack.Push(b);
                if (sendQueue.Count == 0)
                    this.Log(LogLevel.Warning, "Nothing left in send queue");
                else
                    result = sendQueue.Dequeue();
                break;
            default:
                this.Log(LogLevel.Error, "Received byte 0x{0:X} in an unexpected state: {1}. Ignoring it...", b, state);
                break;
            }

            this.Log(LogLevel.Noisy, "Returning byte: 0x{0:X}", result);
            return result;
        }

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

        private State state;
        private readonly Stack<byte> receiveStack;
        private readonly Queue<byte> sendQueue;
        private Command lastCmd;

        private const byte EmptySegment = 0xff;
        private readonly MappedMemory dataMemory;
        private readonly MappedMemory spareMemory;

        private uint pageSize;
        private uint spareSize;
        private uint pagesPerBlock;
        private uint blocksPerLun;
        private uint lunsPerChip;

        private uint column = 0;
        private byte[] pageCache;

        public ByteRegisterCollection RegistersCollection { get; }
    }
}
