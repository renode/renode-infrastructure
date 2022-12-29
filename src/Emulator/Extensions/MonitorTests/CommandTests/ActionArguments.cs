//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;
using NUnit.Framework;

namespace Antmicro.Renode.MonitorTests.CommandTests
{
    [TestFixture]
    public class ActionArguments
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            monitor = new Monitor();
            commandEater = new CommandInteractionEater();

            var file = GetType().Assembly.FromResourceToTemporaryFile("MockExtension.cs");
            monitor.Parse($"i @{file}", commandEater);
            commandEater.Clear();
        }

        [TestCase("emulation MethodThatTakesParamsIntArray", "args: []",
            TestName = "ParamsIntArrayEmptyTest")]

        [TestCase("emulation MethodThatTakesParamsIntArray 1 2 3", "args: [1, 2, 3]",
            TestName = "ParamsIntArrayTest")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterString \"bcd\" 1 2 3", "arg: bcd args: [1, 2, 3]",
            TestName = "ParamsIntArrayAfterStringTest")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterOptionalString", "arg: default args: []",
            TestName = "ParamsIntArrayAfterOptionalStringEmptyTest")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterOptionalString \"bcd\" 1 2 3", "arg: bcd args: [1, 2, 3]",
            TestName = "ParamsIntArrayAfterOptionalStringTest")]
        public void CommandResultShouldContain(string command, string expected)
        {
            monitor.Parse(command, commandEater);
            var contentsAfter = commandEater.GetContents();
            StringAssert.Contains(expected, contentsAfter);
        }

        [TearDown]
        public void TearDown()
        {
            commandEater.Clear();
        }

        private CommandInteractionEater commandEater;
        private Monitor monitor;
    }
}

