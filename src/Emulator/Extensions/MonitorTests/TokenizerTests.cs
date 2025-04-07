//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.UserInterface.Tokenizer;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.MonitorTests
{
    [TestFixture]
    public class TokenizerTests
    {
        [Test]
        public void CommentTest()
        {
            var result = tokenizer.Tokenize("#something");
            AssertTokenizationTypes(result, typeof(CommentToken));
            AssertTokenizationValues(result, "something");

            result = tokenizer.Tokenize("emu[\"SomeIndexWith#Hash\"]");
            AssertTokenizationTypes(result, typeof(LiteralToken), typeof(LeftBraceToken), typeof(StringToken), typeof(RightBraceToken));
            AssertTokenizationValues(result, "emu", "[", "SomeIndexWith#Hash", "]");

            result = tokenizer.Tokenize("emu[\"SomeIndexWithoutHash\"#Comment]");
            AssertTokenizationTypes(result, typeof(LiteralToken), typeof(LeftBraceToken), typeof(StringToken), typeof(CommentToken));
            AssertTokenizationValues(result, "emu", "[", "SomeIndexWithoutHash", "Comment]");
        }

        [Test]
        public void ExecutionTest()
        {
            var result = tokenizer.Tokenize("`something`");
            AssertTokenizationTypes(result, typeof(ExecutionToken));
            AssertTokenizationValues(result, "something");
        }

        [Test]
        public void VariableTest()
        {
            var result = tokenizer.Tokenize("$name $_name $NaMe $.name $123name123");
            AssertTokenizationTypes(result, typeof(VariableToken), typeof(VariableToken), typeof(VariableToken), typeof(VariableToken), typeof(VariableToken));
            AssertTokenizationValues(result, "name", "_name", "NaMe", ".name", "123name123");

            result = tokenizer.Tokenize("$variable=\"value\"");
            AssertTokenizationTypes(result, typeof(VariableToken), typeof(EqualityToken), typeof(StringToken));
            AssertTokenizationValues(result, "variable", "=", "value");

            result = tokenizer.Tokenize("$variable?=\"value\"");
            AssertTokenizationTypes(result, typeof(VariableToken), typeof(ConditionalEqualityToken), typeof(StringToken));
            AssertTokenizationValues(result, "variable", "?=", "value");
        }

        [Test]
        public void IndexTest()
        {
            var result = tokenizer.Tokenize("emu[\"SomeIndex\"]");
            AssertTokenizationTypes(result, typeof(LiteralToken), typeof(LeftBraceToken), typeof(StringToken), typeof(RightBraceToken));
            AssertTokenizationValues(result, "emu", "[", "SomeIndex", "]");

            result = tokenizer.Tokenize("emu[15]");
            AssertTokenizationTypes(result, typeof(LiteralToken), typeof(LeftBraceToken), typeof(DecimalIntegerToken), typeof(RightBraceToken));
            AssertTokenizationValues(result, "emu", "[", 15, "]");
        }

        [Test]
        public void StringTest()
        {
            var result = tokenizer.Tokenize("'string1' \"string2\"");
            AssertTokenizationTypes(result, typeof(StringToken), typeof(StringToken));
            AssertTokenizationValues(result, "string1", "string2");

            result = tokenizer.Tokenize("'string1\" 'string2\"");
            AssertTokenizationResult(result, 1, null, typeof(StringToken), typeof(LiteralToken));
            AssertTokenizationValues(result, "string1\" ", "string2");
        }

        [Test]
        public void UnbalancedStringTest()
        {
            var result = tokenizer.Tokenize("\"test\\\"       \\\"         \\\"       test \" 'test\\'        \\'  \\'   test '");
            AssertTokenizationValues(result, "test\"       \"         \"       test ", "test'        '  '   test ");
            AssertTokenizationTypes(result, typeof(StringToken), typeof(StringToken));
        }

        [Test]
        public void RangeTest()
        {
            var result = tokenizer.Tokenize("<    \t0x123abDE  \t, \t\t  0xabcdef0 \t\t   >");
            var expectedValue = new Range(0x123abde, 0xabcdef0 - 0x123abde + 1);
            AssertTokenizationTypes(result, typeof(AbsoluteRangeToken));
            AssertTokenizationValues(result, expectedValue);

            result = tokenizer.Tokenize("<0xdefg, 0xefgh>");
            AssertTokenizationResult(result, 16);
        }

        [Test]
        public void RelativeRangeTest()
        {
            var result = tokenizer.Tokenize("<0x6 0x2>");
            var expectedValue = new Range(0x6, 0x2);
            AssertTokenizationTypes(result, typeof(RelativeRangeToken));
            AssertTokenizationValues(result, expectedValue);
        }

        [Test]
        public void SimplePathTest()
        {
            var result = tokenizer.Tokenize("@Some\\path\\to\\File");
            AssertTokenizationTypes(result, typeof(PathToken));
            AssertTokenizationValues(result, "Some\\path\\to\\File");

            result = tokenizer.Tokenize("@Some\\path\\to\\Directory\\");
            AssertTokenizationTypes(result, typeof(PathToken));
            AssertTokenizationValues(result, "Some\\path\\to\\Directory\\");
        }

        [Test]
        public void EscapedPathTest()
        {
            var result = tokenizer.Tokenize("@Some\\path\\to\\file\\ with\\ Spaces");
            AssertTokenizationTypes(result, typeof(PathToken));
            AssertTokenizationValues(result, "Some\\path\\to\\file with Spaces");

            result = tokenizer.Tokenize("@Some\\path\\to\\directory\\ with\\ Spaces\\");
            AssertTokenizationTypes(result, typeof(PathToken));
            AssertTokenizationValues(result, "Some\\path\\to\\directory with Spaces\\");
        }

        [Test]
        public void MultilineTest()
        {
            var result = tokenizer.Tokenize("\"\"\"");
            AssertTokenizationTypes(result, typeof(MultilineStringTerminatorToken));
            AssertTokenizationValues(result, "\"");

            result = tokenizer.Tokenize("\"\"\"SomeMultiline\r\nString with many #tokens [\"inside\"] with\r\nnumbers 123 0x23 and\r\n stuff\"\"\"");
            AssertTokenizationTypes(result, typeof(MultilineStringToken));
            AssertTokenizationValues(result, "SomeMultiline\r\nString with many #tokens [\"inside\"] with\r\nnumbers 123 0x23 and\r\n stuff");
        }

        [Test]
        public void BooleanTest()
        {
            var result = tokenizer.Tokenize("true");
            AssertTokenizationTypes(result, typeof(BooleanToken));
            AssertTokenizationValues(result, true);

            result = tokenizer.Tokenize("TrUe");
            AssertTokenizationTypes(result, typeof(BooleanToken));
            AssertTokenizationValues(result, true);

            result = tokenizer.Tokenize("FalSE");
            AssertTokenizationTypes(result, typeof(BooleanToken));
            AssertTokenizationValues(result, false);

            result = tokenizer.Tokenize("false");
            AssertTokenizationTypes(result, typeof(BooleanToken));
            AssertTokenizationValues(result, false);
        }

        [Test]
        public void NullTest()
        {
            var result = tokenizer.Tokenize("null");
            AssertTokenizationTypes(result, typeof(NullToken));
            AssertTokenizationValues(result, new object[] { null });
        }

        [Test]
        public void IntegerTest()
        {
            var result = tokenizer.Tokenize("123465 -213245 +132432");
            AssertTokenizationTypes(result, typeof(DecimalIntegerToken), typeof(DecimalIntegerToken), typeof(DecimalIntegerToken));
            AssertTokenizationValues(result, 123465, -213245, 132432);
        }

        [Test]
        public void FloatTest()
        {
            var result = tokenizer.Tokenize("145.5 -0.43 +45.");
            AssertTokenizationTypes(result, typeof(FloatToken), typeof(FloatToken), typeof(FloatToken));
            AssertTokenizationValues(result, 145.5f, -0.43f, 45.0f);
        }

        [Test]
        public void HexadecimalTest()
        {
            var result = tokenizer.Tokenize("0xabcdef 0x123469 0xABCDEF 0x123AbC");
            AssertTokenizationTypes(result, typeof(HexToken), typeof(HexToken), typeof(HexToken), typeof(HexToken));
            AssertTokenizationValues(result, 0xabcdef, 0x123469, 0xabcdef, 0x123abc);

            result = tokenizer.Tokenize("0xgfd 123bcd");
            AssertTokenizationTypes(result, typeof(DecimalIntegerToken), typeof(LiteralToken), typeof(DecimalIntegerToken), typeof(LiteralToken));
            AssertTokenizationValues(result, 0, "xgfd", 123, "bcd");
        }

        public void LiteralTest()
        {
            var result = tokenizer.Tokenize(".Some.Literal-With?Extra:SignsIn.It:");
            AssertTokenizationTypes(result, typeof(LiteralToken));
            AssertTokenizationValues(result, ".Some.Literal-With?Extra:SignsIn.It:");
        }

        [SetUp]
        public void TestSetUp()
        {
            tokenizer = Tokenizer.CreateTokenizer();
        }

        private static void AssertTokenizationResult(TokenizationResult result, int unmatchedCharacters, Type exception = null, params Type[] types)
        {
            if(exception != null)
            {
                Assert.AreEqual(result.Exception.GetType(), exception);
            }
            else
            {
                Assert.IsNull(result.Exception);
            }
            Assert.IsTrue(result.UnmatchedCharactersLeft == unmatchedCharacters);
            Assert.IsNotNull(result.Tokens);
            var tokens = result.Tokens.ToArray();
            Assert.AreEqual(tokens.Length, types.Length);
            for(var i = 0; i < tokens.Length; ++i)
            {
                Assert.AreSame(tokens[i].GetType(), types[i]);
            }
        }

        private static void AssertTokenizationTypes(TokenizationResult result, params Type[] types)
        {
            Assert.IsNull(result.Exception);
            Assert.IsTrue(result.UnmatchedCharactersLeft == 0);
            Assert.IsNotNull(result.Tokens);
            var tokens = result.Tokens.ToArray();
            Assert.AreEqual(tokens.Length, types.Length);
            for(var i = 0; i < tokens.Length; ++i)
            {
                Assert.AreSame(tokens[i].GetType(), types[i]);
            }
        }

        private static void AssertTokenizationValues(TokenizationResult result, params object[] values)
        {
            var tokens = result.Tokens.ToArray();
            Assert.AreEqual(tokens.Length, values.Length);
            CollectionAssert.AreEqual(values, tokens.Select(x => x.GetObjectValue()));
        }

        private Tokenizer tokenizer;
    }
}

