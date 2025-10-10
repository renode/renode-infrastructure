//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.IO;
using System.IO.Compression;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Storage
{
    public static class DataStorage
    {
        public static Stream Create(string imageFile, long? size = null, bool persistent = false, byte paddingByte = 0)
        {
            Logger.Log(LogLevel.Warning, "Create method is obsolete, please use CreateFromFile method");
            return CreateFromFile(imageFile, size, persistent, paddingByte);
        }

        public static Stream CreateFromFile(string imageFile, long? size = null, bool persistent = false, byte paddingByte = 0, CompressionType compression = CompressionType.None)
        {
            if(string.IsNullOrEmpty(imageFile))
            {
                throw new ConstructionException("No image file provided.");
            }

            if(persistent && compression != CompressionType.None)
            {
                throw new RecoverableException("Creating persistent storage from compresssed file is not supported.");
            }

            if(!persistent)
            {
                var tempFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(imageFile, tempFileName, true);
                imageFile = tempFileName;
            }

            return new SerializableStreamView(GetUnderlyingStream(imageFile, compression), size, paddingByte: paddingByte);
        }

        public static Stream Create(long size, byte paddingByte = 0)
        {
            Logger.Log(LogLevel.Warning, "Create method is obsolete, please use CreateInTemporaryFile method");
            return CreateInTemporaryFile(size, paddingByte);
        }

        public static Stream CreateInTemporaryFile(long size, byte paddingByte = 0)
        {
            return new SerializableStreamView(new FileStream(TemporaryFilesManager.Instance.GetTemporaryFile(), FileMode.OpenOrCreate), size, paddingByte);
        }

        public static Stream CreateInMemory(int size, byte paddingByte = 0)
        {
            var mem = new byte[size];
            return new SerializableStreamView(new MemoryStream(mem), size, paddingByte);
        }

        private static Stream GetUnderlyingStream(string filepath, CompressionType compression)
        {
            if(compression == CompressionType.None)
            {
                return new FileStream(filepath, FileMode.OpenOrCreate);
            }

            var tempFilepath = TemporaryFilesManager.Instance.GetTemporaryFile();
            using(var decompressedFile = File.OpenWrite(tempFilepath))
            using(var compressedFile = GetDecompressedStream(filepath, compression))
            {
                compressedFile.CopyTo(decompressedFile);
            }
            return new FileStream(tempFilepath, FileMode.OpenOrCreate);
        }

        private static Stream GetDecompressedStream(string filepath, CompressionType compression)
        {
            switch(compression)
            {
            case CompressionType.GZip:
                return new GZipStream(File.OpenRead(filepath), CompressionMode.Decompress);
            default:
                throw new RecoverableException($"Unrecognized compression type {compression}");
            }
        }
    }

    public enum CompressionType
    {
        None,
        GZip,
    }
}