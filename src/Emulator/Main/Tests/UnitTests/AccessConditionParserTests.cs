//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class AccessConditionParserTests
    {
        [TestCaseSource(nameof(AccessConditionParserTestCases))]
        public void ShouldParseAndConvertToDnf(string conditionString, string expectedDnfString)
        {
            var dnfExpression = AccessConditionParser.ParseCondition(conditionString);
            Assert.AreEqual(expectedDnfString, dnfExpression.ToString(), $"Input condition: '{conditionString}'");
        }

        [TestCase(31)]
        [TestCase(32)]
        [TestCase(33)]
        [TestCase(63)]
        public void ShouldEvaluateStateBitsAbove32Bits(int position)
        {
            var stateBits = new Dictionary<string, int>
            {
                ["privileged"] = 0,
                ["above32"] = position,
            };

            var dnfExpression = AccessConditionParser.ParseCondition("above32 && !privileged");
            var result = AccessConditionParser.EvaluateWithStateBits(dnfExpression, _ => stateBits);

            Assert.AreEqual(1, result.Count);
            var mask = result[string.Empty].Single();
            Assert.AreEqual(1UL << position, mask.State, "State");
            Assert.AreEqual((1UL << position) | 1UL, mask.Mask, "Mask");
        }

        [TestCase(32)]
        [TestCase(63)]
        public void ShouldDetectConflictingStateBitsAbove32Bits(int position)
        {
            var stateBits = new Dictionary<string, int>
            {
                ["above32"] = position,
            };

            var dnfExpression = AccessConditionParser.ParseCondition("above32 && !above32");
            var exception = Assert.Throws<RecoverableException>(() => AccessConditionParser.EvaluateWithStateBits(dnfExpression, _ => stateBits));

            StringAssert.Contains("Conditions conflict detected", exception.Message);
        }

        private static IEnumerable<TestCaseData> AccessConditionParserTestCases()
        {
            // Trivial as the input is already a DNF term
            yield return new TestCaseData(
                "a",
                "(a)"
            );
            // Trivial as the input is already a DNF term
            yield return new TestCaseData(
                "!a",
                "(!a)"
            );
            // Trivial as the input is already a DNF term, but it has superfluous parentheses
            yield return new TestCaseData(
                "!((((((a))))))",
                "(!a)"
            );
            // Trivial as the input is already a DNF term, but it is a double negation
            yield return new TestCaseData(
                "!(((!(((a))))))",
                "(a)"
            );
            // Trivial as the input is already a DNF term
            yield return new TestCaseData(
                "!secure && initiator == cpu1",
                "(!secure && initiator == cpu1)"
            );
            // De Morgan's law yields a single DNF term
            yield return new TestCaseData(
                "!(privileged || secure)",
                "(!privileged && !secure)"
            );
            // De Morgan's law, output is a DNF formula of 2 terms
            yield return new TestCaseData(
                "!(privileged && secure)",
                "(!privileged) || (!secure)"
            );
            // Initiator conditions distributed over the state conditions
            yield return new TestCaseData(
                "secure && !privileged && (initiator == cpu1 || initiator == cpu2)",
                "(secure && !privileged && initiator == cpu1) || (secure && !privileged && initiator == cpu2)"
            );
            // An input condition which results in a fairly large DNF formula (4 terms, each with 3 conditions in it)
            yield return new TestCaseData(
                "(secure || busSecure) && (initiator == cpu1 || initiator == cpu2) && privileged",
                "(secure && initiator == cpu1 && privileged) || (secure && initiator == cpu2 && privileged) || (busSecure && initiator == cpu1 && privileged) || (busSecure && initiator == cpu2 && privileged)"
            );
        }
    }
}