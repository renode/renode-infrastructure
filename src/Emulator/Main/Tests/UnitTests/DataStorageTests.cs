//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Storage;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class DataStorageTests
    {
        [Test]
        public void ShouldThrowConstructionExceptionOnNoFile()
        {
            Assert.Throws<ConstructionException>(() => DataStorage.CreateFromFile("/doesntexist"));
        }

        [Test]
        public void ShouldThrowConstructionExceptionOnEmptyPath()
        {
            Assert.Throws<ConstructionException>(() => DataStorage.CreateFromFile(null));
            Assert.Throws<ConstructionException>(() => DataStorage.CreateFromFile(""));
        }
    }
}
