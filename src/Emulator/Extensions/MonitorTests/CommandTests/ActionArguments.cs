//
// Copyright (c) 2010-2025 Antmicro
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

        [TestCase("emulation MethodWithArrayThenString [1, 2, 3] \",\"", "numbers: [1, 2, 3] separator: ,",
            TestName = "ArrayAsNonLastParameterPositional")]

        [TestCase("emulation MethodWithArrayThenString numbers=[1, 2, 3] separator=\",\"", "numbers: [1, 2, 3] separator: ,",
            TestName = "ArrayAsNonLastParameterNamed")]

        [TestCase("emulation MethodWithArrayThenString [1, 2, 3] separator=\",\"", "numbers: [1, 2, 3] separator: ,",
            TestName = "ArrayAsNonLastParameterMixed")]

        [TestCase("emulation MethodWithArrayThenString [] \",\"", "numbers: [] separator: ,",
            TestName = "EmptyArrayAsNonLastParameter")]

        [TestCase("emulation MethodWithArrayThenString [1, 2, 3, ] \",\"", "numbers: [1, 2, 3] separator: ,",
            TestName = "ArrayWithTrailingComma")]

        [TestCase("emulation MethodWithListOfStrings words=[\"a\", \"b\",]", "words: [\"a\", \"b\"]",
            TestName = "ListOfStringsWithTrailingComma")]

        [TestCase("emulation MethodWithListOfStrings [\"a\", \"b c\", \"d\"]", "words: [\"a\", \"b c\", \"d\"]",
            TestName = "ListOfStringsPositional")]

        [TestCase("emulation MethodWithListOfStrings words=[\"a\", \"b\", \"c\"]", "words: [\"a\", \"b\", \"c\"]",
            TestName = "ListOfStringsNamed")]

        [TestCase("emulation MethodWithListOfStrings []", "words: []",
            TestName = "ListOfStringsEmptyPositional")]

        [TestCase("emulation MethodWithListOfStrings words=[]", "words: []",
            TestName = "ListOfStringsEmptyNamed")]

        [TestCase("emulation MethodWithParamsStringArray \"one\" \"two three\" \"four\"", "args: [\"one\", \"two three\", \"four\"]",
            TestName = "ParamsStringArrayPositional")]

        [TestCase("emulation MethodWithStringThenIntArrayThenAnotherString \"abc\" [1, 2] \"def\"", "s1:abc numbers:[1,2] s2:def",
            TestName = "ArrayPositionalNonLast")]

        [TestCase("emulation MethodWithParamsStringArray \"one\" \"two three\"", "args: [\"one\", \"two three\"]",
            TestName = "ParamsStringArrayParams")]

        [TestCase("emulation MethodWithParamsStringArray [\"one\", \"two three\"]", "args: [\"one\", \"two three\"]",
            TestName = "ParamsStringArrayPositional")]

        [TestCase("emulation MethodWithParamsStringArray args=[\"one\",\"two three\"]", "args: [\"one\", \"two three\"]",
            TestName = "ParamsStringArrayNamed")]

        [TestCase("emulation MethodWithParamsStringArray", "args: []",
            TestName = "ParamsStringArrayEmptyParams")]

        [TestCase("emulation MethodWithParamsStringArray []", "args: []",
            TestName = "ParamsStringArrayEmptyPositional")]

        [TestCase("emulation MethodWithParamsStringArray args=[]", "args: []",
            TestName = "ParamsStringArrayEmptyNamed")]

        [TestCase("emulation MethodWithNullableIntArray [1, null, 3]", "numbers: [1, null, 3]",
            TestName = "NullableIntArrayContainingNulls")]

        [TestCase("emulation MethodWithNullableIntArray numbers=[1, null, 3]", "numbers: [1, null, 3]",
            TestName = "NullableIntArrayContainingNullsNamed")]

        [TestCase("emulation MethodWithNullableIntList [1, null, 3]", "numbers: [1, null, 3]",
            TestName = "NullableIntListContainingNulls")]

        [TestCase("emulation MethodWithNullableIntList numbers=[1, null, 3]", "numbers: [1, null, 3]",
            TestName = "NullableIntListContainingNullsNamed")]

        [TestCase("emulation MethodWithNullableIntList []", "numbers: []",
            TestName = "NullableIntListEmpty")]
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

        [TestCase("emulation MethodWithStringThenIntArrayThenAnotherString s2=\"a\" [1, 2, 3] s1=\"b\"",
            TestName = "PositionalAfterNamedWithMismatchedPosition")]

        [TestCase("emulation MethodThatTakesParamsIntArray 1 \"2\" 3",
            TestName = "TypeMismatchInParamsArray")]

        [TestCase("emulation MethodWithArrayThenString [1, \"2\", 3] \",\"",
            TestName = "TypeMismatchInArray")]

        [TestCase("emulation MethodWithArrayThenString [1, 2, 3 \",\"",
            TestName = "UnclosedBracket")]

        [TestCase("emulation MethodThatTakesParamsIntArray [1,",
            TestName = "UnclosedBracketWithTrailingComma")]

        [TestCase("emulation MethodWithArrayThenString [1, , 3] \",\"",
            TestName = "EmptyElement")]

        [TestCase("emulation MethodWithArrayThenString [1, 2, 3, ,] \",\"",
            TestName = "TwoTrailingCommas")]
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

