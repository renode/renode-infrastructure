//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.UserInterface.Commands;

namespace Antmicro.Renode.MonitorTests
{
    [TestFixture]
    public class FeaturesTests
    {
        [Test]
        public void AutoLoadCommandTest()
        {
            var commandInteraction = new CommandInteractionEater();
            var commandInstance = new TestCommand(monitor);
            monitor.Parse("help", commandInteraction);
            var contents = commandInteraction.GetContents();
            Assert.IsTrue(contents.Contains(commandInstance.Description));
        }

        [Test]
        public void ShouldHandleNewExtensionsFromLoadedAssembly()
        {
            var commandInteraction = new CommandInteractionEater();
            var file = GetType().Assembly.FromResourceToTemporaryFile("MockExtension.cs");
            monitor.Parse("emulation GetSeed", commandInteraction);
            monitor.Parse($"i @{file}", commandInteraction);
            monitor.Parse("emulation GetMockString", commandInteraction);
            var contentsAfter = commandInteraction.GetContents();
            Assert.IsTrue(contentsAfter.Contains("this is an extension"));
        }

        [SetUp]
        public void SetUp()
        {
            monitor = new Monitor();
        }

        private Monitor monitor;
    }

    public class TestCommand : AutoLoadCommand
    {
        public TestCommand(Monitor monitor):base(monitor, "featuresTests.TestCommand", "is just a test command.")
        {
        }
    }
}

