//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using ELFSharp.ELF.Sections;

namespace Antmicro.Renode.Peripherals.Bus
{
    public static class ISymbolEntryExtensions
    {
        public static ulong GetValue(this ISymbolEntry @this)
        {
            if(@this is SymbolEntry<uint> entryUInt32)
            {
                return entryUInt32.Value;
            }
            if(@this is SymbolEntry<ulong> entryUInt64)
            {
                return entryUInt64.Value;
            }
            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetSize(this ISymbolEntry @this)
        {
            if(@this is SymbolEntry<uint> entryUInt32)
            {
                return entryUInt32.Size;
            }
            if(@this is SymbolEntry<ulong> entryUInt64)
            {
                return entryUInt64.Size;
            }
            throw new ArgumentException(ExceptionMessage);
        }

        private const string ExceptionMessage = "Unsupported SymbolEntry type";
    }
}
