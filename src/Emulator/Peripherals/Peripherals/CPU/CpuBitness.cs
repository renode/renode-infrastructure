//
// Copyright (c) 2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public enum CpuBitness
    {
        Bits32 = 32,
        Bits64 = 64
    }

    public static class CpuBitnessExtensions
    {
        public static ulong GetMaxAddress(this CpuBitness @this)
        {
            switch(@this)
            {
            case CpuBitness.Bits32:
                return uint.MaxValue;
            case CpuBitness.Bits64:
                return ulong.MaxValue;
            default:
                throw new ArgumentException($"Unsupported cpu bitness encountered: {@this}");
            }
        }
    }
}