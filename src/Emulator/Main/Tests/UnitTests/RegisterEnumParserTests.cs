//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using NUnit.Framework;
using System.IO;

namespace Antmicro.Renode.CoresSourceParser
{
    [TestFixture]
    public class RegisterEnumParserTests
    {
        [Test]
        public void ShouldParseEnumWithSimpleValue()
        {
            var stream = GetStreamWitString(@"typedef enum {
                A_32 = 0
            } Registers;");

            var parser = new RegistersEnumParser(stream);
            Assert.AreEqual(1, parser.Registers.Length);
            Assert.AreEqual(0, parser.RegisterGroups.Length);

            Assert.AreEqual("A", parser.Registers[0].Name);
            Assert.AreEqual(32, parser.Registers[0].Width);
            Assert.AreEqual(0, parser.Registers[0].Value);
        }

        [Test]
        public void ShouldParseEnumWithMultipleValues()
        {
            var stream = GetStreamWitString(@"typedef enum {
                A_32 = 0,
                B_64 = 1,
                C_128 = 2
            } Registers;");

            var parser = new RegistersEnumParser(stream);
            Assert.AreEqual(3, parser.Registers.Length);
            Assert.AreEqual(0, parser.RegisterGroups.Length);

            Assert.AreEqual("A", parser.Registers[0].Name);
            Assert.AreEqual(32, parser.Registers[0].Width);
            Assert.AreEqual(0, parser.Registers[0].Value);

            Assert.AreEqual("B", parser.Registers[1].Name);
            Assert.AreEqual(64, parser.Registers[1].Width);
            Assert.AreEqual(1, parser.Registers[1].Value);

            Assert.AreEqual("C", parser.Registers[2].Name);
            Assert.AreEqual(128, parser.Registers[2].Width);
            Assert.AreEqual(2, parser.Registers[2].Value);
        }

        [Test]
        public void ShouldParseEnumWithUndefinedIfdef()
        {
            var stream = GetStreamWitString(@"typedef enum {
                A_32 = 0,
                #ifdef XYZ
                B_64 = 1,
                #endif
                C_128 = 2
            } Registers;");

            var parser = new RegistersEnumParser(stream);
            Assert.AreEqual(2, parser.Registers.Length);
            Assert.AreEqual(0, parser.RegisterGroups.Length);

            Assert.AreEqual("A", parser.Registers[0].Name);
            Assert.AreEqual(32, parser.Registers[0].Width);
            Assert.AreEqual(0, parser.Registers[0].Value);

            Assert.AreEqual("C", parser.Registers[1].Name);
            Assert.AreEqual(128, parser.Registers[1].Width);
            Assert.AreEqual(2, parser.Registers[1].Value);
        }
        
        [Test]
        public void ShouldParseEnumWithDefinedIfdef()
        {
            var stream = GetStreamWitString(@"typedef enum {
                A_32 = 0,
                #ifdef XYZ
                B_64 = 1,
                #endif
                C_128 = 2
            } Registers;");

            var parser = new RegistersEnumParser(stream, new [] { "XYZ" });
            Assert.AreEqual(3, parser.Registers.Length);
            Assert.AreEqual(0, parser.RegisterGroups.Length);

            Assert.AreEqual("A", parser.Registers[0].Name);
            Assert.AreEqual(32, parser.Registers[0].Width);
            Assert.AreEqual(0, parser.Registers[0].Value);

            Assert.AreEqual("B", parser.Registers[1].Name);
            Assert.AreEqual(64, parser.Registers[1].Width);
            Assert.AreEqual(1, parser.Registers[1].Value);

            Assert.AreEqual("C", parser.Registers[2].Name);
            Assert.AreEqual(128, parser.Registers[2].Width);
            Assert.AreEqual(2, parser.Registers[2].Value);
        }

        [Test]
        public void ShouldParseEnumWithMultipleValuesWithIndices()
        {
            var stream = GetStreamWitString(@"typedef enum {
                A_1_32 = 15,
                A_2_32 = 16,
                A_3_32 = 17
            } Registers;");

            var parser = new RegistersEnumParser(stream);
            Assert.AreEqual(0, parser.Registers.Length);
            Assert.AreEqual(1, parser.RegisterGroups.Length);

            Assert.AreEqual("A", parser.RegisterGroups[0].Name);
            Assert.AreEqual(32, parser.RegisterGroups[0].Width);
            Assert.AreEqual(3, parser.RegisterGroups[0].IndexValueMap.Count);
            Assert.AreEqual(15, parser.RegisterGroups[0].IndexValueMap[1]);
            Assert.AreEqual(16, parser.RegisterGroups[0].IndexValueMap[2]);
            Assert.AreEqual(17, parser.RegisterGroups[0].IndexValueMap[3]);
        }

        [Test]
        public void ShouldParseEnumWithCommentOrEmptyLines()
        {
            var stream = GetStreamWitString(@"typedef enum {

                // Some comment
                A_1_32 = 15,
                
                A_2_32 = 16,
                //Another comment
                A_3_32 = 17
            } Registers;");

            var parser = new RegistersEnumParser(stream);
            Assert.AreEqual(0, parser.Registers.Length);
            Assert.AreEqual(1, parser.RegisterGroups.Length);
        }

        private static Stream GetStreamWitString(string text)
        {
            var result = new MemoryStream();
            var writer = new StreamWriter(result);
            writer.WriteLine(text);
            writer.Flush();
            result.Seek(0, SeekOrigin.Begin);

            return result;
        }
    }
}

