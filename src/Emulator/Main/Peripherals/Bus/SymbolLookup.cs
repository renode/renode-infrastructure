//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

using Antmicro.Renode.Utilities.Collections;
using System.Collections;
using Antmicro.Renode.Utilities;

using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Core
{
    public class SymbolLookup
    {
        public SymbolLookup()
        {
            symbolsByName = new MultiValueDictionary<string, Symbol>();
            symbols = new SortedIntervals(this);
            allSymbolSets.Add(this);
        }

        /// <summary>
        /// Loads the symbols from ELF and puts the symbols into the SymbolLookup.
        /// </summary>
        /// <param name="elf">Elf.</param>
        /// <param name="useVirtualAddress">Use the virtual address of the symbols, default is physical.</param>
        /// <param name="textAddress">Override the starting address of the text section (actually the lowest-address loaded segment).</param>
        public void LoadELF(IELF elf, bool useVirtualAddress = false, ulong? textAddress = null)
        {
            if(elf is ELF<uint> elf32)
            {
                LoadELF(elf32, useVirtualAddress, textAddress);
            }
            else if(elf is ELF<ulong> elf64)
            {
                LoadELF(elf64, useVirtualAddress, textAddress);
            }
            else
            {
                throw new ArgumentException("Unsupported ELF format - only 32 and 64-bit ELFs are supported");
            }
        }

        /// <summary>
        /// Inserts one symbol into the lookup structure.
        /// </summary>
        /// <param name="symbol">Symbol.</param>
        public void InsertSymbol(Symbol symbol)
        {
            var symbolToAdd = GetUnique(symbol);
            symbols.Add(symbolToAdd);
            AddSymbolToNameLookup(symbol);
            maxLoadAddress = SymbolAddress.Max(maxLoadAddress, symbol.End);
        }

        /// <summary>
        /// Inserts a new symbol with defined parameters into the lookup structure.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="start">Start.</param>
        /// <param name="size">Size.</param>
        /// <param name="type">SymbolType.</param>
        /// <param name="binding">Symbol binding</param> 
        /// <param name="isThumb">If set to <c>true</c>, symbol is marked as a thumb symbol.</param>
        public void InsertSymbol(string name, SymbolAddress start, SymbolAddress size, SymbolType type = SymbolType.NotSpecified, SymbolBinding binding = SymbolBinding.Global, bool isThumb = false)
        {
            var symbol = new Symbol(start, start + size, name, type, binding, isThumb);
            InsertSymbol(symbol);
        }

        /// <summary>
        /// Inserts a batch of symbols.
        /// </summary>
        /// <param name="symbols">Symbols.</param>
        public void InsertSymbols(IEnumerable<Symbol> symbols)
        {
            var symbolsToAdd = new List<Symbol>();

            // deduplicate symbols
            foreach(var symbol in symbols)
            {
                if(symbol.Name == "")
                {
                    continue;
                }
                symbolsToAdd.Add(GetUnique(symbol));
            }

            // Add symbols to name map
            foreach(var symbolToAdd in symbolsToAdd)
            {
                AddSymbolToNameLookup(symbolToAdd);
                maxLoadAddress = SymbolAddress.Max(maxLoadAddress, symbolToAdd.End);
            }

            // Add symbols to interval set
            this.symbols.Add(symbolsToAdd);
        }

        public bool TryGetSymbolsByName(string name, out IReadOnlyCollection<Symbol> symbols)
        {
            return symbolsByName.TryGetValue(name, out symbols);
        }

        public IReadOnlyCollection<Symbol> GetSymbolsByName(string name)
        {
            IReadOnlyCollection<Symbol> candidates;
            symbolsByName.TryGetValue(name, out candidates);
            return candidates;
        }

        /// <summary>
        /// Tries to get the innermost symbol which contains the specified address.
        /// </summary>
        /// <returns><c>true</c>, if a symbol was found, <c>false</c> when no symbol contains specified address.</returns>
        /// <param name="offset">Offset.</param>
        /// <param name="symbol">Symbol.</param>
        public bool TryGetSymbolByAddress(SymbolAddress offset, out Symbol symbol)
        {
            if(offset > maxLoadAddress)
            {
                symbol = default(Symbol);
                return false;
            }
            return symbols.TryGet(offset, out symbol);
        }

        /// <summary>
        /// Gets the innermost symbol which contains the specified address.
        /// </summary>
        /// <returns>The symbol by address.</returns>
        /// <param name="offset">Offset.</param>
        public Symbol GetSymbolByAddress(SymbolAddress offset)
        {
            Symbol symbol;
            if(!TryGetSymbolByAddress(offset, out symbol))
            {
                throw new KeyNotFoundException("No symbol for given address [" + offset + "] found.");
            }
            return symbol;
        }

        public SymbolAddress? EntryPoint
        {
            get;
            private set;
        }

        public ulong? FirstNotNullSectionAddress
        {
            get;
            private set;
        }

        /// <summary>
        /// All SymbolLookup objects.
        /// </summary>
        static private readonly HashSet<SymbolLookup> allSymbolSets = new HashSet<SymbolLookup>();
        // Those symbols have a special meaning
        // $a - marks ARM code segments
        // $d - marks data segments
        // $t - marks THUMB code segments
        // $x - marks RISC-V code segments
        // Sources: https://simplemachines.it/doc/aaelf.pdf section 4.5.6, https://www.mail-archive.com/bug-binutils@gnu.org/msg38960.html
        // All of those symbols can be used by the disassembler when reading the binary and are not needed during the execution
        static private readonly string[] excludedSymbolNames = { "$a", "$d", "$t", "$x" };
        static private readonly SymbolType[] excludedSymbolTypes = { SymbolType.File };

        private void LoadELF<T>(ELF<T> elf, bool useVirtualAddress = false, ulong? textAddress = null) where T : struct
        {
            if(!elf.TryGetSection(".symtab", out var symtabSection))
            {
                return;
            }

            var segments = elf.Segments.Where(x => x.Type == ELFSharp.ELF.Segments.SegmentType.Load).OfType<ELFSharp.ELF.Segments.Segment<T>>();
            foreach(var segment in segments)
            {
                var loadAddress = useVirtualAddress ? segment.GetSegmentAddress() : segment.GetSegmentPhysicalAddress();
                minLoadAddress = SymbolAddress.Min(minLoadAddress, loadAddress);
                maxLoadAddress = SymbolAddress.Max(maxLoadAddress, loadAddress + segment.GetSegmentSize());
            }

            var offset = (T)Convert.ChangeType(textAddress != null ? textAddress.Value - minLoadAddress.RawValue : 0, typeof(T));
            var thumb = elf.Machine == ELFSharp.ELF.Machine.ARM;
            var symtab = (SymbolTable<T>)symtabSection;

            // All names on the excluded list are valid C identifiers, so someone may name their function like that
            // To guard against it we also check if the type of this symbol is not specified
            var elfSymbols = symtab.Entries.Where(x => !(excludedSymbolNames.Contains(x.Name) && x.Type == SymbolType.NotSpecified))
                                .Where(x => !excludedSymbolTypes.Contains(x.Type))
                                .Where(x => x.PointedSectionIndex != (uint)SpecialSectionIndex.Undefined)
                                .Select(x => new Symbol(x.OffsetBy(offset), thumb));
            InsertSymbols(elfSymbols);
            EntryPoint = elf.GetEntryPoint();
            FirstNotNullSectionAddress  = elf.Sections
                                    .Where(x => x.Type != SectionType.Null && x.Flags.HasFlag(SectionFlags.Allocatable))
                                    .Select(x => x.GetSectionPhysicalAddress())
                                    .Cast<ulong?>()
                                    .Min();
        }

        private bool TryGetSymbol(Symbol symbol, out Symbol outSymbol)
        {
            IReadOnlyCollection<Symbol> candidates;
            outSymbol = null;
            // Try getting cadidates via name, then if there are any present check whether they fit actual symbol.
            if(TryGetSymbolsByName(symbol.Name, out candidates) && candidates.Count > 0)
            {
                outSymbol = candidates.FirstOrDefault(s => s.Equals(symbol));
            }
            return outSymbol != null;
        }

        /// <summary>
        /// Removes the symbol from the local name look up. This is used internally when some symbol has to change, and needs to be evicted.
        /// </summary>
        private void RemoveSymbolFromNameLookup(Symbol symbol)
        {
            Symbol candidate;
            if(TryGetSymbol(symbol, out candidate))
            {
                symbolsByName.Remove(symbol.Name, symbol);
            }
        }

        /// <summary>
        /// Adds the symbol to the name lookup. If the symbol with the same name exists, the one which
        /// starts earlier will is chosen to be present in the name lookup/view.
        /// </summary>
        /// <param name="symbol">Symbol.</param>
        private void AddSymbolToNameLookup(Symbol symbol)
        {
            symbolsByName.Add(symbol.Name, symbol);

            var dotIndex = symbol.Name.IndexOf('.');
            if(dotIndex >= 0)
            {
                // NOTE: Add alias for the symbol if it contains clone suffix
                var withoutCloneSuffix = symbol.Name.Substring(0, dotIndex);
                symbolsByName.Add(withoutCloneSuffix, symbol);
            }
        }

        /// <summary>
        /// Returs a unique reference for the equivalent of given <paramref name="symbol"/>. If there exists a duplicate, a reference
        /// to the existing symbol is given, if not the argument is passed back.
        /// </summary>
        /// <description>
        /// The algorithm for each known symbolSet works as follows:
        /// 1. Check if the symbol is in the name set (this is O(1)), if it does it's our `candidate`.
        ///  1.1. If not we are sure there is no matching symbol in the current set.
        /// 2. Compare `candidate` to symbol. (It might be different, since symbols can have same names and different intervals.)
        ///  2.1. If it is the same, return candidate.
        ///  2.2. Otherwise, try to find the symbol via interval.
        /// 3. If we didn't find a match in all sets return original reference.
        /// </description>
        /// <returns>The reference to the original symbol.</returns>
        /// <param name="symbol">Symbol.</param>
        static private Symbol GetUnique(Symbol symbol)
        {
            Symbol outSymbol;
            foreach(var symbolSet in allSymbolSets)
            {
                if(symbolSet.TryGetSymbol(symbol, out outSymbol))
                {
                    return outSymbol;
                }
            }
            return symbol;
        }

        /// <summary>
        /// Interval to symbol mapping.
        /// </summary>
        private readonly SortedIntervals symbols;

        /// <summary>
        /// Name to symbol mapping.
        /// </summary>
        private MultiValueDictionary<string, Symbol> symbolsByName;

        private SymbolAddress minLoadAddress = SymbolAddress.MaxValue;
        private SymbolAddress maxLoadAddress;

        private class SortedIntervals : IEnumerable
        {
            public SortedIntervals(SymbolLookup owner)
            {
                this.owner = owner;
                symbols = new Symbol[0];
                indexOfEnclosingInterval = new int[0];
                Count = 0;
            }

            /// <summary>
            /// Adds the intervals to the containers in batch mode.
            /// </summary>
            /// <param name="newSymbols">Intervals.</param>
            public void Add(IEnumerable<Symbol> newSymbols)
            {
                var sortedIntervals = SelectDistinctViaImportance(newSymbols).ToArray();
                Array.Sort(sortedIntervals, comparer);
                sortedIntervals = DropUnusedLabels(sortedIntervals).ToArray();

                if(Count > 0)
                {
                    List<Symbol> createdSymbols;
                    List<Symbol> deletedSymbols;
                    MergeInSymbolArray(sortedIntervals, out createdSymbols, out deletedSymbols);
                    foreach(var deletedSymbol in deletedSymbols)
                    {
                        owner.RemoveSymbolFromNameLookup(deletedSymbol);
                    }
                    foreach(var createdSymbol in createdSymbols)
                    {
                        owner.AddSymbolToNameLookup(SymbolLookup.GetUnique(createdSymbol));
                    }
                }
                else
                {
                    symbols = sortedIntervals;
                }

                Count = symbols.Length;
                RebuildEnclosingIntervals();
            }

            /// <summary>
            /// Add the specified interval. NOTE: This is a version for single element. It's logic is complicated compared to the batch version,
            /// but it should have better performance when it comes to constants with complexities.
            /// Our typical use-cases do not use it very much, so if the maintain cost becomes too high, the implementation should be swapped
            /// for to use range add. "Too high" is when you ask yourself a question "Is it too high?".
            /// </summary>
            /// <param name="symbol">Symbol.</param>
            public void Add(Symbol symbol)
            {
                Add(new[] { symbol });
            }

            /// <summary>
            /// Tries to get the innermost symbol containing the specified scalar. If no symbol contains the scalar the trial fails.
            /// </summary>
            /// <returns><c>true</c>, if the interval was found, <c>false</c> when such interval does not exist.</returns>
            /// <param name="scalar">Scalar.</param>
            /// <param name="interval">Found interval.</param>
            public bool TryGet(SymbolAddress scalar, out Symbol interval)
            {
                interval = default(Symbol);
                var marker = new MarkerSymbol(scalar);
                var index = Array.BinarySearch<Symbol>(symbols, marker, comparer);
                if(index < 0)
                {
                    index = ~index;
                    // we are behind last element or before 1st
                    if(index == 0)
                    {
                        return false;
                    }
                    // move back one element because search will always point to the 1st larger element than the marker
                    index--;
                }

                /***
                * We might be between two towers, that lie on the common ground (we will be between the top of the left tower and
                * before the base of the next one). `index` points to the left top symbol, we check if any enclosing symbol also contains the
                * scalar. If not, there is no such symbol (the common ground) and scalar just lies between two towers. If it exists scalar 
                * is between two symbol lying on the same base.
                ****/
                index = FindEnclosingInterval(index, scalar);
                if(index != NoEnclosingInterval)
                {
                    interval = symbols[index];
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Tries to retrieve a Symbol with the given interval from the container.
            /// </summary>
            /// <returns><c>true</c>, if such container existed and was found, <c>false</c> otherwise.</returns>
            /// <param name="other">Interval to look for.</param>
            /// <param name="interval">Interval.</param>
            public bool TryGet(Symbol other, out Symbol interval)
            {
                interval = null;
                var index = Array.BinarySearch<Symbol>(symbols, other, comparer);
                if(index > 0)
                {
                    interval = symbols[index];
                }
                return interval != null;
            }

            public IEnumerator GetEnumerator()
            {
                return symbols.GetEnumerator();
            }

            /// <summary>
            /// Tells whether an exact interval is contained in the container.
            /// </summary>
            /// <param name="interval">Interval.</param>
            public bool Contains(Symbol interval)
            {
                var index = Array.BinarySearch<Symbol>(symbols, interval, comparer);
                return index > 0;
            }

            /// <summary>
            /// Total number of elements.
            /// </summary>
            /// <value>The count.</value>
            public int Count { get; private set; }

            /// <summary>
            /// Filters out the symbols that are just labels and are also covered by non-label symbols.
            /// </summary>
            /// <param name="symbols">Sorted collection of symbols.</param>
            private IEnumerable<Symbol> DropUnusedLabels(IEnumerable<Symbol> symbols)
            {
                Symbol currentSymbol = null;
                foreach(var symbol in symbols)
                {
                    // only include labels that are not part of other symbols
                    if(!symbol.IsLabel)
                    {
                        if(currentSymbol == null || !currentSymbol.Contains(symbol))
                        {
                            // we advance currentSymbol only if we're in topmost (largest) symbol of the stack.
                            // The reason is that it will definitely contain all smaller ones and we will not miss this situation:
                            //
                            //  aaaaaa  i
                            // xxxxxxxxxxxxx
                            //
                            // in which `i` is not contained by `a`, but is contained by `x`.
                            currentSymbol = symbol;
                        }
                        yield return symbol;
                    }
                    else
                    {
                        // label
                        if(currentSymbol == null || !currentSymbol.Contains(symbol))
                        {
                            yield return symbol;
                        }
                    }
                }
            }

            /// <summary>
            /// Consumes whole cake from provider
            /// </summary>
            /// <param name="destination">Destination.</param>
            /// <param name="source">Source.</param>
            private static void CopyCake(ICollection<Symbol> destination, ISymbolProvider source)
            {
                Symbol cakeBase = source.Current;
                destination.Add(source.Consume()); // copy base
                // "move cake" - while current old symbol is contained in base copy it
                while(!source.Empty && cakeBase.Contains(source.Current))
                {
                    destination.Add(source.Consume());
                }
            }

            /// <summary>
            /// Consumes all symbols from provider copying them to the destination.
            /// </summary>
            /// <param name="destination">Destination.</param>
            /// <param name="source">Source.</param>
            private static void CopyRest(ICollection<Symbol> destination, ISymbolProvider source)
            {
                while(!source.Empty)
                {
                    destination.Add(source.Consume());
                }
            }

            /// <summary>
            /// This function filters out symbols with the same intervals perserving the most important ones.
            /// </summary>
            private IEnumerable<Symbol> SelectDistinctViaImportance(IEnumerable<Symbol> symbolSet)
            {
                var distinctSymbols = new MultiValueDictionary<int, Symbol>();
                IReadOnlyCollection<Symbol> symbolsWithSameHash;
                int hashCode;
                foreach(var symbol in symbolSet)
                {
                    hashCode = comparer.GetHashCode(symbol);
                    if(distinctSymbols.TryGetValue(hashCode, out symbolsWithSameHash))
                    {
                        // Replace all exisiting copies (interval-wise) of the symbol which that are less important.
                        // There should be only one and we have to break after modification of the collection.
                        foreach(var existingSymbol in symbolsWithSameHash)
                        {
                            if(comparer.Equals(symbol, existingSymbol) && symbol.IsMoreImportantThan(existingSymbol))
                            {
                                distinctSymbols.Remove(hashCode, existingSymbol);
                                distinctSymbols.Add(hashCode, symbol);
                                break;
                            }
                        }
                    }
                    else
                    {
                        distinctSymbols.Add(hashCode, symbol);
                    }
                }
                return distinctSymbols;
            }


            /// <summary>
            /// Consumes a symbol from oldSymbols, and puts it into destination and/or tails of oldSymbols. Handles overlapping with newSymbols.
            /// The head of a symbol is the part from start of the *old* symbol till the start of the *new* one.
            /// The tail of the symbol is the part from the end of the *new* symbol till the end of the *old*.
            /// This function copies over the head to the destination and adds a tail to the tail set (of course
            /// when it makes sense).
            /// </summary>
            /// <param name="newSymbols">Provider of new symbols.</param>
            /// <param name="destination">Destination container.</param>
            /// <param name="oldSymbols">OldSymbolsProvider, symbols provider.</param>
            /// <param name="createdSymbols">Container with the symbols that got created during merging.</param>
            /// <param name="deletedSymbols">Container with the symbols that got got replaced/deleted during merging.</param>
            private void AddSymbolWithHashing(OldSymbolProvider oldSymbols, ISymbolProvider newSymbols, ICollection<Symbol> destination, ICollection<Symbol> createdSymbols, ICollection<Symbol> deletedSymbols)
            {
                var consumedSymbolType = oldSymbols.CurrentType;
                var symbolToAdd = oldSymbols.Consume();

                // If there is no symbol to potentially overlap just copy the orignal one
                if(newSymbols.Empty)
                {
                    destination.Add(symbolToAdd);
                    return;
                }

                if(consumedSymbolType == OldSymbolProvider.SymbolType.Original)
                {
                    deletedSymbols.Add(symbolToAdd);
                }

                // If the symbol to add starts before the new one, add its head to the destination.
                if(symbolToAdd.Start < newSymbols.Current.Start)
                {
                    Symbol symbolHead;
                    symbolToAdd.TryGetRightTrimmed(newSymbols.Current.Start, out symbolHead);
                    // If there are symbols on the tail list, check if the trimming would produce the same interval.
                    // If so, we skip the current addition. We do so, because in such case we want to preserve the top (most inner-one) former symbol.
                    // We need, however, to process the possible remainder, a new tail might be generated.
                    Symbol trimmedTail;
                    if(!oldSymbols.HasNextTail ||
                       !oldSymbols.NextTail.TryGetRightTrimmed(newSymbols.Current.Start, out trimmedTail) ||
                       comparer.Compare(symbolHead, trimmedTail) != 0)
                    {
                        destination.Add(symbolHead);
                        createdSymbols.Add(symbolHead);
                    }
                }

                if(newSymbols.Current.Length == 0 && symbolToAdd.Start == newSymbols.Current.Start)
                {
                    destination.Add(symbolToAdd);
                    return;
                }

                // if there is something behind it add it to the new symbols list
                if(symbolToAdd.End > newSymbols.Current.End)
                {
                    Symbol symbolTail;
                    symbolToAdd.TryGetLeftTrimmed(newSymbols.Current.End, out symbolTail);
                    oldSymbols.AddTail(symbolTail);
#if DEBUG
                    DebugAssert.AssertFalse(
                        newSymbols.Current.Length > 0 && newSymbols.Current.Overlaps(symbolTail),
                        "New symbol overlaps with created tail! Old and new symbols should not overlap."
                    );
#endif
                    return;
                }

                // There is one another case. Where the overlapping candidate completely overshadows the symbol.
                // Don't do anything, the overshadowed symbol got deleted anyways and no new symbol gets created.
            }

            private void MergeInSymbolArray(Symbol[] symbolsToAdd, out List<Symbol> createdSymbols, out List<Symbol> deletedSymbols)
            {
                int capacity = symbols.Length + symbolsToAdd.Length;
                // We don't know exact numbers of the new symbols, because some might get split, and some might get removed.
                // Thus we use a list, and chosen initial capacity makes some sense.
                var mergedIntervals = new List<Symbol>(capacity);
                var oldSymbols = new OldSymbolProvider(comparer, symbols);
                var newSymbols = new SymbolProvider(symbolsToAdd);
                createdSymbols = new List<Symbol>();
                deletedSymbols = new List<Symbol>();

                // While new and old symbols might interleave
                while(!oldSymbols.Empty && !newSymbols.Empty)
                {
                    // While they do not overlap
                    while(!oldSymbols.Empty && !newSymbols.Empty && !oldSymbols.Current.Overlaps(newSymbols.Current))
                    {
                        // Symbols do not overlap here so it is sufficient to check only beginnings to find out which comes 1st.
                        // In case of copying new symbol, we can move whole cake, because it will never be cached.
                        if(oldSymbols.Current.Start < newSymbols.Current.Start)
                        {
                            mergedIntervals.Add(oldSymbols.Consume());
                        }
                        else
                        {
                            CopyCake(mergedIntervals, newSymbols);
                        }
                    }
                    if(oldSymbols.Empty || newSymbols.Empty)
                    {
                        break;
                    }
                    // Here we are sure that current old and new symbols overlap somehow
                    //
                    // While we are not behind the current new symbol, keep adding old symbols and hash them if needed.
                    // Also handles zero length symbols. It is done with such an ugly condition, be cause otherwise,
                    // Two loops would be needed, one before overlaps, and one after, for different placements of 0-length
                    // symbols.
                    while(!oldSymbols.Empty && (
                              oldSymbols.Current.Start < newSymbols.Current.End || (
                                  newSymbols.Current.Length == 0 &&
                                  oldSymbols.Current.Start == newSymbols.Current.End
                              )))
                    {
                        AddSymbolWithHashing(oldSymbols, newSymbols, mergedIntervals, createdSymbols, deletedSymbols);
                    }
                    // Copy in the new symbol cake
                    CopyCake(mergedIntervals, newSymbols);
                }

                // Copy rest of the symbols. If the symbol comes from temporary list, it should be "materialized"
                while(!oldSymbols.Empty)
                {
                    if(oldSymbols.CurrentType == OldSymbolProvider.SymbolType.Temporary)
                    {
                        createdSymbols.Add(oldSymbols.Current);
                    }
                    mergedIntervals.Add(oldSymbols.Consume());
                }

                // Copy rest of the newSymbols
                CopyRest(mergedIntervals, newSymbols);
                symbols = mergedIntervals.ToArray();
            }

            private interface ISymbolProvider
            {
                Symbol Current { get; }

                bool Empty { get; }

                Symbol Consume();
            }

            private class OldSymbolProvider : ISymbolProvider
            {
                public OldSymbolProvider(IComparer<Symbol> comparer, Symbol[] oldSymbols)
                {
                    intervalComparer = comparer;
                    symbols = new SymbolProvider(oldSymbols);
                    tails = new Queue<Symbol>();
                    currentsSynced = false;
                }

                public Symbol Current
                {
                    get
                    {
                        UpdateCurrentSymbolAndType();
                        return currentSymbol;
                    }
                }

                public SymbolType CurrentType
                {
                    get
                    {
                        UpdateCurrentSymbolAndType();
                        return currentSymbolType;
                    }
                }

                public bool Empty
                {
                    get
                    {
                        UpdateCurrentSymbolAndType();
                        return currentSymbol == null;
                    }
                }

                public bool HasNextTail { get { return tails.Count != 0; } }

                public Symbol NextTail { get { return tails.Peek(); } }

                public void AddTail(Symbol tail)
                {
                    tails.Enqueue(tail);
                    currentsSynced = false;
                }

                public Symbol Consume()
                {
                    Symbol ret = Current;
                    DiscardCurrentSymbol();
                    return ret;
                }

                public enum SymbolType
                {
                    NoSymbol,
                    Original,
                    Temporary
                }

                /// <summary>
                /// Discards the current symbol. It consumes original symbol. Why the temporary symbols are not consumed here
                /// is described in updateCurrentSymbolAndType description.
                /// </summary>
                private void DiscardCurrentSymbol()
                {
                    switch(CurrentType)
                    {
                    case SymbolType.Original:
                        symbols.Consume();
                        break;
                    case SymbolType.Temporary:
                    case SymbolType.NoSymbol:
                        break;
                    }
                    currentsSynced = false;
                }

                /// <summary>
                /// Updates current symbol and its type. If current symbol is a temporary it is consumed,
                /// when it's original it is not consumed.
                /// </summary>
                /// <description>
                /// There can exists redundant temporary symbols. I.e. when trimming would produce the same intervals for two symbols.
                /// Everything but last symbol with the same interval is important, rest must be ignored.
                /// This is why temporary symbols are consumed upon update. We cannot remove redundant symbols upon addition,
                /// but we can ignore them upon update. We also cannot peek more than one symbol ahed, so we need to consume one and
                /// check if the next is the same.
                /// Original symbol cannot be consumed when set as current, because a new tail might will be placed before current
                /// it would need to be put back. Vice versa situation do not exists, because original symbols are immutable.
                /// Also tails are added in a monotonic manner, so adding a new tail will also not result in eventual put back.
                /// Original symbols are consumed in DiscardCurrentSymbol.
                /// </description>
                private void UpdateCurrentSymbolAndType()
                {
                    if(currentsSynced)
                    {
                        return;
                    }
                    currentsSynced = true;

                    if(tails.Count == 0 && symbols.Empty)
                    {
                        currentSymbol = null;
                        currentSymbolType = SymbolType.NoSymbol;
                        return;
                    }
                    if(tails.Count == 0)
                    {
                        currentSymbol = symbols.Current;
                        currentSymbolType = SymbolType.Original;
                        return;
                    }
                    if(symbols.Empty)
                    {
                        currentSymbol = DequeueLastCopy(tails);
                        currentSymbolType = SymbolType.Temporary;
                        return;
                    }
                    if(intervalComparer.Compare(symbols.Current, tails.Peek()) < 0)
                    {
                        currentSymbol = symbols.Current;
                        currentSymbolType = SymbolType.Original;
                        return;
                    }
                    // We will get last original copy of the symbol from the tails queue.
                    currentSymbol = DequeueLastCopy(tails);
                    currentSymbolType = SymbolType.Temporary;
                    // If it's exactly the same as the symbol in the old list, we prefer to ignore tail, because it
                    // for sure started before the original symbol, thus would not be the innermost.
                    if(intervalComparer.Compare(symbols.Current, currentSymbol) == 0)
                    {
                        currentSymbol = symbols.Current;
                        currentSymbolType = SymbolType.Original;
                    }
                }

                /// <summary>
                /// Dequeues a tail symbol returning the last equivalent copy. This ensures that if there are tails of exact same interval,
                /// we will get last one, as comming from the more inner one original. This might happen when, two symbol end at the same
                /// address and both will get their heads trimmed.
                /// </summary>
                /// <returns>The last copy.</returns>
                /// <param name="symbolTails">Symbol tails.</param>
                private Symbol DequeueLastCopy(Queue<Symbol> symbolTails)
                {
                    var symbol = symbolTails.Dequeue();
                    while(symbolTails.Count > 0 && intervalComparer.Compare(symbol, symbolTails.Peek()) == 0)
                    {
                        symbol = symbolTails.Dequeue();
                    }
                    return symbol;
                }

                private Symbol currentSymbol;
                private SymbolType currentSymbolType;
                private bool currentsSynced;
                private readonly IComparer<Symbol> intervalComparer;
                private readonly Queue<Symbol> tails;
                private readonly SymbolProvider symbols;
            }

            private class SymbolProvider : ISymbolProvider
            {
                public int Length { get { return array.Length - iterator; } }

                public Symbol Current { get { return Peek(); } }

                public bool Empty { get { return !HasSymbolsLeft(); } }


                public SymbolProvider(Symbol[] symbols)
                {
                    array = symbols;
                }

                public Symbol Consume()
                {
                    return array[iterator++];
                }

                private bool HasSymbolsLeft()
                {
                    return iterator < array.Length;
                }

                private Symbol Peek()
                {
                    return array[iterator];
                }

                private int iterator;
                private readonly Symbol[] array;
            }

            /// <summary>
            /// Rebuilds the helper table - `indexOfEnclosingInterval` from the intervals table in O(n). From fast back-of-the-envelope analysis the
            /// the pessimistic run time is O(2n).
            ///
            /// The algorithms works by constructing a stack of enclosing intervals, and iterating over intervals.
            /// 1. At the beginning the guard is created, meaning there is no enclosing interval. We also set it for the 1st element (it
            /// obviously cannot be containted in anything else).
            /// 2. We iterate over the remaining intervals.
            ///   3. We check whether the current interval is enclosed in the previous one.
            ///   4. If yes, it belong to the "tower", so we add it to the stack.
            ///   5. If not, we descend the stack poping elements until we are enclosed by one or we hit the guard.
            /// 6. The parent is top element from the stack.
            /// </summary>
            private void RebuildEnclosingIntervals()
            {
                indexOfEnclosingInterval = new int[Count];
                if(Count < 1)
                {
                    return;
                }
                var parents = new Stack<int>();

                // 1.
                parents.Push(NoEnclosingInterval);
                indexOfEnclosingInterval[0] = NoEnclosingInterval;

                // 2.
                for(int i = 1; i < Count; ++i)
                {
                    var interval = symbols[i];
                    // 3.
                    if(symbols[i - 1].Contains(interval))
                    {
                        // 4.
                        parents.Push(i - 1);
                    }
                    else
                    {
                        // 5.
                        while(parents.Peek() != NoEnclosingInterval && !symbols[parents.Peek()].Contains(interval))
                        {
                            parents.Pop();
                        }
                    }
                    // 6.
                    indexOfEnclosingInterval[i] = parents.Peek();
                }
            }

            /// <summary>
            /// Finds the next sibling. NOTE: Orphans do not have siblings.
            /// </summary>
            /// <returns>The next sibling or <see cref="NoRightSibling"/> special value.</returns>
            /// <param name="index">Index.</param>
            private int FindNextSibling(int index)
            {
                var parentIdx = indexOfEnclosingInterval[index];

                if(parentIdx == NoEnclosingInterval)
                {
                    return NoRightSibling;
                }

                var parentEnd = symbols[parentIdx].End;
                index++;
                // as long as there are any elements and we're not past our parent's end
                while(index < symbols.Length && symbols[index].End > parentEnd)
                {
                    // check if the element has the same parent
                    if(indexOfEnclosingInterval[index] == parentIdx)
                    {
                        return index;
                    }
                    index++;
                }
                return NoRightSibling;
            }

            /// <summary>
            /// Checks if the given <paramref name="interval"/> is enclosed by the one under <paramref name="index"/> or one of it's "parents".
            /// </summary>
            /// <returns>The enclosing interval index or NoEnclosingInterval.</returns>
            /// <param name="index">Index of the input interval.</param>
            /// <param name="interval">Interval.</param>
            private int FindEnclosingInterval(int index, Symbol interval)
            {
                while(index != NoEnclosingInterval && !symbols[index].Contains(interval))
                {
                    index = indexOfEnclosingInterval[index];
                }
                return index;
            }

            /// <summary>
            /// Checks if the given <paramref name="scalar"/> is enclosed by the one under <paramref name="index"/> or one of it's "parents".
            /// </summary>
            /// <returns>The enclosing interval index or NoEnclosingInterval.</returns>
            /// <param name="index">Index of the input interval.</param>
            /// <param name = "scalar"></param>
            private int FindEnclosingInterval(int index, SymbolAddress scalar)
            {
                var original = index;
                while(index != NoEnclosingInterval && !symbols[index].Contains(scalar))
                {
                    index = indexOfEnclosingInterval[index];
                }
                // A special case when we hit after a 0-length symbol thas does not have any parent.
                // We do not need to check it in the loop, as such a symbol would never enclose other symbols.
                // This gives an approximate result.
                if(index == NoEnclosingInterval)
                {
                    if(symbols[original].Start <= scalar && symbols[original].End == symbols[original].Start && (symbols.Length == original + 1 || symbols[original + 1].Start > scalar))
                    {
                        index = original;
                    }
                }
                return index;
            }

            private SymbolLookup owner;
            private const int NoEnclosingInterval = -1;
            private const int NoRightSibling = -1;
            private readonly MarkerComparer<SymbolAddress> comparer = new MarkerComparer<SymbolAddress>();

            private Symbol[] symbols;

            /// <summary>
            /// Internal container holding mapping from interval index to its enclosing interval index,
            /// if such exists, otherwise a special value <see cref="NoEnclosingInterval"/> is used.
            /// It allows to get all the enclosing intervals in fast manner.
            /// indexOfEnclosingInterval[i] says where the ith's "parent" is.
            /// </summary>
            private int[] indexOfEnclosingInterval;

            /// <summary>
            /// This class is used with marker symbols, to find the innermost intervals which the marker (representing scalar) belongs to.
            /// It basically says if the symbol starts earlier or later than the marker.
            /// </summary>
            private class MarkerComparer<TScalar> : IntervalComparer<TScalar> where TScalar : struct, IComparable<TScalar>
            {
                static TScalar MarkerValue = (TScalar)typeof(TScalar).GetField("MaxValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);

                static public TScalar GetMarkerValue()
                {
                    return MarkerValue;
                }

                public override int Compare(IInterval<TScalar> lhs, IInterval<TScalar> rhs)
                {
                    var markerValue = GetMarkerValue();
                    if(lhs.End.CompareTo(markerValue) != 0 && rhs.End.CompareTo(markerValue) != 0)
                    {
                        return base.Compare(lhs, rhs);
                    }

                    IInterval<TScalar> marker;
                    IInterval<TScalar> symbol;
                    bool markerIsLhs;
                    if(lhs.End.CompareTo(markerValue) == 0)
                    {
                        marker = lhs;
                        symbol = rhs;
                        markerIsLhs = true;
                    }
                    else
                    {
                        marker = rhs;
                        symbol = lhs;
                        markerIsLhs = false;
                    }

                    /**
                    If the marker finds the symbol exactly matching the start, explicitly make the symbol smaller.
                    This will ensure the property, that marker cannot be exactly matched to the symbol.
                    This way, symbol starting directly at the pointer will be treated the same as the symbol
                    Starting before it, which makes sense. It both cases it will contain it depending on the end position.
                    In other words this defines that inclusive start boundary of comparison overrides exclusive end.
                    **/
                    var cmp = symbol.Start.CompareTo(marker.Start) == 0 ? marker.Start.CompareTo(symbol.End) : symbol.Start.CompareTo(marker.Start);
                    // If marker was passed as left hand side argument, we need to invert the comparison result.
                    return markerIsLhs ? -cmp : cmp;
                }
            }

            /// <summary>
            /// This is a convienice class, that makes a marker symbol, which is treated differently by MarkerComparer.
            /// It is a special symbol that wraps a scalar value in the Symbol object. Its Start represents the scalar
            /// value, and its End is a special "magic value" that marks it as a marker symbol.
            /// </summary>
            private class MarkerSymbol : Symbol
            {
                public MarkerSymbol(SymbolAddress value) : base(value, MarkerComparer<SymbolAddress>.GetMarkerValue())
                {
                }
            }
        }
    }
}
