//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.CPU;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public interface ICanLoadFiles : IInterestingType
    {
        // Handle data from FileChunk collection - specific for each peripheral
        void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu);
    }

    public struct FileChunk
    {
        public IEnumerable<byte> Data;
        public ulong OffsetToLoad;
    }
}
