//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using NUnit.Framework;
using System.Collections.Generic;
using ELFSharp.ELF.Sections;


namespace Antmicro.Renode.UnitTests.SymbolLookupTests
{
    [TestFixture]
    public class SymbolLookupTests
    {
        [Test]
        public void ShouldHaveMoreAndLessImportantViaName()
        {
            var lookup = new SymbolLookup();
            var symbols = new List<Symbol>
            {
                new Symbol(0, 10, "LessImportant"),
                new Symbol(0, 10, "MoreImportant", SymbolType.Function)
            };
            IReadOnlyCollection<Symbol> dummy;
            lookup.InsertSymbols(symbols);
            Assert.IsTrue(lookup.TryGetSymbolsByName("MoreImportant", out dummy));
            Assert.IsTrue(lookup.TryGetSymbolsByName("LessImportant", out dummy));
        }

        [Test]
        public void ShouldHaveImportantViaAddress()
        {
            int symbolNumber = 0;

            var symbols = new List<Symbol>();
            //reverse order according to type importance. This is not optimal, but the actual
            //importance is private to Symbol type.
            foreach(var type in new []{ SymbolType.Function,
                SymbolType.NotSpecified,
                SymbolType.ProcessorSpecific,
                SymbolType.Section,
                SymbolType.Object,
                SymbolType.File })
            {
                foreach(var binding in new []{ SymbolBinding.Global,
                    SymbolBinding.Local,
                    SymbolBinding.ProcessorSpecific,
                    SymbolBinding.Weak })
                {
                    symbols.Add(new Symbol(0, 10, symbolNumber.ToString(), type, binding));
                    symbolNumber++;
                }
            }
            //check every symbol. The symbol in question and all LESS important are added, both in order and in reverse order.
            for(var i = 0; i < symbols.Count; ++i)
            {
                var lookup = new SymbolLookup();
                lookup.InsertSymbols(symbols.Skip(i));
                Assert.AreEqual(symbols[i], lookup.GetSymbolByAddress(5));
                lookup = new SymbolLookup();
                lookup.InsertSymbols(symbols.Skip(i).Reverse());
                Assert.AreEqual(symbols[i], lookup.GetSymbolByAddress(5));
            }
        }

        [Test]
        public void ShouldFindOneSymbol()
        {
            var lookup = new SymbolLookup();
            lookup.InsertSymbol("Test", 0x100, 0x10);
            Assert.AreEqual("Test", lookup.GetSymbolByAddress(0x105).Name);
        }

        [Test]
        public void ShouldFindOneSymbolBoundary()
        {
            var lookup = new SymbolLookup();
            lookup.InsertSymbol("Test", 0x100, 0x10);
            Assert.AreEqual("Test", lookup.GetSymbolByAddress(0x100).Name);
        }

        [Test]
        public void ShouldNotFindOneSymbolBoundary()
        {
            var lookup = new SymbolLookup();
            lookup.InsertSymbol("Test", 0x100, 0x10);
            Symbol dummy;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(0x110, out dummy));
        }

        [Test]
        public void ShouldNotFindOneSymbolMiss()
        {
            var lookup = new SymbolLookup();
            lookup.InsertSymbol("Test", 0x100, 0x10);
            Symbol dummy;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(0x120, out dummy));
        }

        [Test]
        public void ShouldTrimBigSymbol()
        {
            Symbol dummy;
            var lookup = new SymbolLookup();
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Large", 0, 10),
                MakeSymbolEntry("Small", 9, 1)
            };
            lookup.InsertSymbols(symbols);

            Assert.IsFalse(lookup.TryGetSymbolByAddress(10, out dummy));
            Assert.AreEqual("Small", lookup.GetSymbolByAddress(9).Name);
            Assert.AreEqual("Large", lookup.GetSymbolByAddress(8).Name);
            Assert.AreEqual("Large", lookup.GetSymbolByAddress(0).Name);
        }

        [Test]
        public void ShouldFindTenSymbols()
        {
            var symbols = Enumerable.Range(1, 10).Select(x => MakeSymbolEntry(x.ToString(), (uint)(x * 10), 5)).ToList();
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            foreach (var symbol in symbols)
            {
                Assert.AreEqual(symbol.Name, lookup.GetSymbolByAddress(symbol.Start + 2).Name);
            }
        }

        [Test]
        public void ShouldFindSimplyNestedSymbol()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Big", 0, 100),
                MakeSymbolEntry("Small", 50, 10)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(10).Name);
            Assert.AreEqual("Small", lookup.GetSymbolByAddress(51).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(60).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(61).Name);
        }

        [Test]
        public void ShouldFindRecursivelyNestedSymbol()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Big", 0, 100),
                MakeSymbolEntry("Average", 40, 20),
                MakeSymbolEntry("Small", 50, 5)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("Big", lookup.GetSymbolByAddress(10).Name);
            Assert.AreEqual("Average", lookup.GetSymbolByAddress(41).Name);
            Assert.AreEqual("Small", lookup.GetSymbolByAddress(51).Name);
            Assert.AreEqual("Average", lookup.GetSymbolByAddress(55).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(60).Name);
        }

        [Test]
        public void ShouldNotFindHole()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Alice", 0, 50),
                MakeSymbolEntry("Bob", 51, 50)
            };
            var lookup = new SymbolLookup();

            lookup.InsertSymbols(symbols);

            Symbol dummy;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(50, out dummy));
        }

        [Test]
        public void ShouldFindDoubleCut()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Big", 0, 100),
                MakeSymbolEntry("Alice", 10, 20),
                MakeSymbolEntry("Bob", 70, 20)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(5).Name);
            Assert.AreEqual("Alice", lookup.GetSymbolByAddress(15).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(35).Name);
            Assert.AreEqual("Bob", lookup.GetSymbolByAddress(75).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(95).Name);
        }

        [Test]
        public void ShouldFindNestedAndDoubleCut()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Big", 0, 100),
                MakeSymbolEntry("Alice", 10, 30),
                MakeSymbolEntry("Small1", 20, 5),
                MakeSymbolEntry("Small2", 30, 5),
                MakeSymbolEntry("Bob", 60, 30),
                MakeSymbolEntry("Small3", 70, 5)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("Big", lookup.GetSymbolByAddress(5).Name);
            Assert.AreEqual("Alice", lookup.GetSymbolByAddress(15).Name);
            Assert.AreEqual("Small1", lookup.GetSymbolByAddress(22).Name);
            Assert.AreEqual("Alice", lookup.GetSymbolByAddress(26).Name);
            Assert.AreEqual("Small2", lookup.GetSymbolByAddress(31).Name);
            Assert.AreEqual("Alice", lookup.GetSymbolByAddress(36).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(41).Name);
            Assert.AreEqual("Bob", lookup.GetSymbolByAddress(61).Name);
            Assert.AreEqual("Small3", lookup.GetSymbolByAddress(71).Name);
            Assert.AreEqual("Bob", lookup.GetSymbolByAddress(76).Name);
            Assert.AreEqual("Big", lookup.GetSymbolByAddress(91).Name);
        }

        [Test]
        public void ShouldWorkWithLongSeriesAndNesting()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("Large", 0, 10),
                MakeSymbolEntry("First", 3, 3),
                MakeSymbolEntry("Small", 4, 1),
                MakeSymbolEntry("Last", 6, 1)
            };

            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Symbol dummy;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(10, out dummy));
            Assert.AreEqual("Large", lookup.GetSymbolByAddress(9).Name);
            Assert.AreEqual("Last", lookup.GetSymbolByAddress(6).Name);
            Assert.AreEqual("First", lookup.GetSymbolByAddress(5).Name);
            Assert.AreEqual("Small", lookup.GetSymbolByAddress(4).Name);
            Assert.AreEqual("First", lookup.GetSymbolByAddress(3).Name);
            Assert.AreEqual("Large", lookup.GetSymbolByAddress(2).Name);
        }

        [Test]
        public void ShouldFindNestedDeeper()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("First", 0, 100),
                MakeSymbolEntry("Second", 10, 80),
                MakeSymbolEntry("Third", 20, 60),
                MakeSymbolEntry("Fourth", 30, 40)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("First", lookup.GetSymbolByAddress(5).Name);
            Assert.AreEqual("Second", lookup.GetSymbolByAddress(15).Name);
            Assert.AreEqual("Third", lookup.GetSymbolByAddress(25).Name);
            Assert.AreEqual("Fourth", lookup.GetSymbolByAddress(35).Name);
            Assert.AreEqual("Third", lookup.GetSymbolByAddress(75).Name);
            Assert.AreEqual("Second", lookup.GetSymbolByAddress(85).Name);
            Assert.AreEqual("First", lookup.GetSymbolByAddress(95).Name);
        }

        [Test]
        public void ShouldFindMatroska()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("First", 90, 10),
                MakeSymbolEntry("Second", 80, 20),
                MakeSymbolEntry("Third", 70, 30),
                MakeSymbolEntry("Fourth", 60, 40),
                MakeSymbolEntry("Fifth", 85, 5),
                MakeSymbolEntry("Sixth", 40, 20),
                MakeSymbolEntry("Seventh", 0, 100)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("Seventh", lookup.GetSymbolByAddress(0).Name);
            Assert.AreEqual("Sixth", lookup.GetSymbolByAddress(45).Name);
            Assert.AreEqual("Fifth", lookup.GetSymbolByAddress(85).Name);
            Assert.AreEqual("Fourth", lookup.GetSymbolByAddress(65).Name);
            Assert.AreEqual("Third", lookup.GetSymbolByAddress(75).Name);
            Assert.AreEqual("Second", lookup.GetSymbolByAddress(80).Name);
            Assert.AreEqual("First", lookup.GetSymbolByAddress(90).Name);
        }

        [Test]
        public void ShouldFindComplicatedTest()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 0, 100),
                MakeSymbolEntry("二", 10, 40),
                MakeSymbolEntry("三", 15, 15),
                MakeSymbolEntry("四", 30, 15),
                MakeSymbolEntry("五", 55, 5),
                MakeSymbolEntry("中", 60, 10),
                MakeSymbolEntry("国", 50, 20),
                MakeSymbolEntry("猫", 80, 15),
                MakeSymbolEntry("私", 85, 5),
                MakeSymbolEntry("糞", 100, 20),
                MakeSymbolEntry("ICantSpeekJapaneese", 56, 2),
                MakeSymbolEntry("KoreanNeither", 58, 2)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);


            Assert.AreEqual("一", lookup.GetSymbolByAddress(1).Name);
            Assert.AreEqual("二", lookup.GetSymbolByAddress(11).Name);
            Assert.AreEqual("三", lookup.GetSymbolByAddress(16).Name);
            Assert.AreEqual("四", lookup.GetSymbolByAddress(31).Name);
            Assert.AreEqual("二", lookup.GetSymbolByAddress(46).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(52).Name);
            Assert.AreEqual("五", lookup.GetSymbolByAddress(55).Name);
            Assert.AreEqual("中", lookup.GetSymbolByAddress(64).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(72).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(84).Name);
            Assert.AreEqual("私", lookup.GetSymbolByAddress(86).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(94).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(95).Name);
            Assert.AreEqual("糞", lookup.GetSymbolByAddress(100).Name);

            Assert.AreEqual("ICantSpeekJapaneese", lookup.GetSymbolByAddress(57).Name);
            Assert.AreEqual("KoreanNeither", lookup.GetSymbolByAddress(59).Name);
        }

        [Test]
        public void ShouldFindComplicatedFromSingleInsertsTest()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 0, 100),
                MakeSymbolEntry("二", 10, 40),
                MakeSymbolEntry("三", 15, 15),
                MakeSymbolEntry("四", 30, 15),
                MakeSymbolEntry("五", 55, 5),
                MakeSymbolEntry("中", 60, 10),
                MakeSymbolEntry("国", 50, 20),
                MakeSymbolEntry("猫", 80, 15),
                MakeSymbolEntry("私", 85, 5),
                MakeSymbolEntry("糞", 100, 20),
                MakeSymbolEntry("ICantSpeekJapaneese", 56, 2),
                MakeSymbolEntry("KoreanNeither", 58, 2)
            };
            var lookup = new SymbolLookup();
            foreach(var symbol in symbols)
            {
                lookup.InsertSymbol(symbol);
            }

            Assert.AreEqual("一", lookup.GetSymbolByAddress(1).Name);
            Assert.AreEqual("二", lookup.GetSymbolByAddress(11).Name);
            Assert.AreEqual("三", lookup.GetSymbolByAddress(16).Name);
            Assert.AreEqual("四", lookup.GetSymbolByAddress(31).Name);
            Assert.AreEqual("二", lookup.GetSymbolByAddress(46).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(52).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(55).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(64).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(72).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(84).Name);
            Assert.AreEqual("私", lookup.GetSymbolByAddress(86).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(94).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(95).Name);
            Assert.AreEqual("糞", lookup.GetSymbolByAddress(100).Name);

            Assert.AreEqual("ICantSpeekJapaneese", lookup.GetSymbolByAddress(57).Name);
            Assert.AreEqual("KoreanNeither", lookup.GetSymbolByAddress(59).Name);
        }

        [Test]
        public void ShouldFindNotSoComplicatedTest()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 0, 100),
                MakeSymbolEntry("国", 50, 20),
                MakeSymbolEntry("五", 55, 5),
                MakeSymbolEntry("中", 60, 10),
                MakeSymbolEntry("猫", 80, 15),
                MakeSymbolEntry("私", 85, 5),
                MakeSymbolEntry("ICantSpeekJapaneese", 56, 2),
                MakeSymbolEntry("KoreanNeither", 58, 2)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("一", lookup.GetSymbolByAddress(1).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(52).Name);
            Assert.AreEqual("五", lookup.GetSymbolByAddress(55).Name);
            Assert.AreEqual("中", lookup.GetSymbolByAddress(64).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(72).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(84).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(94).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(95).Name);
            Assert.AreEqual("ICantSpeekJapaneese", lookup.GetSymbolByAddress(57).Name);
            Assert.AreEqual("KoreanNeither", lookup.GetSymbolByAddress(59).Name);
        }

        [Test]
        public void ShouldWorkWithStackedTest()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 0, 50),
                MakeSymbolEntry("国", 50, 50),
                MakeSymbolEntry("五", 65, 35),
                MakeSymbolEntry("中", 70, 30),
                MakeSymbolEntry("猫", 80, 20),
                MakeSymbolEntry("私", 81, 9),
                MakeSymbolEntry("糞", 90, 10)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);

            Assert.AreEqual("一", lookup.GetSymbolByAddress(1).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(52).Name);
            Assert.AreEqual("五", lookup.GetSymbolByAddress(66).Name);
            Assert.AreEqual("中", lookup.GetSymbolByAddress(71).Name);
            Assert.AreEqual("猫", lookup.GetSymbolByAddress(80).Name);
            Assert.AreEqual("私", lookup.GetSymbolByAddress(82).Name);
            Assert.AreEqual("糞", lookup.GetSymbolByAddress(94).Name);
            Assert.AreEqual("糞", lookup.GetSymbolByAddress(99).Name);
            Symbol dummy;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(100, out dummy));
        }

        /// <summary>
        /// This is a regression test. It should simply not crash nor throw anything.
        /// </summary>
        [Test]
        public void ShouldNotCrashOnInsertDoubledSymbol()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 0, 100),
                MakeSymbolEntry("二", 0, 100),
                MakeSymbolEntry("三", 100, 15),
                MakeSymbolEntry("四", 100, 15),
                MakeSymbolEntry("国", 10, 15),
                MakeSymbolEntry("五", 10, 15),
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            Symbol dummySymbol;
            foreach(var symbol in symbols)
            {
                lookup.GetSymbolsByName(symbol.Name);
                lookup.GetSymbolByAddress(symbol.Start);
                lookup.TryGetSymbolByAddress(symbol.End, out dummySymbol);
            }
        }

        [Test]
        public void ShouldFindZeroLenghtSymbol()
        {
            var symbols = new List<Symbol>
            {
                new Symbol(0, 75, "一"),
                new Symbol(50, 75, "三"),
                new Symbol(75, 75, "二"),
                new Symbol(75, 100, "一"),
                new Symbol(75, 99, "三"),
            };
            var addressesToQuery = new List<uint>{5, 60, 75, 99, 76};
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            CollectionAssert.AreEqual(symbols, addressesToQuery.Select(address => lookup.GetSymbolByAddress(address)));
        }

        [Test]
        public void ShouldCorrectlyGuessZeroLengthSymbol()
        {
            var symbols = new List<Symbol>
            {
                MakeSymbolEntry("一", 20, 0),
                MakeSymbolEntry("三", 30, 10),
                MakeSymbolEntry("国", 40, 0),
                MakeSymbolEntry("五", 50, 50),
                MakeSymbolEntry("中", 60, 10),
                MakeSymbolEntry("猫", 65, 0),
                MakeSymbolEntry("私", 80, 0),
                MakeSymbolEntry("糞", 100, 0),
                MakeSymbolEntry("二", 110, 0)
            };
            var lookup = new SymbolLookup();
            lookup.InsertSymbols(symbols);
            Symbol dummySymbol;
            Assert.IsFalse(lookup.TryGetSymbolByAddress(19, out dummySymbol));
            Assert.AreEqual("一", lookup.GetSymbolByAddress(20).Name);
            Assert.AreEqual("一", lookup.GetSymbolByAddress(21).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(40).Name);
            Assert.AreEqual("国", lookup.GetSymbolByAddress(41).Name);

            Assert.AreEqual("猫", lookup.GetSymbolByAddress(65).Name);
            Assert.AreEqual("中", lookup.GetSymbolByAddress(66).Name);
            Assert.AreEqual("五", lookup.GetSymbolByAddress(70).Name);

            Assert.AreEqual("私", lookup.GetSymbolByAddress(80).Name);
            Assert.AreEqual("五", lookup.GetSymbolByAddress(81).Name);

            Assert.AreEqual("糞", lookup.GetSymbolByAddress(100).Name);
            Assert.AreEqual("糞", lookup.GetSymbolByAddress(101).Name);
            Assert.AreEqual("二", lookup.GetSymbolByAddress(110).Name);
            Assert.IsFalse(lookup.TryGetSymbolByAddress(111, out dummySymbol));
        }

        private Symbol MakeSymbolEntry(string name, uint start, uint length)
        {
            var symbol = new Symbol(start, start + length, name);
            return symbol;
        }
    }
}
