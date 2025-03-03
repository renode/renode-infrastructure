//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Extensions
{
    public static class MemoryDumpExtensions
    {
        public static void DumpBinary(this IMemory memory, SequencedFilePath fileName, ulong offset = 0, ICPU context = null)
        {
            memory.DumpBinary(fileName, offset, (ulong)memory.Size - offset, context);
        }

        public static void DumpBinary(this IMemory memory, SequencedFilePath fileName, ulong offset, ulong size, ICPU context = null)
        {
            AssertArguments(memory, offset, size);

            try
            {
                using(var writer = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    var windows = (int)((size - offset + WindowSize - 1) / WindowSize - 1);
                    for(var i = 0; i < windows; ++i)
                    {
                        WriteBinaryMemoryChunk(writer, memory, offset, i, context);
                    }

                    var lastChunkSize = (size - offset) % WindowSize;
                    lastChunkSize = lastChunkSize == 0 ? WindowSize : lastChunkSize;
                    WriteBinaryMemoryChunk(writer, memory, offset, windows, context, lastChunkSize);
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while saving to file {fileName}: {e.Message}");
            }
        }

        private static void WriteBinaryMemoryChunk(FileStream writer, IMemory memory, ulong offset, int chunk, ICPU context, ulong size = WindowSize)
        {
            var data = memory.ReadBytes((long)(offset + (ulong)chunk * WindowSize), (int)size, context);
            writer.Write(data, offset: 0, count: (int)size);
        }

        private static void AssertArguments(this IMemory memory, ulong offset, ulong size)
        {
            if(size == 0)
            {
                throw new RecoverableException($"'{nameof(size)}' must be greater than zero");
            }
            if(offset > (ulong)memory.Size)
            {
                throw new RecoverableException($"'{nameof(offset)}' is outside of memory");
            }
            if(offset + size > (ulong)memory.Size)
            {
                throw new RecoverableException($"'{nameof(size)}' is too big, region is outside of memory");
            }
        }

        private const ulong WindowSize = 100 * 1024;
    }
}
