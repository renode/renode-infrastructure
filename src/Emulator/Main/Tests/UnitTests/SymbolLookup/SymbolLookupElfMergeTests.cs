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
    public class ElfMergeTests
    {
        [Test]
        public void ShouldMergeTrimEndAndBeginning()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 50, "一"),
                new Symbol(50, 100, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(30, 70, "二"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 30, "一"),
                new Symbol(30, 70, "二"),
                new Symbol(70, 100, "三"),
            };
            var addressesToQuery = new List<uint>{25, 35, 75};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldSortedIntervalsHaveExpectedLayout()
        {
            var symbols1 = new List<Symbol>
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
            var symbols2 = new List<Symbol>
            {
                new Symbol(5, 25, "糞"),
                new Symbol(40, 82, "二"),
                new Symbol(50, 55, "ICantSpeekJapaneese"),
                new Symbol(45, 50, "ICantSpeekKorean"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 5, "一"),
                new Symbol(1, 5, "四"),
                new Symbol(2, 5, "国"),
                new Symbol(3, 5, "中"),
                new Symbol(4, 5, "五"),
                new Symbol(5, 25, "糞"),
                new Symbol(25, 40, "中"),
                new Symbol(25, 35, "三"),
                new Symbol(40, 82, "二"),
                new Symbol(45, 50, "ICantSpeekKorean"),
                new Symbol(50, 55, "ICantSpeekJapaneese"),
                new Symbol(82, 100, "一"),
                new Symbol(82, 85, "猫"),
            };
            var addressesToQuery = new List<uint>{0, 1, 2, 3, 4, 10, 37, 30, 42, 47, 53, 90, 83};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(
                expectedSymbols,
                addressesToQuery.Select(address => lookup.GetSymbolByAddress(address))
            );
        }

        [Test]
        public void ShouldTrimEndAndBeginning2()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(30, 70, "二"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(0, 40, "一"),
                new Symbol(60, 100, "三"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 40, "一"),
                new Symbol(40, 60, "二"),
                new Symbol(60, 100, "三"),
            };
            var addressesToQuery = new List<uint>{25, 59, 60};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));

        }

        [Test]
        public void ShouldTrimCakeEnds()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(3, 60, "中"),
                new Symbol(4, 60, "五"),
                new Symbol(20, 50, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(50, 100, "猫"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 50, "一"),
                new Symbol(1, 50, "四"),
                new Symbol(2, 50, "国"),
                new Symbol(3, 50, "中"),
                new Symbol(4, 50, "五"),
                new Symbol(20, 50, "三"),
                new Symbol(50, 100, "猫"),
            };
            var addressesToQuery = new List<uint>{0, 1, 2, 3, 15, 35, 99};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldTrimCakeBeginnings()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(3, 60, "中"),
                new Symbol(20, 50, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(0, 30, "猫"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 30, "猫"),
                new Symbol(30, 100, "一"),
                new Symbol(30, 85, "四"),
                new Symbol(30, 70, "国"),
                new Symbol(30, 60, "中"),
                new Symbol(30, 50, "三"),
            };
            var addressesToQuery = new List<uint>{10, 95, 80, 65, 55, 45};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldCutIntoCake()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(3, 60, "中"),
                new Symbol(20, 50, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(35, 40, "猫"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 35, "一"),
                new Symbol(1, 35, "四"),
                new Symbol(2, 35, "国"),
                new Symbol(3, 35, "中"),
                new Symbol(20, 35, "三"),
                new Symbol(35, 40, "猫"),
                new Symbol(40, 100, "一"),
                new Symbol(40, 85, "四"),
                new Symbol(40, 70, "国"),
                new Symbol(40, 60, "中"),
                new Symbol(40, 50, "三"),
            };
            var addressesToQuery = new List<uint>{0, 1, 2, 10, 25, 37, 90, 80, 65, 55, 45};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldTrimCakeToTopSymbol()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 85, "四"),
                new Symbol(2, 70, "国"),
                new Symbol(3, 60, "中"),
                new Symbol(4, 60, "五"),
                new Symbol(20, 50, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(0, 30, "猫"),
                new Symbol(40, 100, "糞"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 30, "猫"),
                new Symbol(30, 40, "三"),
                new Symbol(40, 100, "糞"),
            };
            var addressesToQuery = new List<uint>{10, 37, 80};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldTrimAndOvershadow()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 10, "一"),
                new Symbol(1, 8, "四"),
                new Symbol(20, 40, "国"),
                new Symbol(30, 40, "中"),
                new Symbol(40, 60, "五"),
                new Symbol(45, 55, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(5, 50, "猫"),
                new Symbol(10, 15, "糞"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 5, "一"),
                new Symbol(1, 5, "四"),
                new Symbol(5, 50, "猫"),
                new Symbol(10, 15, "糞"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var addressesToQuery = new List<uint>{0, 1, 45, 12, 59, 52};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldSplitCake()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 5, "一"),
                new Symbol(1, 4, "四"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(2, 2, "猫"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 2, "一"),
                new Symbol(1, 2, "四"),
                new Symbol(2, 2, "猫"),
                new Symbol(2, 4, "四"),
                new Symbol(2, 5, "一"),
            };
            var addressesToQuery = new List<uint>{0, 1, 2, 3, 4};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldTrimAndOverShadowOnABase()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 8, "四"),
                new Symbol(20, 40, "国"),
                new Symbol(30, 40, "中"),
                new Symbol(40, 60, "五"),
                new Symbol(45, 55, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(5, 50, "猫"),
                new Symbol(10, 15, "糞"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 5, "一"),
                new Symbol(1, 5, "四"),
                new Symbol(5, 50, "猫"),
                new Symbol(10, 15, "糞"),
                new Symbol(50, 100, "一"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var addressesToQuery = new List<uint>{0, 3, 5, 14, 60, 59, 51};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldSplitCakeAfterSecondTower()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 100, "一"),
                new Symbol(1, 8, "四"),
                new Symbol(20, 40, "国"),
                new Symbol(30, 40, "中"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(42, 47, "猫"),
                new Symbol(43, 43, "糞"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(0, 42, "一"),
                new Symbol(1, 8, "四"),
                new Symbol(20, 40, "国"),
                new Symbol(30, 40, "中"),
                new Symbol(42, 47, "猫"),
                new Symbol(43, 43, "糞"),
                new Symbol(47, 100, "一"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var addressesToQuery = new List<uint>{41, 5, 25, 35, 45, 43, 48, 58, 50};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldHaveSlicedSymbolInNameLookup()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(20, 40, "国"),
                new Symbol(30, 40, "中"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(30, 35, "中"),
            };
            var expectedSymbols = new List<Symbol> {
                new Symbol(20, 30, "国"),
                new Symbol(30, 35, "中"),
                new Symbol(35, 40, "中"),
                new Symbol(50, 60, "五"),
                new Symbol(50, 55, "三"),
            };
            var addressesToQuery = new List<uint>{20, 30, 35, 58, 50};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
            var name = expectedSymbols[1].Name;
            Assert.AreEqual(expectedSymbols[1], lookup.GetSymbolsByName(name).ElementAt(0));
            Assert.AreEqual(expectedSymbols[2], lookup.GetSymbolsByName(name).ElementAt(1));
        }

        [Test]
        public void ShouldPlaceZeroLenghtSymbolBeforeCake()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(50, 100, "一"),
                new Symbol(50, 75, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(50, 50, "二"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(50, 100, "一"),
                new Symbol(50, 75, "三"),
                new Symbol(50, 50, "二"),
            };
            var addressesToQuery = new List<uint>{90, 55, 50};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldCoverZeroLenghtSymbol()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(50, 50, "二"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(50, 100, "一"),
                new Symbol(50, 75, "三"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(50, 100, "一"),
                new Symbol(50, 75, "三"),
            };
            var addressesToQuery = new List<uint>{90, 55};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
            IReadOnlyCollection<Symbol> symbols;
            Assert.IsFalse(lookup.TryGetSymbolsByName("二", out symbols), "Symbol \"二\"should not be present.");
        }

        [Test]
        public void ShouldZeroLenghtSymbolCutAfterCake()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(0, 50, "一"),
                new Symbol(10, 50, "三"),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(50, 50, "二"),
            };
            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 50, "一"),
                new Symbol(10, 50, "三"),
                new Symbol(50, 50, "二"),
            };
            var addressesToQuery = new List<uint>{0, 15, 50};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldNotDestroyOtherSymbolLookupCache()
        {
            var symbols1 = new List<Symbol>
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
            var symbols2 = new List<Symbol>
            {
                new Symbol(5, 25, "糞"),
                new Symbol(40, 82, "二"),
                new Symbol(50, 55, "ICantSpeekJapaneese"),
                new Symbol(45, 50, "ICantSpeekKorean"),
            };
            var mergedSymbols = new List<Symbol>
            {
                new Symbol(0, 5, "一"),
                new Symbol(1, 5, "四"),
                new Symbol(2, 5, "国"),
                new Symbol(3, 5, "中"),
                new Symbol(4, 5, "五"),
                new Symbol(5, 25, "糞"),
                new Symbol(25, 40, "中"),
                new Symbol(25, 35, "三"),
                new Symbol(40, 82, "二"),
                new Symbol(45, 50, "ICantSpeekKorean"),
                new Symbol(50, 55, "ICantSpeekJapaneese"),
                new Symbol(82, 100, "一"),
                new Symbol(82, 85, "猫"),
            };
            var addressesToQuery = new List<uint>{0, 1, 2, 3, 4, 10, 37, 30, 42, 47, 53, 90, 83};
            var lookup = new List<SymbolLookup> {new SymbolLookup(), new SymbolLookup()};
            lookup[0].InsertSymbols(symbols1);
            lookup[1].InsertSymbols(symbols1);
            lookup[0].InsertSymbols(symbols2);
            CollectionAssert.AreEqual(
                mergedSymbols,
                addressesToQuery.Select(address => lookup[0].GetSymbolByAddress(address))
            );
            CollectionAssert.AreEqual(
                symbols1,
                symbols1.Select(symbol => lookup[1].GetSymbolByAddress(symbol.Start))
            );
        }

        /// <summary>
        /// Regression test. "三" was removed and wasn't added.
        /// </summary>
        [Test]
        public void ShouldPerserveSymbol()
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
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);

            var symbol = symbols1[1];
            IReadOnlyCollection<Symbol> symbols;
            Assert.IsTrue(
                lookup.TryGetSymbolsByName(symbol.Name, out symbols),
                string.Format("Symbol {0} has NOT been deduplicated.", symbol)
            );
        }

        /// <summary>
        /// This is a regression test. Previously such inserts resulted in infinite loop.
        /// </summary>
        [Test]
        public void ShouldMergeOverlappingThumbSymbols()
        {
            var symbols1 = new List<Symbol>
            {
                new Symbol(10, 20, "一", ELFSharp.ELF.Sections.SymbolType.NotSpecified, ELFSharp.ELF.Sections.SymbolBinding.Global, true),
            };
            var symbols2 = new List<Symbol>
            {
                new Symbol(5, 15, "悪魔", ELFSharp.ELF.Sections.SymbolType.NotSpecified, ELFSharp.ELF.Sections.SymbolBinding.Global, true),
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols1);
            lookup.InsertSymbols(symbols2);
        }

        [Test]
        public void ShouldFavorUnempty()
        {
            var symbols = new List<Symbol>
            {
                new Symbol(0, 10, "Unempty"),
                new Symbol(0, 10, ""),
                new Symbol(10, 20, "Other"),
            };

            var expectedSymbols = new List<Symbol>
            {
                new Symbol(0, 10, "Unempty"),
                new Symbol(10, 20, "Other"),
            };

            var addressesToQuery = new List<uint>{0, 10};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            CollectionAssert.AreEqual(expectedSymbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }
    }
}
