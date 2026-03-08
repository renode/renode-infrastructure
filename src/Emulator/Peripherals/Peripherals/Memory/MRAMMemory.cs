//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;

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
    ///   - <see cref="WriteFaultMode"/> selects between power-loss (partial
    ///     program) and bit-corruption (deterministic bit flips) fault models.
    ///   - <see cref="WriteTraceEnabled"/> records each word write as a
    ///     (writeIndex, wordOffset) tuple for offline trace replay.
    ///   - <see cref="RetainOldDataOnFault"/> controls whether the un-programmed
    ///     half of a partial write retains old data (MRAM) or is filled with
    ///     <see cref="EraseFill"/> (flash).
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
            writeTrace = new List<(ulong writeIndex, long wordOffset)>();
        }

        public override void Reset()
        {
            // Intentionally do NOT call base.Reset() or clear storage:
            // this models non-volatile memory that retains data across resets.
            WriteInProgress = false;
            LastFaultInjected = false;
            FaultEverFired = false;
            TotalWordWrites = 0;
            WriteTraceClear();
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
        /// Simulate a power cut during a word-aligned write.  The second half of
        /// the word at <paramref name="address"/> is filled with EraseFill; the
        /// first half is left unchanged.  Call after writing partial data to
        /// model a mid-program power loss.
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

        /// <summary>
        /// Return all recorded write trace entries as a CSV string.
        /// Each line is "writeIndex,wordOffset\n".
        /// </summary>
        public string WriteTraceToString()
        {
            var sb = new StringBuilder();
            foreach(var entry in writeTrace)
            {
                sb.AppendFormat("{0},{1}\n", entry.writeIndex, entry.wordOffset);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clear all recorded write trace entries.
        /// </summary>
        public void WriteTraceClear()
        {
            writeTrace.Clear();
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

        public bool LastFaultInjected { get; set; }

        /// <summary>
        /// Sticky flag: set when any fault fires, never cleared by subsequent
        /// writes.  Use this instead of <see cref="LastFaultInjected"/> when
        /// you need to check after the CPU has continued past the faulted write.
        /// </summary>
        public bool FaultEverFired { get; set; }

        public ulong TotalWordWrites { get; set; }

        public ulong FaultAtWordWrite { get; set; } = ulong.MaxValue;

        /// <summary>
        /// Fault model selector:
        ///   0 = power_loss (default): partial program, first half written,
        ///       second half erased or retained depending on
        ///       <see cref="RetainOldDataOnFault"/>.
        ///   1 = bit_corruption: full word is written, then 1-3 deterministic
        ///       bits are flipped to model partial cell state transitions.
        /// </summary>
        public int WriteFaultMode { get; set; }

        /// <summary>
        /// Seed for the bit-corruption PRNG.  When 0, the word-aligned address
        /// is used as the seed, giving address-dependent deterministic results.
        /// </summary>
        public uint CorruptionSeed { get; set; }

        /// <summary>
        /// When true (default), the un-programmed half of a partial write
        /// retains the old data that was present before the erase step.  This
        /// models MRAM/FRAM where erase is implicit and old data survives
        /// partial programming.  When false, the un-programmed half is filled
        /// with <see cref="EraseFill"/>, modeling flash where erase physically
        /// clears the cell before programming.
        /// </summary>
        public bool RetainOldDataOnFault { get; set; } = true;

        /// <summary>
        /// When true, each word-granularity write is recorded as a
        /// (writeIndex, wordOffset) tuple.  Retrieve with
        /// <see cref="WriteTraceToString"/> and clear with
        /// <see cref="WriteTraceClear"/>.
        /// </summary>
        public bool WriteTraceEnabled { get; set; }

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
                    var oldWord = new byte[wordSize];
                    var mergedWord = new byte[wordSize];
                    for(var i = 0; i < wordSize; i++)
                    {
                        oldWord[i] = array[wordStart + i];
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
                        if(WriteFaultMode == 1)
                        {
                            // Bit-corruption mode: write the full merged word,
                            // then flip 1-3 deterministic bits to model partial
                            // cell state transitions during interrupted NVM programming.
                            for(var i = 0; i < wordSize; i++)
                            {
                                array[wordStart + i] = mergedWord[i];
                            }
                            ApplyBitCorruption(wordStart);
                        }
                        else
                        {
                            // Power-loss mode: partial program, first half written.
                            var partialBytes = wordSize / 2;
                            for(var i = 0; i < partialBytes; i++)
                            {
                                array[wordStart + i] = mergedWord[i];
                            }
                            // Second half: retain old data (MRAM) or leave as EraseFill (flash).
                            if(RetainOldDataOnFault)
                            {
                                for(var i = partialBytes; i < wordSize; i++)
                                {
                                    array[wordStart + i] = oldWord[i];
                                }
                            }
                        }
                        LastFaultInjected = true;
                        FaultEverFired = true;
                        TotalWordWrites++;
                        RecordWriteTrace(TotalWordWrites, wordStart);
                        break;
                    }

                    // Full program.
                    for(var i = 0; i < wordSize; i++)
                    {
                        array[wordStart + i] = mergedWord[i];
                    }
                    TotalWordWrites++;
                    RecordWriteTrace(TotalWordWrites, wordStart);
                }
            }
            finally
            {
                WriteInProgress = false;
            }
        }

        private void RecordWriteTrace(ulong writeIndex, long wordOffset)
        {
            if(!WriteTraceEnabled || writeTrace.Count >= WriteTraceMaxEntries)
            {
                return;
            }
            writeTrace.Add((writeIndex, wordOffset));
        }

        /// <summary>
        /// Apply deterministic bit corruption to the word at the given aligned
        /// address.  Flips 1-3 bits using an LCG PRNG, modeling partial cell
        /// state transitions during interrupted NVM programming.
        /// </summary>
        private void ApplyBitCorruption(long wordStart)
        {
            var seed = CorruptionSeed != 0 ? CorruptionSeed : (uint)wordStart;
            var totalBits = wordSize * 8;

            // Determine number of bits to flip: 1-3 from first LCG step.
            seed = LcgNext(seed);
            var numFlips = (int)(seed % 3) + 1;

            for(var f = 0; f < numFlips; f++)
            {
                seed = LcgNext(seed);
                var bitPos = (int)(seed % (uint)totalBits);
                var byteIndex = bitPos / 8;
                var bitIndex = bitPos % 8;
                array[wordStart + byteIndex] ^= (byte)(1 << bitIndex);
            }
        }

        private static uint LcgNext(uint seed)
        {
            return (uint)((seed * 1103515245UL + 12345UL) & 0xFFFFFFFF);
        }

        private long AlignDown(long value)
        {
            return value & ~((long)wordSize - 1);
        }

        private int wordSize;
        private readonly List<(ulong writeIndex, long wordOffset)> writeTrace;

        private const ulong DefaultSize = 0x80000;
        private const int DefaultWordSize = 8;
        private const int WriteTraceMaxEntries = 100000;
    }
}
