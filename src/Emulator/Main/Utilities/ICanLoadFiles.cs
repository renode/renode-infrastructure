//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public interface ICanLoadFiles
    {
        // Handle data from FileChunk collection - specific for each peripheral
        void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu);
    }

    public static class LoadFileChunksExtensions
    {
        // Returns the lowest touched address
        public static ulong LoadFileChunks(this IMultibyteWritePeripheral peripheral, IEnumerable<FileChunk> chunks, ICPU cpu = null)
        {
            ulong minAddr = ulong.MaxValue;

            foreach(FileChunk chunk in chunks)
            {
                Logger.LogAs(
                    peripheral,
                    LogLevel.Info,
                    "Loading block of {0} bytes length at 0x{1:X}.",
                    chunk.Data.Count(),
                    chunk.OffsetToLoad
                );
                var chunkData = chunk.Data.ToArray();
                peripheral.WriteBytes((long)chunk.OffsetToLoad, chunkData, 0, chunkData.Length, context: cpu);
                minAddr = Math.Min(minAddr, chunk.OffsetToLoad);
            }

            return minAddr;
        }
    }

    public struct FileChunk
    {
        public IEnumerable<byte> Data;
        public ulong OffsetToLoad;
    }
}
