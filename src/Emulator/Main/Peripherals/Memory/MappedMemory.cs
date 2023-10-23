//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
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
using Antmicro.Renode.Peripherals.CPU;
#if PLATFORM_WINDOWS
using System.Reflection.Emit;
using System.Reflection;
#endif

namespace Antmicro.Renode.Peripherals.Memory
{
    [Icon("memory")]
    public sealed class MappedMemory : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IQuadWordPeripheral, IMapped, IDisposable, IKnownSize, ISpeciallySerializable, IMemory, IMultibyteWritePeripheral, ICanLoadFiles
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

        public MappedMemory(IMachine machine, long size, int? segmentSize = null)
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
            if(offset < 0 || offset >= size)
            {
                this.Log(LogLevel.Error, "Tried to read byte at offset 0x{0:X} outside the range of the peripheral 0x0 - 0x{1:X}", offset, size);
                return 0;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            return Marshal.ReadByte(new IntPtr(segment.ToInt64() + localOffset));
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset < 0 || offset >= size)
            {
                this.Log(LogLevel.Error, "Tried to write byte value 0x{0:X} to offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", value, offset, size);
                return;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            Marshal.WriteByte(new IntPtr(segment.ToInt64() + localOffset), value);
            InvalidateMemoryFragment(offset, 1);
        }

        public ushort ReadWord(long offset)
        {
            if(offset < 0 || offset > size - sizeof(ushort))
            {
                this.Log(LogLevel.Error, "Tried to read word at offset 0x{0:X} outside the range of the peripheral 0x0 - 0x{1:X}", offset, size);
                return 0;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize - sizeof(ushort)) // cross segment read
            {
                var bytes = new byte[2];
                bytes[0] = Marshal.ReadByte(new IntPtr(segment.ToInt64() + localOffset));
                var secondSegment = segments[GetSegmentNo(offset + 1)];
                bytes[1] = Marshal.ReadByte(secondSegment);
                return BitConverter.ToUInt16(bytes, 0);
            }
            return unchecked((ushort)Marshal.ReadInt16(new IntPtr(segment.ToInt64() + localOffset)));
        }

        public void WriteWord(long offset, ushort value)
        {
            if(offset < 0 || offset > size - sizeof(ushort))
            {
                this.Log(LogLevel.Error, "Tried to write word value 0x{0:X} to offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", value, offset, size);
                return;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize - sizeof(ushort)) // cross segment write
            {
                var bytes = BitConverter.GetBytes(value);
                Marshal.WriteByte(new IntPtr(segment.ToInt64() + localOffset), bytes[0]);
                var secondSegment = segments[GetSegmentNo(offset + 1)];
                Marshal.WriteByte(secondSegment, bytes[1]);
                InvalidateMemoryFragment(offset, 1);
                InvalidateMemoryFragment(offset + 1, 1);
            }
            else
            {
                Marshal.WriteInt16(new IntPtr(segment.ToInt64() + localOffset), unchecked((short)value));
                InvalidateMemoryFragment(offset, sizeof(ushort));
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset < 0 || offset > size - sizeof(uint))
            {
                this.Log(LogLevel.Error, "Tried to read double word at offset 0x{0:X} outside the range of the peripheral 0x0 - 0x{1:X}", offset, size);
                return 0;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize - sizeof(uint)) // cross segment read
            {
                var bytes = ReadBytes(offset, sizeof(uint));
                return BitConverter.ToUInt32(bytes, 0);
            }
            return unchecked((uint)Marshal.ReadInt32(new IntPtr(segment.ToInt64() + localOffset)));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset > size - sizeof(uint))
            {
                this.Log(LogLevel.Error, "Tried to write double word value 0x{0:X} to offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", value, offset, size);
                return;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize - sizeof(uint)) // cross segment write
            {
                var bytes = BitConverter.GetBytes(value);
                WriteBytes(offset, bytes);
                // the memory will be invalidated by `WriteBytes`
            }
            else
            {
                Marshal.WriteInt32(new IntPtr(segment.ToInt64() + localOffset), unchecked((int)value));
                InvalidateMemoryFragment(offset, sizeof(uint));
            }
        }

        public ulong ReadQuadWord(long offset)
        {
            if(offset < 0 || offset > size - sizeof(ulong))
            {
                this.Log(LogLevel.Error, "Tried to read quad word at offset 0x{0:X} outside the range of the peripheral 0x0 - 0x{1:X}", offset, size);
                return 0;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize  - sizeof(ulong)) // cross segment read
            {
                var bytes = ReadBytes(offset, sizeof(ulong));
                return BitConverter.ToUInt64(bytes, 0);
            }
            return unchecked((ulong)Marshal.ReadInt64(new IntPtr(segment.ToInt64() + localOffset)));
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            if(offset < 0 || offset > size - sizeof(ulong))
            {
                this.Log(LogLevel.Error, "Tried to write quad word value 0x{0:X} to offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", value, offset, size);
                return;
            }

            var localOffset = GetLocalOffset(offset);
            var segment = segments[GetSegmentNo(offset)];
            if(localOffset > SegmentSize - sizeof(ulong)) // cross segment write
            {
                var bytes = BitConverter.GetBytes(value);
                WriteBytes(offset, bytes);
                // the memory will be invalidated by `WriteBytes`
            }
            else
            {
                Marshal.WriteInt64(new IntPtr(segment.ToInt64() + localOffset), unchecked((long)value));
                InvalidateMemoryFragment(offset, sizeof(ulong));
            }
        }

        public void ReadBytes(long offset, int count, byte[] destination, int startIndex)
        {
            if(offset < 0 || offset > size - count)
            {
                this.Log(LogLevel.Error, "Tried to read {0} bytes at offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", count, offset, size);
                return;
            }

            var read = 0;
            while(read < count)
            {
                var currentOffset = offset + read;
                var localOffset = GetLocalOffset(currentOffset);
                var segment = segments[GetSegmentNo(currentOffset)];
                var length = Math.Min(count - read, (int)(SegmentSize - localOffset));
                Marshal.Copy(new IntPtr(segment.ToInt64() + localOffset), destination, read + startIndex, length);
                read += length;
            }
        }

        public byte[] ReadBytes(long offset, int count, ICPU context = null)
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

        public void WriteBytes(long offset, byte[] array, int startingIndex, int count, ICPU context = null)
        {
            if(offset < 0 || offset > size - count)
            {
                this.Log(LogLevel.Error, "Tried to write {0} bytes at offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", count, offset, size);
                return;
            }

            var written = 0;
            while(written < count)
            {
                var currentOffset = offset + written;
                var localOffset = GetLocalOffset(currentOffset);
                var segment = segments[GetSegmentNo(currentOffset)];
                var length = Math.Min(count - written, (int)(SegmentSize - localOffset));
                Marshal.Copy(array, startingIndex + written, new IntPtr(segment.ToInt64() + localOffset), length);
                written += length;

                InvalidateMemoryFragment(currentOffset, length);
            }
        }

        public void WriteString(long offset, string value)
        {
            WriteBytes(offset, new System.Text.ASCIIEncoding().GetBytes(value).Concat(new []{ (byte)'\0' }).ToArray());
        }

        public void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu)
        {
            this.LoadFileChunks(chunks, cpu);
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

        public void ZeroRange(long rangeStart, long rangeLength)
        {
            var array = new byte[rangeLength];
            for(long i = 0; i < rangeLength; ++i)
            {
                array[i] = ResetByte;
            }
            WriteBytes(rangeStart, array);
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
            ResetByte = reader.ReadByte();
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

            writer.Write(Magic);
            writer.Write(SegmentSize);
            writer.Write(size);
            writer.Write(ResetByte);
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

        private long GetLocalOffset(long offset)
        {
            return (offset % SegmentSize);
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
            var sizeOfLast = (uint)(size % SegmentSize);
            if(sizeOfLast == 0)
            {
                sizeOfLast = (uint)SegmentSize;
            }
            describedSegments[last] = new MappedSegment(this, last, sizeOfLast);
        }

        private int GetSegmentNo(long offset)
        {
            var segmentNo = (int)(offset / SegmentSize);
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

        private void InvalidateMemoryFragment(long start, int length)
        {
            if(machine == null)
            {
                // this peripheral is not connected to any machine, so there is nothing we can do
                return;
            }

            this.NoisyLog("Invalidating memory fragment at 0x{0:X} of size {1} bytes.", start, length);

            var registrationPoints = GetRegistrationPoints();
            foreach(var cpu in machine.SystemBus.GetCPUs().OfType<CPU.ICPU>())
            {
                foreach(var regPoint in registrationPoints)
                {
                    try
                    {
                        //it's dynamic to avoid cyclic dependency to TranslationCPU
                        ((dynamic)cpu).InvalidateTranslationBlocks(new IntPtr(regPoint + start), new IntPtr(regPoint + start + length));
                    }
                    catch(RuntimeBinderException)
                    {
                        // CPU does not implement `InvalidateTranslationBlocks`, there is not much we can do
                    }
                }
            }
        }

        private List<long> GetRegistrationPoints()
        {
            if(registrationPointsCached == null)
            {
                registrationPointsCached = machine.SystemBus.GetRegistrationPoints(this).Select(x => (long)(x.Range.StartAddress + x.Offset)).ToList();
            }
            return registrationPointsCached;
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
        private List<long> registrationPointsCached;
        private readonly IMachine machine;

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

