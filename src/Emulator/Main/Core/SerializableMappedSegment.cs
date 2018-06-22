//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;

namespace Antmicro.Renode.Core
{
    public sealed class SerializableMappedSegment : IMappedSegment, IDisposable
    {
        public SerializableMappedSegment(ulong size, ulong startingOffset)
        {
            Size = size;
            StartingOffset = startingOffset;
            MakeSegment();
        }

        public IntPtr Pointer { get { return pointer; } }

        public ulong StartingOffset { get; private set; }

        public ulong Size { get; private set; }

        public void Touch()
        {
            if(pointer != IntPtr.Zero)
            {
                return;
            }
            var sizeAsInt = checked((int)Size);
            pointer = Marshal.AllocHGlobal(sizeAsInt);
            var zeroBuf = new byte[sizeAsInt];
            Marshal.Copy(zeroBuf, 0, pointer, sizeAsInt);
        }

        public void Dispose()
        {
            var oldPointer = Interlocked.Exchange(ref pointer, IntPtr.Zero);
            if(oldPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(oldPointer);
            }
        }

        [PreSerialization]
        private void PrepareBuffer()
        {
            if(pointer == IntPtr.Zero)
            {
                return;
            }
            var sizeAsInt = checked((int)Size);
            buffer = new byte[sizeAsInt];
            Marshal.Copy(pointer, buffer, 0, sizeAsInt);
        }

        [PostSerialization]
        private void DisposeBuffer()
        {
            buffer = null;
        }

        [PostDeserialization]
        private void MakeSegment()
        {
            if(pointer != IntPtr.Zero)
            {
                throw new InvalidOperationException("Unexpected non null pointer during initialization.");
            }
            if(buffer != null)
            {
                Touch();
                Marshal.Copy(buffer, 0, pointer, checked((int)Size));
                buffer = null;
            }
        }

        [Transient]
        private IntPtr pointer;
        private byte[] buffer;
    }
}

