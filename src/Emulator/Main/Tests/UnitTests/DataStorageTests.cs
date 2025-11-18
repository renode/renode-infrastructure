//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using NUnit.Framework;

using Antmicro.Renode.Storage;
using Antmicro.Renode.Exceptions;

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
    }
}
