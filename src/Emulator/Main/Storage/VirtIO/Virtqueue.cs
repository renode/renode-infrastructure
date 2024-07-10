//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Storage.VirtIO
{
    // Implementation of split virtqueue
    // Source: https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.html#x1-350007
    public class Virtqueue
    {
        public Virtqueue(VirtIO parent, uint maxSize)
        {
            this.parent = parent;
            this.maxSize = maxSize;
        }

        public void Reset()
        {
            DescTableAddress = 0;
            AvailableAddress = 0;
            UsedAddress = 0;
            AvailableIndex = 0;
            AvailableIndexFromDriver = 0;
            UsedIndex = 0;
            UsedIndexForDriver = 0;
            IsReady = false;
            IsReset = false;
        }

        public bool TryReadFromBuffers(int len, out byte[] data)
        {
            var readLen = 0;
            data = new byte[len];
            while(readLen < len)
            {
                ReadDescriptorMetadata();
                var toRead = (int)Math.Min(Descriptor.Length, len - readLen);
                parent.Log(LogLevel.Debug, "Reading data from buffer at addr {0}, toRead {1}, length {2}, len {3}", Descriptor.BufferAddress, toRead, Descriptor.Length, len);

                var readData = parent.SystemBus.ReadBytes(Descriptor.BufferAddress, toRead);
                parent.Log(LogLevel.Debug, "Read data: {0}", Misc.PrettyPrintCollection(readData));

                Array.Copy(readData, 0, data, (int)readLen, toRead);
                readLen += toRead;

                var nextFlag = TrySetNextIndex();
                parent.Log(LogLevel.Debug, "Next flag: {0}", nextFlag);
                if(!nextFlag)
                {
                    return false;
                }
                if(readLen < len)
                {
                    parent.Log(LogLevel.Debug, "Reading next buffer");
                }
            }
            return true;
        }

        public bool TryWriteToBuffers(byte[] data)
        {
            var writtenLen = 0;
            var dataLen = data.Length;
            while(writtenLen < dataLen)
            {
                ReadDescriptorMetadata();
                if(!CanSafelyWriteToBuffer())
                {
                    return false;
                }
                var toWrite = Math.Min(data.Length, Descriptor.Length);
                parent.SystemBus.WriteBytes(data, Descriptor.BufferAddress, (long)toWrite);

                writtenLen += toWrite;
                var dataLeft = new byte[data.Length - toWrite];
                Array.Copy(data, toWrite, dataLeft, 0, dataLeft.Length);
                data = dataLeft;

                BytesWritten += Descriptor.Length;
                parent.Log(LogLevel.Debug, "Wrote {0} bytes", toWrite);
                if(!TrySetNextIndex())
                {
                    break;
                }
                if(writtenLen < dataLen)
                {
                    parent.Log(LogLevel.Debug, "Continuing writing in next buffer...");
                }
            }
            return true;
        }

        public void Handle()
        {
            var idx = (ushort)parent.SystemBus.ReadWord(AvailableAddress + (ulong)UsedAndAvailable.Index);
            // Processing all available requests
            // We're using 2 variables: availableIndex, availableIndexFromDriver
            // because we have to compare this value to index field in driver's
            // struct for available descriptors.
            // This field is meant to start at 0, then only increase and wrap around 65535.
            // That's why we have one variable for comparing and the other one for accessing tables.
            // Source: https://docs.oasis-open.org/virtio/virtio/v1.1/csprd01/virtio-v1.1-csprd01.html#x1-5300013
            while(AvailableIndexFromDriver < idx)
            {
                BytesWritten = 0;
                var descriptor = ReadDescriptorFromAvail();
                DescriptorIndex = descriptor.Item1;

                if(!parent.ProcessChain(this))
                {
                    parent.Log(LogLevel.Error, "Error processing virtqueue requests");
                    BytesWritten = 0;
                    return;
                }
                WriteVirtqueueUsed(descriptor.Item1, descriptor.Item2);
                AvailableIndex = (AvailableIndex + 1u) % Size;
                AvailableIndexFromDriver++;
            }
        }

        // Write processed chain to used descriptors table.
        // usedIndex and usedIndexForDriver work analogically
        // to availableIndex and availableIndexFromDevice.
        public void WriteVirtqueueUsed(int chainFirstIndex, bool noInterruptOnUsed)
        {
            var ringAddress = UsedAddress + (ulong)UsedAndAvailable.Ring
                    + UsedRingEntrySize * ((ulong)UsedIndex);
            UsedIndex = (ushort)((UsedIndex + 1u) % Size);
            UsedIndexForDriver++;
            parent.SystemBus.WriteWord(UsedAddress + (ulong)UsedAndAvailable.Flags, 0);
            parent.SystemBus.WriteDoubleWord(ringAddress + (ulong)UsedRing.Index, (uint)chainFirstIndex);
            parent.SystemBus.WriteDoubleWord(ringAddress + (ulong)UsedRing.Length, (uint)BytesWritten);
            parent.SystemBus.WriteWord(UsedAddress + (ulong)UsedAndAvailable.Index, (ushort)UsedIndexForDriver);
            if(!noInterruptOnUsed)
            {
                parent.InterruptUsedBuffer();
            }
        }

        public bool TrySetNextIndex()
        {
            if((Descriptor.Flags & (ushort)DescriptorFlags.Next) != 0)
            {
                DescriptorIndex = Descriptor.Next;
                return true;
            }
            return false;
        }

        public void ReadDescriptorMetadata()
        {
            parent.Log(LogLevel.Debug, "Reading desc meta, queueSel: {0}, descIndex: {1}", parent.QueueSel, DescriptorIndex);
            var descriptorAddress = DescTableAddress + DescriptorSize * (ulong)DescriptorIndex;
            var scanBytes = parent.SystemBus.ReadBytes(descriptorAddress, DescriptorSizeOffset);
            Descriptor = Packet.Decode<DescriptorMetadata>(scanBytes);
            parent.Log(LogLevel.Debug, "Processing buffer of addr: {0}, next: {1}, length: {2}, flags: {3}", Descriptor.BufferAddress, Descriptor.Next, Descriptor.Length, Descriptor.Flags);
        }

        public ulong Size { get; set; }
        /// Guest physical address of the descriptor table.
        public ulong DescTableAddress { get; set; }
        /// Guest physical address of the available ring.
        public ulong AvailableAddress { get; set; }
        /// Guest physical address of the used ring.
        public ulong UsedAddress { get; set; }
        public int DescriptorIndex { get; set; }
        public ulong AvailableIndex { get; set; }
        public ulong AvailableIndexFromDriver { get; set; }
        public ulong UsedIndex { get; set; }
        public ulong UsedIndexForDriver { get; set; }
        public bool IsReady { get; set; }
        public bool IsReset { get; set; }
        public DescriptorMetadata Descriptor { get; set; }
        public int BytesWritten { get; set; }

        public readonly uint maxSize;

        public const uint AvailableRingEntrySize = 0x2;
        public const uint UsedRingEntrySize = 0x8;
        public const uint DescriptorSize = 0x10;
        public const uint QueueMaxSize = 1 << 15;
        public const uint MaxBufferSize = 1 << 20;

        private bool CanSafelyWriteToBuffer()
        {
            if((Descriptor.Flags & (ushort)DescriptorFlags.Write) == 0)
            {
                parent.Log(LogLevel.Error, "IO Error: Trying to write to device-read buffer. Descriptor info: index: {0}, Address: {1}, Flags: {2}", DescriptorIndex, Descriptor.BufferAddress, Descriptor.Flags);
                return false;
            }
            return true;
        }

        // Reads descriptor entry index and interrupt flag from available ring
        private Tuple<int, bool> ReadDescriptorFromAvail()
        {
            var flag = parent.SystemBus.ReadWord(AvailableAddress + (ulong)UsedAndAvailable.Flags);
            var noInterruptOnUsed = (flag == (ushort)UsedAndAvailableFlags.NoNotify);
            var chainFirstIndex = (int)parent.SystemBus.ReadWord(AvailableAddress +
                (ulong)UsedAndAvailable.Ring + AvailableRingEntrySize * (ulong)AvailableIndex);
            DescriptorIndex = chainFirstIndex;
            parent.Log(LogLevel.Debug, "Chain starting at index {0}", chainFirstIndex);
            return Tuple.Create(chainFirstIndex, noInterruptOnUsed);
        }

        private readonly VirtIO parent;
        private const int DescriptorSizeOffset = 0x12;

        // Used and Available have the same structure. The main difference is type of elements used in ring arrays.
        // In Available it's only a 16bit number. In Used it's a structure described in UsedRing enum.
        public enum UsedAndAvailable
        {
            Flags = 0x0,
            Index = 0x2,
            Ring = 0x4,
        }

        public enum UsedRing
        {
            Index = 0x0,
            Length = 0x4,
        }

        [Flags]
        public enum DescriptorFlags: ushort
        {
            Next = 1 << 0,
            Write = 1 << 1,
            Indirect = 1 << 2,
        }

        [Flags]
        public enum UsedAndAvailableFlags: ushort
        {
            NoNotify = 1 << 0,
        }

        [LeastSignificantByteFirst]
        public struct DescriptorMetadata
        {
            [PacketField, Width(64)]
            public ulong BufferAddress;
            [PacketField, Offset(doubleWords: 2), Width(32)]
            public int Length;
            [PacketField, Offset(doubleWords: 3), Width(16)]
            public ushort Flags;
            [PacketField, Offset(doubleWords: 3, bits: 16), Width(16)]
            public ushort Next;
        }
    }
}
