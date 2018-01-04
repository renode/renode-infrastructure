//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Storage;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class StorageTests
    {
        public StorageTests()
        {
            random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        }

        [Test, Repeat(10)]
        public void ShouldReadAndWriteLBABackend()
        {
            var underlyingFile = TemporaryFilesManager.Instance.GetTemporaryFile();
            try
            {
                var blocksCount = random.Next(MaxBlocksCount + 1);
                using(var lbaBackend = new LBABackend(underlyingFile, blocksCount, BlockSize))
                {
                    var testBlocksCount = Math.Min(DesiredTestBlocksCount, blocksCount);
                    var blockPositions = Enumerable.Range(0, blocksCount).OrderBy(x => random.Next()).Take(testBlocksCount).ToArray();
                    var blockContent = new byte[testBlocksCount][];
                    for(var i = 0; i < testBlocksCount; i++)
                    {
                        blockContent[i] = new byte[BlockSize];
                        random.NextBytes(blockContent[i]);
                        lbaBackend.Write(blockPositions[i], blockContent[i], 1);
                    }
                    for(var i = 0; i < testBlocksCount; i++)
                    {
                        CollectionAssert.AreEqual(blockContent[i], lbaBackend.Read(blockPositions[i], 1));
                    }
                }
            }
            finally
            {
                File.Delete(underlyingFile);
            }
        }

        private readonly PseudorandomNumberGenerator random;
        private const int BlockSize = 512;
        private const int DesiredTestBlocksCount = 500;
        private const int MaxBlocksCount = 1024 * 256;
    }
}

