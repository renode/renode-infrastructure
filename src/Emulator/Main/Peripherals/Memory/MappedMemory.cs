//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using System.Threading.Tasks;
using System.Threading;
using LZ4;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Core;
#if PLATFORM_WINDOWS
using System.Reflection.Emit;
using System.Reflection;
#endif

namespace Antmicro.Renode.Peripherals.Memory
{
    [Icon("memory")]
    public sealed class MappedMemory : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IMapped, IDisposable, IKnownSize, ISpeciallySerializable, IMemory, IMultibyteWritePeripheral
    {
#if PLATFORM_WINDOWS
        static MappedMemory()
        {
            var dynamicMethod = new DynamicMethod("Memset", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
                null, new [] { typeof(IntPtr), typeof(byte), typeof(int) }, typeof(MappedMemory), true);

            var generator = dynamicMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Initblk);
            generator.Emit(OpCodes.Ret);

            MemsetDelegate = (Action<IntPtr, byte, int>)dynamicMethod.CreateDelegate(typeof(Action<IntPtr, byte, int>));
        }
#endif

        public MappedMemory(Machine machine, long size, int? segmentSize = null)
        {
            if(segmentSize == null)
            {
                var proposedSegmentSize = Math.Min(MaximalSegmentSize, Math.Max(MinimalSegmentSize, size / RecommendedNumberOfSegments));
                // align it
                segmentSize = (int)(Math.Ceiling(1.0 * proposedSegmentSize / MinimalSegmentSize) * MinimalSegmentSize);
                this.DebugLog("Segment size automatically calculated to value {0}B", Misc.NormalizeBinary(segmentSize.Value));
            }
            this.machine = machine;
            this.size = size;
            SegmentSize = segmentSize.Value;
            Init();
        }

        public event Action<int> SegmentTouched;

        public int SegmentCount
        {
            get
            {
                return segments.Length;
            }
        }

        public int SegmentSize { get; private set; }

        public IEnumerable<IMappedSegment> MappedSegments
        {
            get
            {
                return describedSegments;
            }
        }

        public byte ReadByte(long offset)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            return Marshal.ReadByte(segment + localOffset);
        }

        public void WriteByte(long offset, byte value)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            Marshal.WriteByte(segment + localOffset, value);
            InvalidateMemoryFragment(segment + localOffset, 1);
        }

        public ushort ReadWord(long offset)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            if(localOffset == SegmentSize - 1) // cross segment read
            {
                var bytes = new byte[2];
                bytes[0] = Marshal.ReadByte(segment + localOffset);
                var secondSegment = segments[GetSegmentNo((uint)offset + 1)];
                bytes[1] = Marshal.ReadByte(secondSegment);
                return BitConverter.ToUInt16(bytes, 0);
            }
            return unchecked((ushort)Marshal.ReadInt16(segment + localOffset));
        }

        public void WriteWord(long offset, ushort value)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            if(localOffset == SegmentSize - 1) // cross segment write
            {
                var bytes = BitConverter.GetBytes(value);
                Marshal.WriteByte(segment + localOffset, bytes[0]);
                var secondSegment = segments[GetSegmentNo((uint)(offset + 1))];
                Marshal.WriteByte(secondSegment, bytes[1]);
                InvalidateMemoryFragment(segment + localOffset, 1);
                InvalidateMemoryFragment(secondSegment, 1);
            }
            else
            {
                Marshal.WriteInt16(segment + localOffset, unchecked((short)value));
                InvalidateMemoryFragment(segment + localOffset, 2);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            if(localOffset >= SegmentSize - 3) // cross segment read
            {
                var bytes = ReadBytes(offset, 4);
                return BitConverter.ToUInt32(bytes, 0);
            }
            return unchecked((uint)Marshal.ReadInt32(segment + localOffset));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            var localOffset = GetLocalOffset((uint)offset);
            var segment = segments[GetSegmentNo((uint)offset)];
            if(localOffset >= SegmentSize - 3) // cross segment write
            {
                var bytes = BitConverter.GetBytes(value);
                WriteBytes(offset, bytes);
                // the memory will be invalidated by `WriteBytes`
            }
            else
            {
                Marshal.WriteInt32(segment + localOffset, unchecked((int)value));
                InvalidateMemoryFragment(segment + localOffset, 4);
            }
        }

        public void ReadBytes(long offset, int count, byte[] destination, int startIndex)
        {
            var read = 0;
            while(read < count)
            {
                var currentOffset = offset + read;
                var localOffset = GetLocalOffset((uint)currentOffset);
                var segment = segments[GetSegmentNo((uint)currentOffset)];
                var length = Math.Min(count - read, SegmentSize - localOffset);
                Marshal.Copy(segment + localOffset, destination, read + startIndex, length);
                read += length;
            }
        }

        public byte[] ReadBytes(long offset, int count)
        {
            var result = new byte[count];
            ReadBytes(offset, count, result, 0);
            return result;
        }

        public void WriteBytes(long offset, byte[] value)
        {
            WriteBytes(offset, value, 0, value.Length);
        }

        public void WriteBytes(long offset, byte[] value, int count)
        {
            WriteBytes(offset, value, 0, count);
        }

        public void WriteBytes(long offset, byte[] array, int startingIndex, int count)
        {
            var written = 0;
            while(written < count)
            {
                var currentOffset = offset + written;
                var localOffset = GetLocalOffset((uint)currentOffset);
                var segment = segments[GetSegmentNo((uint)currentOffset)];
                var length = Math.Min(count - written, SegmentSize - localOffset);
                Marshal.Copy(array, startingIndex + written, segment + localOffset, length);
                InvalidateMemoryFragment(segment + localOffset, length);
                written += length;
            }
        }

        public void WriteString(long offset, string value)
        {
            WriteBytes(offset, new System.Text.ASCIIEncoding().GetBytes(value).Concat(new []{ (byte)'\0' }).ToArray());
        }

        public void Reset()
        {
            // nothing happens with memory
            // we do not reset segments (as we do in init), since we do in init only
            // to have deterministic behaviour (i.e. given script executed two times will
            // give the same results; not zeroing during reset will however is not necessary
            // (starting values are not random anyway)
        }

        public IntPtr GetSegment(int segmentNo)
        {
            if(segmentNo < 0 || segmentNo > segments.Length)
            {
                throw new ArgumentOutOfRangeException("segmentNo");
            }
            return segments[segmentNo];
        }

        public void TouchAllSegments()
        {
            for(var i = 0; i < segments.Length; i++)
            {
                TouchSegment(i);
            }
        }

        public bool IsTouched(int segmentNo)
        {
            CheckSegmentNo(segmentNo);
            return segments[segmentNo] != IntPtr.Zero;
        }

        public void TouchSegment(int segmentNo)
        {
            CheckSegmentNo(segmentNo);
            if(segments[segmentNo] == IntPtr.Zero)
            {
                var allocSeg = AllocateSegment();
                var originalPointer = (long)allocSeg;
                var alignedPointer = (IntPtr)((originalPointer + Alignment) & ~(Alignment - 1));
                segments[segmentNo] = alignedPointer;
                this.NoisyLog(string.Format("Segment no {1} allocated at 0x{0:X} (aligned to 0x{2:X}).",
                    allocSeg.ToInt64(), segmentNo, alignedPointer.ToInt64()));
                originalPointers[segmentNo] = allocSeg;
                MemSet(alignedPointer, ResetByte, SegmentSize);
                var segmentTouched = SegmentTouched;
                if(segmentTouched != null)
                {
                    segmentTouched(segmentNo);
                }
            }
        }

        public byte ResetByte { get; set; }

        public long Size
        {
            get
            {
                return size;
            }
        }

        public void InitWithRandomData()
        {
            var rand = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            var buf = new byte[SegmentSize];

            for(var i = 0; i < segments.Length; ++i)
            {
                rand.NextBytes(buf);
                WriteBytes(i * SegmentSize, buf);
            }
        }

        public void ZeroAll()
        {
            foreach(var segment in segments.Where(x => x != IntPtr.Zero))
            {
                MemSet(segment, ResetByte, SegmentSize);
            }
        }

        public void Dispose()
        {
            Free();
            GC.SuppressFinalize(this);
        }

        public void Load(PrimitiveReader reader)
        {
            // checking magic
            var magic = reader.ReadUInt32();
            if(magic != Magic)
            {
                throw new InvalidOperationException("Memory: Cannot resume state from stream: Invalid magic.");
            }
            SegmentSize = reader.ReadInt32();
            size = reader.ReadInt64();
            if(emptyCtorUsed)
            {
                Init();
            }
            var realSegmentsCount = 0;
            for(var i = 0; i < segments.Length; i++)
            {
                var isTouched = reader.ReadBoolean();
                if(!isTouched)
                {
                    continue;
                }
                var compressedSegmentSize = reader.ReadInt32();
                var compressedBuffer = reader.ReadBytes(compressedSegmentSize);
                TouchSegment(i);
                realSegmentsCount++;
                var decodedBuffer = LZ4Codec.Decode(compressedBuffer, 0, compressedBuffer.Length, SegmentSize);
                Marshal.Copy(decodedBuffer, 0, segments[i], decodedBuffer.Length);
            }
            this.NoisyLog(string.Format("{0} segments loaded from stream, of which {1} had content.", segments.Length, realSegmentsCount));
        }

        public void Save(PrimitiveWriter writer)
        {
            var globalStopwatch = Stopwatch.StartNew();
            var realSegmentsCount = 0;
            // magic
            writer.Write(Magic);
            // saving size of the memory segment
            writer.Write(SegmentSize);
            // saving size of the memory
            writer.Write(size);
            byte[][] outputBuffers = new byte[segments.Length][];
            Parallel.For(0, segments.Length, i =>
            {
                if(segments[i] == IntPtr.Zero)
                {
                    return;
                }
                Interlocked.Increment(ref realSegmentsCount);
                var localBuffer = new byte[SegmentSize];
                Marshal.Copy(segments[i], localBuffer, 0, localBuffer.Length);
                outputBuffers[i] = LZ4Codec.Encode(localBuffer, 0, localBuffer.Length);
            });
            for(var i = 0; i < segments.Length; i++)
            {
                if(segments[i] == IntPtr.Zero)
                {
                    writer.Write(false);
                    continue;
                }
                writer.Write(true);
                writer.Write(outputBuffers[i].Length);
                writer.Write(outputBuffers[i], 0, outputBuffers[i].Length);
            }
            this.NoisyLog(string.Format("{0} segments saved to stream, of which {1} had contents.", segments.Length, realSegmentsCount));
            globalStopwatch.Stop();
            this.NoisyLog("Memory serialization ended in {0}s.", Misc.NormalizeDecimal(globalStopwatch.Elapsed.TotalSeconds));
        }

        /// <summary>
        /// This constructor is only to be used with serialization. Deserializer has to invoke Load method after such
        /// construction.
        /// </summary>
        private MappedMemory()
        {
            emptyCtorUsed = true;
        }

        private void CheckAlignment(IntPtr segment)
        {
            if((segment.ToInt64() & 7) != 0)
            {
                throw new ArgumentException(string.Format("Segment address has to be aligned to 8 bytes, but it is 0x{0:X}.", segment));
            }
        }

        private void Init()
        {
            PrepareSegments();
        }

        private void Free()
        {
            if(!disposed)
            {
                for(var i = 0; i < segments.Length; i++)
                {
                    var segment = originalPointers[i];
                    if(segments[i] != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(segment);
                        this.NoisyLog("Segment {0} freed.", i);
                    }
                }
            }
            disposed = true;
        }

        private int GetLocalOffset(uint offset)
        {
            return (int)(offset % (uint)SegmentSize);
        }

        void CheckSegmentNo(int segmentNo)
        {
            if(segmentNo < 0 || segmentNo >= SegmentCount)
            {
                throw new ArgumentOutOfRangeException("segmentNo");
            }
        }

        private void PrepareSegments()
        {
            if(segments != null)
            {
                // this is because in case of loading the starting memory snapshot
                // after deserialization (i.e. resetting after deserialization)
                // memory segments would have been lost
                return;
            }
            // how many segments we need?
            var segmentsNo = size / SegmentSize + (size % SegmentSize != 0 ? 1 : 0);
            this.NoisyLog(string.Format("Preparing {0} segments for {1} bytes of memory, each {2} bytes long.",
                segmentsNo, size, SegmentSize));
            segments = new IntPtr[segmentsNo];
            originalPointers = new IntPtr[segmentsNo];
            // segments are not allocated until they are used by read, write, load etc (or touched)
            describedSegments = new IMappedSegment[segmentsNo];
            for(var i = 0; i < describedSegments.Length - 1; i++)
            {
                describedSegments[i] = new MappedSegment(this, i, (uint)SegmentSize);
            }
            var last = describedSegments.Length - 1;
            var sizeOfLast = (uint)(size % (uint)SegmentSize);
            if(sizeOfLast == 0)
            {
                sizeOfLast = (uint)SegmentSize;
            }
            describedSegments[last] = new MappedSegment(this, last, sizeOfLast);
        }

        private int GetSegmentNo(uint offset)
        {
            var segmentNo = (int)(offset / (uint)SegmentSize);
#if DEBUG
            // check bounds
            if(segmentNo >= segments.Length || segmentNo < 0)
            {
                throw new IndexOutOfRangeException(string.Format(
                    "Memory: Attemption to use segment number {0}, which does not exist. Total number of segments is {1}.",
                    segmentNo,
                    segments.Length
                ));
            }
#endif
            // if such segment is not currently allocated,
            // allocate it
            TouchSegment(segmentNo);
            return segmentNo;
        }

        private IntPtr AllocateSegment()
        {
            this.NoisyLog("Allocating segment of size {0}.", SegmentSize);
            return Marshal.AllocHGlobal(SegmentSize + Alignment);
        }

        private void InvalidateMemoryFragment(IntPtr start, int length)
        {
            if(machine == null)
            {
                // this peripheral is not connected to any machine, so there is nothing we can do
                return;
            }

            this.NoisyLog("Invalidating memory fragment at 0x{0:X} of size {1} bytes.", start, SegmentSize);

            var otherCpus = machine.SystemBus.GetCPUs().OfType<CPU.ICPU>();
            foreach(var cpu in otherCpus)
            {
                //it's dynamic to avoid cyclic dependency to TranslationCPU
                ((dynamic)cpu).InvalidateTranslationBlocks(start, start + length);
            }
        }

#if PLATFORM_WINDOWS
        private static void MemSet(IntPtr pointer, byte value, int length)
        {
            MemsetDelegate(pointer, value, length);
        }

        private static Action<IntPtr, byte, int> MemsetDelegate;
#else
        [DllImport("libc", EntryPoint = "memset")]
        private static extern IntPtr MemSet(IntPtr pointer, byte value, int length);
#endif

        private readonly bool emptyCtorUsed;
        private IntPtr[] segments;
        private IntPtr[] originalPointers;
        private IMappedSegment[] describedSegments;
        private bool disposed;
        private long size;
        private readonly Machine machine;

        private const uint Magic = 0xABCD6366;
        private const int Alignment = 0x1000;
        private const int MinimalSegmentSize = 64 * 1024;
        private const int MaximalSegmentSize = 16 * 1024 * 1024;
        private const int RecommendedNumberOfSegments = 16;

        private class MappedSegment : IMappedSegment
        {
            public IntPtr Pointer
            {
                get
                {
                    return parent.GetSegment(index);
                }
            }

            public ulong Size
            {
                get
                {
                    return size;
                }
            }

            public ulong StartingOffset
            {
                get
                {
                    return checked((ulong)index * (ulong)parent.SegmentSize);
                }
            }

            public MappedSegment(MappedMemory parent, int index, uint size)
            {
                this.index = index;
                this.parent = parent;
                this.size = size;
            }

            public void Touch()
            {
                parent.TouchSegment(index);
            }

            public override string ToString()
            {
                return string.Format("[MappedSegment: Size=0x{0:X}, StartingOffset=0x{1:X}]", Size, StartingOffset);
            }

            private readonly MappedMemory parent;
            private readonly int index;
            private readonly uint size;
        }
    }
}

