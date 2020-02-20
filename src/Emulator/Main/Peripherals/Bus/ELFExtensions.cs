//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;

namespace Antmicro.Renode.Peripherals.Bus
{
    public static class ELFExtensions
    {
        public static int GetBitness(this IELF elf)
        {
            if(elf is ELF<uint>)
            {
                return 32;
            }
            if(elf is ELF<ulong>)
            {
                return 64;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetEntryPoint(this IELF elf)
        {
            if(elf is ELF<uint> elf32)
            {
                return elf32.EntryPoint;
            }
            if(elf is ELF<ulong> elf64)
            {
                return elf64.EntryPoint;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetSegmentAddress(this ISegment segment)
        {
            if(segment is Segment<uint> segment32)
            {
                return segment32.Address;
            }
            if(segment is Segment<ulong> segment64)
            {
                return segment64.Address;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetSegmentPhysicalAddress(this ISegment segment)
        {
            if(segment is Segment<uint> segment32)
            {
                return segment32.PhysicalAddress;
            }
            if(segment is Segment<ulong> segment64)
            {
                return segment64.PhysicalAddress;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetSegmentSize(this ISegment segment)
        {
            if(segment is Segment<uint> segment32)
            {
                return segment32.Size;
            }
            if(segment is Segment<ulong> segment64)
            {
                return segment64.Size;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        public static ulong GetSectionPhysicalAddress(this ISection section)
        {
            if(section is Section<uint> section32)
            {
                return section32.LoadAddress;
            }
            if(section is Section<ulong> section64)
            {
                return section64.LoadAddress;
            }

            throw new ArgumentException(ExceptionMessage);
        }

        private const string ExceptionMessage = "Unsupported ELF format - only 32 and 64-bit ELFs are supported";
    }
}
