//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using System.IO;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Storage
{
    public class SerializableFileStreamWrapper : IDisposable, IBlobProvider
    {
        public SerializableFileStreamWrapper(string underlyingFile, long? size = null)
        {
            Stream = new FileStreamLimitWrapper(new FileStream(underlyingFile, FileMode.OpenOrCreate), 0, size);
            blobManager = EmulationManager.Instance.CurrentEmulation.BlobManager;
        }

        public void BlobIsReady(string fileName, long offset, long length)
        {
            var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
            Stream = new FileStreamLimitWrapper(fileStream, offset, length);
        }

        public void Dispose()
        {
            Stream.Dispose();
        }

        public BlobDescriptor GetBlobDescriptor()
        {
            Stream.Seek(0, SeekOrigin.Begin);
            return new BlobDescriptor { Stream = Stream, Size = Stream.Length };
        }

        public FileStreamLimitWrapper Stream 
        {
            get { return stream; }
            private set { stream = value; }
        }

        [PreSerialization]
        [PostDeserialization]
        private void OnSerialization()
        {
            blobManager.Register(this);
        }

        private readonly BlobManager blobManager;
        [Transient]
        private FileStreamLimitWrapper stream;
    }
}

