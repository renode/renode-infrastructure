//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Memory
{
    /// <summary>
    /// Non-volatile memory model with configurable word-write semantics and
    /// power-loss fault injection.  Suitable for MRAM, FRAM, or any byte-
    /// addressable NVM whose writes are atomic at a fixed word granularity.
    ///
    /// Key behaviors:
    ///   - Reset() preserves storage contents (non-volatile).
    ///   - When <see cref="EnforceWordWriteSemantics"/> is true, every write is
    ///     decomposed into erase-then-program cycles at <see cref="WordSize"/>
    ///     boundaries, matching real hardware behavior.
    ///   - <see cref="InjectPartialWrite"/> simulates a power cut mid-program:
    ///     the first half of the target word is written, the second half is
    ///     zeroed.  This is the core primitive for fault-injection campaigns.
    /// </summary>
    public class MRAMMemory : ArrayMemory
    {
        public MRAMMemory(ulong size = DefaultSize, int wordSize = DefaultWordSize) : base(size)
        {
            if(wordSize <= 0 || (wordSize & (wordSize - 1)) != 0)
            {
                throw new ArgumentException("WordSize must be a positive power of two");
            }

            this.wordSize = wordSize;
        }

        public new void Reset()
        {
            // Intentionally do NOT call base.Reset() or clear storage:
            // this models non-volatile memory that retains data across resets.
            WriteInProgress = false;
            LastFaultInjected = false;
            TotalWordWrites = 0;
        }

        public override void WriteByte(long offset, byte value)
        {
            WriteBytesWithWordSemantics(offset, new[] { value });
        }

        public override void WriteWord(long offset, ushort value)
        {
            WriteBytesWithWordSemantics(offset, new[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
            });
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            WriteBytesWithWordSemantics(offset, new[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
            });
        }

        public override void WriteQuadWord(long offset, ulong value)
        {
            WriteBytesWithWordSemantics(offset, new[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 56) & 0xFF),
            });
        }

        /// <summary>
        /// Simulate a power cut during a word-aligned write.  The first half of
        /// the word at <paramref name="address"/> is zeroed; the second half is
        /// also zeroed — modeling a partial erase with no successful program.
        /// </summary>
        public void InjectPartialWrite(long address)
        {
            var aligned = AlignDown(address);
            if(aligned < 0 || aligned + wordSize > Size)
            {
                this.Log(LogLevel.Error, "InjectPartialWrite at 0x{0:X} is outside memory bounds", address);
                return;
            }

            var half = wordSize / 2;
            for(var i = half; i < wordSize; i++)
            {
                array[aligned + i] = EraseFill;
            }

            LastFaultInjected = true;
        }

        /// <summary>
        /// Overwrite a region with a fixed pattern, modeling arbitrary corruption.
        /// </summary>
        public void InjectFault(long address, long length, byte pattern = 0x00)
        {
            if(length <= 0)
            {
                return;
            }

            if(address < 0 || address + length > Size)
            {
                this.Log(LogLevel.Error, "InjectFault at 0x{0:X} length {1} is outside memory bounds", address, length);
                return;
            }

            for(var i = 0L; i < length; i++)
            {
                array[address + i] = pattern;
            }
            LastFaultInjected = true;
        }

        /// <summary>
        /// Query the total number of word-granularity write operations performed.
        /// Useful for setting up fault injection at a specific write index.
        /// </summary>
        public ulong GetWordWriteCount()
        {
            return TotalWordWrites;
        }

        public int WordSize
        {
            get { return wordSize; }
            set
            {
                if(value <= 0 || (value & (value - 1)) != 0)
                {
                    throw new ArgumentException("WordSize must be a positive power of two");
                }
                wordSize = value;
            }
        }

        public bool EnforceWordWriteSemantics { get; set; } = true;

        public byte EraseFill { get; set; }

        public bool WriteInProgress { get; private set; }

        public bool LastFaultInjected { get; private set; }

        public ulong TotalWordWrites { get; private set; }

        public ulong FaultAtWordWrite { get; set; } = ulong.MaxValue;

        private void WriteBytesWithWordSemantics(long offset, byte[] data)
        {
            if(data.Length == 0)
            {
                return;
            }

            if(!EnforceWordWriteSemantics)
            {
                for(var i = 0; i < data.Length; i++)
                {
                    base.WriteByte(offset + i, data[i]);
                }
                return;
            }

            var firstWordStart = AlignDown(offset);
            var lastWordStart = AlignDown(offset + data.Length - 1);

            WriteInProgress = true;
            LastFaultInjected = false;

            try
            {
                for(var wordStart = firstWordStart; wordStart <= lastWordStart; wordStart += wordSize)
                {
                    // Read-modify-write: read current word, merge new data, erase, program.
                    var mergedWord = new byte[wordSize];
                    for(var i = 0; i < wordSize; i++)
                    {
                        mergedWord[i] = array[wordStart + i];
                    }

                    for(var i = 0; i < data.Length; i++)
                    {
                        var absoluteAddress = offset + i;
                        if(absoluteAddress < wordStart || absoluteAddress >= wordStart + wordSize)
                        {
                            continue;
                        }
                        mergedWord[absoluteAddress - wordStart] = data[i];
                    }

                    // Erase the word.
                    for(var i = 0; i < wordSize; i++)
                    {
                        array[wordStart + i] = EraseFill;
                    }

                    // Check for automatic fault injection at this write index.
                    var currentWriteIndex = TotalWordWrites + 1;
                    if(currentWriteIndex == FaultAtWordWrite)
                    {
                        // Partial program: first half written, second half stays erased.
                        var partialBytes = wordSize / 2;
                        for(var i = 0; i < partialBytes; i++)
                        {
                            array[wordStart + i] = mergedWord[i];
                        }
                        LastFaultInjected = true;
                        TotalWordWrites++;
                        break;
                    }

                    // Full program.
                    for(var i = 0; i < wordSize; i++)
                    {
                        array[wordStart + i] = mergedWord[i];
                    }
                    TotalWordWrites++;
                }
            }
            finally
            {
                WriteInProgress = false;
            }
        }

        private long AlignDown(long value)
        {
            return value & ~((long)wordSize - 1);
        }

        private int wordSize;

        private const ulong DefaultSize = 0x80000;
        private const int DefaultWordSize = 8;
    }
}
