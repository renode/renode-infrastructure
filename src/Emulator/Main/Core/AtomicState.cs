//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    public class AtomicState : IDisposable
    {
        public AtomicState(IEmulationElement parent, int? storeTableBits = null)
        {
            this.parent = parent;
            StoreTableBits = storeTableBits ?? ConfigurationManager.Instance.Get("general", "store-table-bits", AtomicState.DefaultStoreTableBits);
            InitAtomicMemoryState();
            InitHstState();
        }

        [PostDeserialization]
        public void InitAtomicMemoryState()
        {
            atomicMemoryStatePointer = Marshal.AllocHGlobal(AtomicMemoryStateSize);

            // the beginning of an atomic memory state contains two 8-bit flags:
            // byte 0: information if the mutex has already been initialized
            // byte 1: information if the reservations array has already been initialized
            //
            // the first byte must be set to 0 at start and after each deserialization
            // as this is crucial for proper memory initialization;
            //
            // the second one must be set to 0 at start, but should not be overwritten after deserialization;
            // this is handled when saving `atomicMemoryState`
            if(atomicMemoryState != null)
            {
                Marshal.Copy(atomicMemoryState, 0, atomicMemoryStatePointer, atomicMemoryState.Length);
                atomicMemoryState = null;
            }
            else
            {
                // this write spans two 8-byte flags
                Marshal.WriteInt16(atomicMemoryStatePointer, 0);
            }
        }

        [PostDeserialization]
        public void InitHstState()
        {
            parent.DebugLog("Initializing store table with size {0}...", StoreTableSize);
            // Table must be naturally aligned in order to efficiently calculate the address of its elements.
#if NET
            unsafe
            {
                storeTablePointer =
                    (IntPtr)NativeMemory.AlignedAlloc((UIntPtr)StoreTableSize, (UIntPtr)StoreTableSize);
            }
#else
            // On Mono/.NET Framework NativeMemory is not available, so the allocation
            // needs to be aligned manually:
            unalignedStoreTablePointer = Marshal.AllocHGlobal(2 * StoreTableSize);

            storeTablePointer = (IntPtr)(((long)unalignedStoreTablePointer + StoreTableSize)
                                         & ~(StoreTableSize - 1));
#endif
            parent.DebugLog("Store table allocated at 0x{0:X}", storeTablePointer);

            if(storeTable != null)
            {
                // Restore the serialized state
                Marshal.Copy(storeTable, 0, StoreTablePointer, StoreTableSize);
                storeTable = null;
                parent.DebugLog("Store table deserialized");
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(AtomicMemoryStatePointer);

#if NET
            unsafe
            {
                NativeMemory.AlignedFree((void*)storeTablePointer);
            }
#else
            Marshal.FreeHGlobal(unalignedStoreTablePointer);
#endif
        }

        public IntPtr AtomicMemoryStatePointer => atomicMemoryStatePointer;

        public IntPtr StoreTablePointer => storeTablePointer;
        /// <summary>
        ///  Gets or sets the number of bits used to uniquely address the store table in memory.
        /// </summary>
        public int StoreTableBits
        {
            get => storeTableBits;
            private set
            {
                if(value > IntPtr.Size * 8 || value <= 0)
                {
                    throw new RecoverableException(
                        $"store table bits must be between 0 and the host pointer size ({IntPtr.Size * 8})");
                }

                storeTableBits = value;
            }
        }

        public const int DefaultStoreTableBits = 41; // 64 - 41 = 23, 2^23 = 8388608 bytes = 8 MiB

        [PreSerialization]
        private void SerializeAtomicMemoryState()
        {
            atomicMemoryState = new byte[AtomicMemoryStateSize];
            Marshal.Copy(atomicMemoryStatePointer, atomicMemoryState, 0, atomicMemoryState.Length);
            // the first byte of an atomic memory state contains value 0 or 1
            // indicating if the mutex has already been initialized;
            // the mutex must be restored after each deserialization, so here we force this value to 0
            atomicMemoryState[0] = 0;
        }

        [PreSerialization]
        private void SerializeHstState()
        {
            parent.DebugLog("Serializing store table of size {0}...", StoreTableSize);
            storeTable = new byte[StoreTableSize];
            Marshal.Copy(StoreTablePointer, storeTable, 0, StoreTableSize);
        }

        private int StoreTableSize => 1 << (IntPtr.Size * 8 - StoreTableBits); // In bytes

        [Transient]
        private IntPtr atomicMemoryStatePointer;
        private byte[] atomicMemoryState;

        [Transient]
        private IntPtr storeTablePointer;
#if !NET
        [Transient]
        private IntPtr unalignedStoreTablePointer;
#endif
        private byte[] storeTable;
        private int storeTableBits;

        private readonly IEmulationElement parent;

        // TODO: this probably should be dynamically get from Tlib, but how to nicely do that from here?
        private const int AtomicMemoryStateSize = 25600;
    }
}