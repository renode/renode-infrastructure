//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using System.Collections.Generic;
using System.IO;

namespace Antmicro.Renode.Utilities
{
    public class BlobManager
    {
        public BlobManager()
        {
            providers = new List<IBlobProvider>();
        }

        public void Load(Stream stream, string streamName)
        {
            using (var reader = new PrimitiveReader(stream, false))
            {
                foreach(var provider in providers)
                {
                    var tempFile = TemporaryFilesManager.Instance.GetTemporaryFile();
                    if(ConfigurationManager.Instance.Get("file-system", "use-cow", false))
                    {
                        FileCopier.Copy(streamName, tempFile, true);

                        var size = reader.ReadInt64();
                        var localPosition = stream.Position;
                        reader.ReadBytes((int)size);
                        provider.BlobIsReady(tempFile, localPosition, size);
                    }
                    else
                    {
                        var size = reader.ReadInt64();
                        using(var fileStream = new FileStream(tempFile, FileMode.OpenOrCreate))
                        {
                            reader.CopyTo(fileStream, size);
                        }
                        provider.BlobIsReady(tempFile, 0, size);
                    }
                }
            }
        }

        public void Register(IBlobProvider provider)
        {
            providers.Add(provider);
        }

        public void Save(Stream stream)
        {
            using(var writer = new PrimitiveWriter(stream, false))
            {
                foreach(var provider in providers)
                {
                    var descriptor = provider.GetBlobDescriptor();
                    writer.Write(descriptor.Size);
                    writer.CopyFrom(descriptor.Stream, descriptor.Size);
                }
            }
        }

        [Constructor]
        private readonly List<IBlobProvider> providers;
    }
}

