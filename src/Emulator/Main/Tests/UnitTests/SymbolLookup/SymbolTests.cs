//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using NUnit.Framework;
using Antmicro.Renode.Core;
using System.Collections.Generic;

namespace Antmicro.Renode.UnitTests.SymbolLookupTests
{
    [TestFixture]
    public class SymbolTests
    {
        [Test]
        public void ShouldOverlap()
        {
            var symbol = new List<Symbol> {
                new Symbol(0, 10, "一"),
                new Symbol(5, 15, "二"),
                new Symbol(5, 30, "三"),
                new Symbol(15, 30, "四"),
                new Symbol(15, 15, "五"),
            };
            Assert.True(symbol[0].Overlaps(symbol[1]));
            Assert.True(symbol[1].Overlaps(symbol[0]));
            Assert.True(symbol[2].Overlaps(symbol[1]));
            Assert.True(symbol[1].Overlaps(symbol[2]));
            Assert.True(symbol[2].Overlaps(symbol[3]));
            Assert.True(symbol[3].Overlaps(symbol[2]));
            Assert.True(symbol[3].Overlaps(symbol[4]));
            Assert.True(symbol[4].Overlaps(symbol[3]));
        }

        [Test]
        public void ShouldNotOverlap()
        {
            var symbol = new List<Symbol> {
                new Symbol(0, 10, "一"),
                new Symbol(5, 15, "二"),
                new Symbol(5, 30, "三"),
                new Symbol(15, 30, "四"),
                new Symbol(10, 20, "五"),
            };
            Assert.False(symbol[0].Overlaps(symbol[4]));
            Assert.False(symbol[4].Overlaps(symbol[0]));
            Assert.False(symbol[0].Overlaps(symbol[3]));
            Assert.False(symbol[3].Overlaps(symbol[0]));
            Assert.False(symbol[1].Overlaps(symbol[3]));
            Assert.False(symbol[3].Overlaps(symbol[1]));
        }

        [Test]
        public void ShouldContain()
        {
            var symbol = new List<Symbol> {
                new Symbol(5, 30, "三"),
                new Symbol(5, 15, "二"),
                new Symbol(15, 30, "四"),
                new Symbol(10, 20, "五"),
                new Symbol(15, 15, "一"),
            };
            Assert.True(symbol[0].Contains(symbol[0]));
            Assert.True(symbol[0].Contains(symbol[1]));
            Assert.True(symbol[0].Contains(symbol[2]));
            Assert.True(symbol[0].Contains(symbol[3]));
            Assert.True(symbol[2].Contains(symbol[4]));
        }

        [Test]
        public void ShouldNotContain()
        {
            var symbol = new List<Symbol> {
                new Symbol(5, 30, "三"),
                new Symbol(5, 15, "二"),
                new Symbol(15, 30, "四"),
                new Symbol(10, 20, "五"),
                new Symbol(15, 15, "一"),
            };
            Assert.False(symbol[1].Contains(symbol[0]));
            Assert.False(symbol[2].Contains(symbol[0]));
            Assert.False(symbol[3].Contains(symbol[0]));
            Assert.False(symbol[1].Contains(symbol[2]));
            Assert.False(symbol[2].Contains(symbol[1]));
            Assert.False(symbol[1].Contains(symbol[3]));
            Assert.False(symbol[3].Contains(symbol[1]));
            Assert.False(symbol[1].Contains(symbol[4]));
        }

        [Test]
        public void ShouldDemangle()
        {
            var symbol = new List<Symbol> {
                new Symbol(0,0, "_ZN2cl2pl4consC1ERKSs"),
                new Symbol(0,0, "_Z1hic"),
                new Symbol(0,0, "?h@@YAXHD@Z" ),
                new Symbol(0,0, "_ZN4Test6Ne$ted12staticMemberE3abc" ),
                new Symbol(0,0, "_ZNSt8ios_base4InitC1Ev@plt"),
                new Symbol(0,0, "_ZN6tflite12_GLOBAL__N_14EvalEP13TfLiteContextP10TfLiteNode")
            };
            Assert.AreEqual("cl::pl::cons::complete object constructor", symbol[0].Name);
            Assert.AreEqual("h", symbol[1].Name);
            Assert.AreEqual("?h@@YAXHD@Z", symbol[2].Name);
            Assert.AreEqual("Test::Ne$ted::staticMember", symbol[3].Name);
            Assert.AreEqual("std::ios_base::Init::complete object constructor", symbol[4].Name);
            Assert.AreEqual("tflite::(anonymous namespace)::Eval", symbol[5].Name);
        }
    }
}

