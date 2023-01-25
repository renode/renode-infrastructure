//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using ELFSharp.ELF.Sections;
using Antmicro.Renode.Utilities.Collections;
using System;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using CxxDemangler;

namespace Antmicro.Renode.Core
{
    /// <summary>
    /// Class representing a Symbol in memory. The symbol represtented spans from Start inclusive, till End exclusive.
    /// A symbol of zero length is considered to occupy Start. So a symbol (Start, End) does contain (Start, Start).
    /// Symbols have optional Name, and can be marked as Thumb symbols (in ARM architecture).
    /// </summary>
    public class Symbol : IInterval<SymbolAddress>, IEquatable<Symbol>
    {
        /// <summary>
        /// Static constructor initializing the demangling mechanisms.
        /// </summary>
        static Symbol()
        {
        }

        /// <summary>
        /// Allows casting from SymbolEntry to Symbol. <seealso cref="Symbol(SymbolEntry, bool)"/>.
        /// </summary>
        public static implicit operator Symbol(SymbolEntry<uint> originalSymbol)
        {
            return new Symbol(originalSymbol);
        }

        /// <summary>
        /// Allows casting from SymbolEntry to Symbol. <seealso cref="Symbol(SymbolEntry, bool)"/>.
        /// </summary>
        public static implicit operator Symbol(SymbolEntry<ulong> originalSymbol)
        {
            return new Symbol(originalSymbol);
        }

        /// <summary>
        /// Demangles the symbol name using the CxxDemangler library.
        /// </summary>
        /// <returns>Demangled symbol name.</returns>
        public static string DemangleSymbol(string symbolName)
        {
            const int globalSymbolPrefixLength = 21;
            if(string.IsNullOrEmpty(symbolName) || symbolName.Length < 2)
            {
                return symbolName;
            }
            //length check for the sake of Substring
            if(symbolName.Length > globalSymbolPrefixLength && symbolName.StartsWith("_GLOBAL__sub_I", StringComparison.Ordinal))
            {
                return string.Format("static initializers for {0}", DemangleSymbol(symbolName.Substring(globalSymbolPrefixLength)));
            }
            if(symbolName.Substring(0, 2) != "_Z")
            {
                return symbolName;
            }

            var result = CxxDemangler.CxxDemangler.Demangle(symbolName);

            if(result.Length == 0)
            {
                return symbolName;
            }
            var substringIndex = result.LastIndexOf('(');
            if(substringIndex != -1)
            {
                result = result.Substring(0, substringIndex);
            }
            return result;
        }

        public Symbol(SymbolAddress start, SymbolAddress end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Symbol"/> class.
        /// </summary>
        /// <param name="start">Start.</param>
        /// <param name="end">End.</param>
        /// <param name="name">Name.</param>
        /// <param name="type">SymbolType.</param>
        /// <param name="binding">SymbolBinding.</param>
        /// <param name="mayBeThumb">Set to <c>true</c> if symbol is related to architecture that allows thumb symbols.</param>
        public Symbol(SymbolAddress start, SymbolAddress end, string name, SymbolType type = SymbolType.NotSpecified, SymbolBinding binding = SymbolBinding.Global, bool mayBeThumb = false)
        {
            if(end < start)
            {
                throw new ArgumentException(string.Format("Symbol cannot have start before the end. Input was: ({0},{1})", start, end));
            }
            Type = type;
            Binding = binding;
            Name = DemangleSymbol(name);
            Start = start;
            End = end;
            thumbArchitecture = mayBeThumb;
            if(mayBeThumb)
            {
                UpdateIsThumbSymbol();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Core.Symbol"/> class.
        /// </summary>
        /// <param name="originalSymbol">Original symbol.</param>
        /// <param name="mayBeThumb">Set to <c>true</c> if symbol is related to architecture that allows thumb symbols.</param>
        public Symbol(ISymbolEntry originalSymbol, bool mayBeThumb = false)
        {
            Start = originalSymbol.GetValue();
            IsThumbSymbol = false;
            thumbArchitecture = mayBeThumb;
            if(mayBeThumb)
            {
                UpdateIsThumbSymbol();
            }
            End = Start + originalSymbol.GetSize();
            Name = DemangleSymbol(originalSymbol.Name);
            Type = originalSymbol.Type;
            Binding = originalSymbol.Binding;
        }

        /// <summary>
        /// Gets the copy of symbol with changed end of the interval.
        /// If the requested end is before start, the trial fails.
        /// If the symbol would not have to be changed, i.e. requested the original reference is returned and trial succeeds.
        /// Otherwise a truncated copy of symbol is returned and trial succeeds.
        /// </summary>
        /// <returns>true if trimming returned a proper symbol, false otherwise.</returns>
        /// <param name="end">New end.</param>
        /// <param name = "trimmedSymbol">Trimmed symbol.</param>
        public bool TryGetRightTrimmed(SymbolAddress end, out Symbol trimmedSymbol)
        {
            trimmedSymbol = null;
            if(end < Start)
            {
                return false;
            }
            trimmedSymbol = End < end ? this : GetTrimmedCopy(Start, end);
            return true;
        }

        /// <summary>
        /// Gets the copy of symbol with start of the interval changed to 1st legal address equal or larger than given start.
        /// Trimming from left ensures that the new symbol does not occupy space before new start.
        /// If the requested start is after end, the trial fails.
        /// If the symbol would not have to be changed, i.e. requested the original reference is returned and trial succeeds.
        /// Otherwise a truncated copy of symbol is returned and trial succeeds.
        ///
        /// NOTE: If you create a new thumb symbol with interval (13,15), the acutal interval used will be (12,15);
        /// However, if you trim this symbol from left to the same start value (12,15).LeftTrim(13), the result will be (14,15).
        /// </summary>
        /// <returns>true if trimming returned a proper symbol, false otherwise.</returns>
        /// <param name = "start">New start.</param>
        /// <param name = "trimmedSymbol">Trimmed symbol</param>
        public bool TryGetLeftTrimmed(SymbolAddress start, out Symbol trimmedSymbol)
        {
            trimmedSymbol = null;
            if(End < start)
            {
                return false;
            }
            //if symbol is in thumb architecture and new start is odd, move it to the next legal address
            if(thumbArchitecture && (start % 2) != 0)
            {
                start += 1;
            }
            trimmedSymbol = Start >= start ? this : GetTrimmedCopy(start, End);
            return true;
        }

        /// <summary>
        /// Determines whether this instance is more important than the specified argument.
        /// Two factors are taken into consideration: the Type of the symbol and, if symbol
        /// types are equal, the Binding of the symbol. For example, Function symbol is more
        /// important than Object symbol, and Global Function symbol is more important than
        /// Weak Function symbol.
        /// As the symbol types may be architecture specific, we only take into consideration
        /// those types that we are aware of.
        /// </summary>
        /// <returns><c>true</c> if this instance is more important than the specified second; otherwise, <c>false</c>.</returns>
        /// <param name="second">Symbol to compare with.</param>
        public bool IsMoreImportantThan(Symbol second)
        {
            if(string.IsNullOrWhiteSpace(Name) || !Enum.IsDefined(typeof(SymbolType), Type))
            {
                return false;
            }
            if(string.IsNullOrWhiteSpace(second.Name) || !Enum.IsDefined(typeof(SymbolType), second.Type))
            {
                return true;
            }
            return (Type == second.Type
                && (BindingImportance[Binding] - BindingImportance[second.Binding]) > 0)
                || ((TypeImportance[Type] - TypeImportance[second.Type]) > 0);
        }

        public bool Contains(SymbolAddress x)
        {
            return Start == x || (Start < x && End > x);
        }

        public bool Contains(IInterval<SymbolAddress> other)
        {
            return Start <= other.Start && End >= other.End && End > other.Start;
        }

        /// <summary>
        /// Checks if two symbols overlap.
        /// </summary>
        /// <param name="other">Other.</param>
        public bool Overlaps(IInterval<SymbolAddress> other)
        {
            return Contains(other.Start) || other.Contains(Start);
        }

        public bool Equals(Symbol other)
        {
            return intervalComparer.Compare(this, other) == 0 && string.Compare(Name, other.Name, StringComparison.Ordinal) == 0;
        }

        public string ToStringRelative(SymbolAddress offset)
        {
            if(string.IsNullOrWhiteSpace(this.Name))
            {
                return String.Empty;
            }
            if(this.Start == offset)
            {
                return "{0} (entry)".FormatWith(this.Name);
            }
            if(this.End == this.Start && offset != this.Start)
            {
                return "{0}+0x{1:X} (guessed)".FormatWith(this.Name, offset.RawValue - this.Start.RawValue);
            }
            return this.Name;
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }

        public SymbolAddress Start { get; private set; }

        /// <summary>
        /// Closing bound of the symbol. Remeber the closing bound is exclusive, not inclusive, for non-zero-length symbols.
        /// </summary>
        /// <value>
        /// First address after the symbol.
        /// </value>
        public SymbolAddress End { get; private set; }

        public SymbolAddress Length { get { return End - Start; } }
        public string Name { get; private set; }
        public SymbolType Type { get; private set; }
        public SymbolBinding Binding { get; private set; }
        public bool IsThumbSymbol { get; private set; }

        public bool IsLabel =>
            Type != SymbolType.Function &&
            (Binding == SymbolBinding.Local || Binding == SymbolBinding.Weak) &&
            Length == 0;

        private void UpdateIsThumbSymbol()
        {
            IsThumbSymbol = (Start & 0x1) != 0;
            Start -= (IsThumbSymbol ? 1 : 0u);
        }

        /// <summary>
        /// Gets the copy of symbol with changed interval.
        /// </summary>
        /// <returns>The truncated copy.</returns>
        /// <param name="start">Start.</param>
        /// <param name="end">End.</param>
        private Symbol GetTrimmedCopy(SymbolAddress start, SymbolAddress end)
        {
            return new Symbol(start, end, Name, Type, Binding, thumbArchitecture);
        }

        static private IntervalComparer<SymbolAddress> intervalComparer = new IntervalComparer<SymbolAddress>();

        static private Dictionary<SymbolType, int> TypeImportance = new Dictionary<SymbolType, int>
        {
            { SymbolType.File, 0 },
            { SymbolType.Object, 1 },
            { SymbolType.Section, 2 },
            { SymbolType.ProcessorSpecific, 3 },
            { SymbolType.NotSpecified, 4 },
            { SymbolType.Function, 5 }
        };

        static private Dictionary<SymbolBinding, int> BindingImportance = new Dictionary<SymbolBinding, int>
        {
            { SymbolBinding.Weak, 0 },
            { SymbolBinding.ProcessorSpecific, 1 },
            { SymbolBinding.Local, 2 },
            { SymbolBinding.Global, 3 }
        };

        private readonly bool thumbArchitecture;
    }
}
