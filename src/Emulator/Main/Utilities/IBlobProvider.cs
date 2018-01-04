//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

namespace Antmicro.Renode.Utilities
{
    public interface IBlobProvider
    {
        BlobDescriptor GetBlobDescriptor();
        void BlobIsReady(string fileName, long offset, long length);
    }

    public struct BlobDescriptor
    {
        public Stream Stream { get; set; }
        public long Size { get; set; }
    }
}

