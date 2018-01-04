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
using System.Linq;

namespace Antmicro.Renode.UnitTests.SymbolLookupTests
{
    [TestFixture]
    public class DeduplicationTests
    {
        [Test]
        public void ShouldDedupliacteIdenticalSymbols()
        {
            var symbols = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(80, 85, "猫"),
                new Symbol(3, 60, "中"),
                new Symbol(70, 75, "私"),
                new Symbol(4, 15, "五"),
                new Symbol(20, 35, "三"),
            };
            var symbolCopies = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(80, 85, "猫"),
                new Symbol(3, 60, "中"),
                new Symbol(70, 75, "私"),
                new Symbol(4, 15, "五"),
                new Symbol(20, 35, "三"),
            };
            var lookup1 = new SymbolLookup();
            var lookup2 = new SymbolLookup();
            lookup1.InsertSymbols(symbols);
            lookup2.InsertSymbols(symbolCopies);

            for(int i = 0; i < symbols.Count; ++i)
            {
                Assert.AreSame(
                    lookup1.GetSymbolsByName(symbols[i].Name).First(),
                    lookup2.GetSymbolsByName(symbolCopies[i].Name).First(),
                    string.Format("Symbol {0}, has not been deduplicated.", i)
                );
            }
        }

        [Test]
        public void ShouldNotDedupliacteIfThereAreTwoDifferentSymbolsWithSameNameAndInterval()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(20, 35, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(20, 35, "一"),
            };
            var lookup1 = new SymbolLookup();
            var lookup2 = new SymbolLookup();
            lookup1.InsertSymbols(symbols1);
            lookup2.InsertSymbols(symbols2);

            var symbol = symbols1[0];
            Assert.AreNotSame(
                lookup1.GetSymbolsByName(symbol.Name).First(),
                lookup2.GetSymbolsByName(symbol.Name).First(),
                string.Format("Symbol {0} has been deduplicated while it shouldn't.", symbol)
            );
            symbol = symbols1[1];
            Assert.AreNotSame(
                lookup1.GetSymbolByAddress(symbol.Start),
                lookup2.GetSymbolByAddress(symbol.Start),
                string.Format("Symbol {0} has been deduplicated while it shouldn't.", symbol)
            );
        }

        [Test]
        public void ShouldDedupliacteAfterMerge()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(20, 35, "三"),
                new Symbol(100, 105, "猫"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(15, 25, "私"),
            };
            var lookup1 = new SymbolLookup();
            var lookup2 = new SymbolLookup();
            lookup1.InsertSymbols(symbols1);
            lookup2.InsertSymbols(symbols1);
            lookup1.InsertSymbols(symbols2);

            var symbol = symbols1[2];
            Assert.AreSame(
                lookup1.GetSymbolsByName(symbol.Name).First(),
                lookup2.GetSymbolsByName(symbol.Name).First(),
                string.Format("Symbol {0} has NOT been deduplicated.", symbol)
            );

            symbol = symbols1[1];
            Assert.AreNotSame(
                lookup1.GetSymbolsByName(symbol.Name).First(),
                lookup2.GetSymbolsByName(symbol.Name).First(),
                string.Format("Symbol {0} has been deduplicated while it shouldn't.", symbol)
            );
        }
    }
}

