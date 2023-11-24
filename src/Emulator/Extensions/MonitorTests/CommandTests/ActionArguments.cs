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

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterString arg=\"bcd\"", "arg: bcd args: []",
            TestName = "ParamsIntArrayEmptyAfterStringPassedByName")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterString arg=\"bcd\" 1 2 3", "arg: bcd args: [1, 2, 3]",
            TestName = "ParamsIntArrayAfterStringPassedByName")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterOptionalString", "arg: default args: []",
            TestName = "ParamsIntArrayAfterOptionalStringEmptyTest")]

        [TestCase("emulation MethodThatTakesParamsIntArrayAfterOptionalString \"bcd\" 1 2 3", "arg: bcd args: [1, 2, 3]",
            TestName = "ParamsIntArrayAfterOptionalStringTest")]

        [TestCase("emulation MethodWithOptionalParameters", "a: 1, b: 2, c: 3",
            TestName = "OptionalParametersProvideNone")]

        [TestCase("emulation MethodWithOptionalParameters 3 4 5", "a: 3, b: 4, c: 5",
            TestName = "OptionalParametersProvideAll")]

        [TestCase("emulation MethodWithOptionalParameters a=4", "a: 4, b: 2, c: 3",
            TestName = "OptionalParametersProvideFirstByName")]

        [TestCase("emulation MethodWithOptionalParameters 2 b=4", "a: 2, b: 4, c: 3",
            TestName = "OptionalParametersProvideMiddleByName")]

        [TestCase("emulation MethodWithOptionalParameters a=3 4 5", "a: 3, b: 4, c: 5",
            TestName = "OptionalParametersProvideFirstByNameRestByPos")]

        [TestCase("emulation MethodWithOptionalParameters 3 c=4", "a: 3, b: 2, c: 4",
            TestName = "OptionalParametersProvideLastByName")]

        [TestCase("emulation MethodWithOptionalParameters a=3 b=4 c=5", "a: 3, b: 4, c: 5",
            TestName = "OptionalParametersProvideAllByName")]

        [TestCase("emulation MethodWithOptionalParameters c=5 b=4 a=3", "a: 3, b: 4, c: 5",
            TestName = "OptionalParametersProvideAllByNameReverseOrder")]

        [TestCase("emulation MethodWithOptionalParametersAndParamArray c=5 b=4 a=3", "a: 3, b: 4, c: 5; ",
            TestName = "OptionalParametersProvideAllByNameAndEmptyArray")]

        [TestCase("emulation MethodWithOptionalParametersAndParamArray c=5 b=4 a=3 9 6", "a: 3, b: 4, c: 5; 9, 6",
            TestName = "OptionalParametersProvideAllByNameAndArray")]
        public void CommandResultShouldContain(string command, string expected)
        {
            monitor.Parse(command, commandEater);
            var contentsAfter = commandEater.GetContents();
            StringAssert.Contains(expected, contentsAfter);
        }

        [TestCase("emulation MethodWithOptionalParameters c=5 c=3",
            TestName = "OptionalParametersFailOnDuplicateName")]

        [TestCase("emulation MethodWithOptionalParameters 1 a=4",
            TestName = "OptionalParametersFailOnDuplicatePositionalAndNamed")]

        [TestCase("emulation MethodWithOptionalParameters b=1 4",
            TestName = "OptionalParametersFailOnPositionalAfterNamed")]
        public void CommandShouldFail(string command)
        {
            CommandResultShouldContain(command, "The following methods are available");
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

