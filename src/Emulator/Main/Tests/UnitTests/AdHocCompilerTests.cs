//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class AdHocCompilerTests
    {
        [SetUp]
        public void Init()
        {
            adhoc = new AdHocCompiler();
            manager = TypeManager.Instance;
        }

        [Test]
        public void ShouldNotThrowOnEmptyFile()
        {
            var extension = TemporaryFilesManager.Instance.GetTemporaryFile();
            Assert.DoesNotThrow(() => { file = adhoc.Compile(extension); });
            Assert.IsTrue(manager.ScanFile(file));
        }

        [Test]
        public void ShouldCompileCsFile()
        {
            var extension = GetType().Assembly.FromResourceToTemporaryFile("MockExtension.cs");
            var methodsBefore = manager.GetExtensionMethods(typeof(Emulation));
            Assert.IsFalse(methodsBefore.Any(x => x.Name == "GetMockString"));
            Assert.DoesNotThrow(() => { file = adhoc.Compile(extension); });
            Assert.IsTrue(manager.ScanFile(file));
            var methodsAfter = manager.GetExtensionMethods(typeof(Emulation));
            Assert.IsNotEmpty(methodsAfter);
            Assert.IsTrue(methodsAfter.Any(x => x.Name == "GetMockString"));
            var result = manager.TryGetTypeByName("Antmicro.Renode.Utilities.MockExtension");
            Assert.IsNotNull(result);
        }

        private AdHocCompiler adhoc;
        private TypeManager manager;
        private string file; 
    }
}
