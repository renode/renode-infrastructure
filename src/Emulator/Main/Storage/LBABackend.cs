//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Storage
{
    public class LBABackend : IDisposable
    {
        public LBABackend(int numberOfBlocks, int blockSize = 512) : this(TemporaryFilesManager.Instance.GetTemporaryFile(), numberOfBlocks, blockSize)
        {
        }

        public LBABackend(string underlyingFile, int? numberOfBlocks = null, int blockSize = 512, bool persistent = true)
        {
            if(!File.Exists(underlyingFile))
            {
                throw new RecoverableException(new FileNotFoundException("File not found: {0}.".FormatWith(underlyingFile), underlyingFile));
            }
            this.blockSize = blockSize;
            this.persistent = persistent;
            this.underlyingFile = underlyingFile;
            this.numberOfBlocks = Touch(numberOfBlocks);
        }

        public int BlockSize
        {
            get
            {
                return blockSize;
            }
        }

        public int NumberOfBlocks
        {
            get
            {
                return numberOfBlocks;
            }
        }

        public string UnderlyingFile
        {
            get
            {
                return underlyingFile;
            }
        }

        public byte[] Read(int startingBlock, int numberOfBlocksToRead = 1)
        {
            Touch(numberOfBlocks);
            var bytesToRead = blockSize * numberOfBlocksToRead;
            Logger.LogAs(this, LogLevel.Noisy, "Reading {0} blocks ({1}B), starting at block no {2}.",
                          numberOfBlocksToRead, Misc.NormalizeBinary(numberOfBlocksToRead * bytesToRead), startingBlock);
            file.Seek((long)blockSize * startingBlock, SeekOrigin.Begin);
            return file.ReadBytes(bytesToRead);
        }

        public void Write(int startingBlock, byte[] data, int numberOfBlocksToWrite = 1)
        {
            Touch(numberOfBlocks);
            Logger.LogAs(this, LogLevel.Noisy, "Writing {0} blocks ({1}B), starting at block no {2}.",
                          numberOfBlocksToWrite, Misc.NormalizeBinary(data.Length), startingBlock);
            if(data.Length > (long)blockSize * numberOfBlocksToWrite)
            {
                throw new InvalidOperationException("Cannot write more data than the LBA block size multiplied by number of blocks to read.");
            }
            file.Seek((long)blockSize * startingBlock, SeekOrigin.Begin);
            file.Write(data, 0, data.Length);
        }

        public void Dispose()
        {
            if(file != null)
            {
                file.Dispose();
            }
        }

        private int ToNumberOfBlocks(long value)
        {
            return checked((int)(value / blockSize) + ((value % blockSize > 0) ? 1 : 0));
        }

        private int Touch(int? numberOfBlocks)
        {
            if(file != null)
            {
                return 0;
            }
            if(!persistent)
            {
                var tempFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(underlyingFile, tempFileName, true);
                underlyingFile = tempFileName;
            }
            if(numberOfBlocks == null)
            {
                numberOfBlocks = ToNumberOfBlocks(new FileInfo(underlyingFile).Length);
            }
            var size = blockSize * (long)numberOfBlocks;
            file = new SerializableStreamView(new FileStream(underlyingFile, FileMode.OpenOrCreate), size);

            return (int)numberOfBlocks;
        }

        private readonly int blockSize;
        private readonly int numberOfBlocks;
        private readonly bool persistent;
        private string underlyingFile;
        private SerializableStreamView file;
    }
}

