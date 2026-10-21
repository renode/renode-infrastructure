//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Numerics;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Andes_AndeStarV5Extension
    {
        internal void RegisterBFloat16()
        {
            if(bfloat16Registered)
            {
                return;
            }
            bfloat16Registered = true;

            cpu.InstallCustomInstruction("0000000sssss00010100ddddd1011011", opcode => ConvertScalarBFloat16(opcode, narrow: false), "FCVT.S.BF16");
            cpu.InstallCustomInstruction("0000000sssss00011100ddddd1011011", opcode => ConvertScalarBFloat16(opcode, narrow: true), "FCVT.BF16.S");
            cpu.InstallCustomInstruction("0000000sssss00000100ddddd1011011", opcode => ConvertVectorBFloat16(opcode, narrow: false), "VFWCVT.S.BF16");
            cpu.InstallCustomInstruction("0000000sssss00001100ddddd1011011", opcode => ConvertVectorBFloat16(opcode, narrow: true), "VFNCVT.BF16.S");
        }

        private static void MarkStateDirty(BaseRiscV cpu, ulong stateMask)
        {
            var status = cpu.GetRegister((int)RiscV32Registers.MSTATUS).RawValue;
            var dirtySummaryMask = 1ul << ((int)cpu.Bitness - 1);
            cpu.SetRegister((int)RiscV32Registers.MSTATUS, status | stateMask | dirtySummaryMask);
        }

        private static uint WidenBFloat16(ushort value, out FloatingPointFlags flags)
        {
            flags = FloatingPointFlags.None;
            if((value & BFloat16ExponentMask) == BFloat16ExponentMask && (value & BFloat16FractionMask) != 0)
            {
                flags = (value & BFloat16QuietNaNBit) == 0 ? FloatingPointFlags.InvalidOperation : FloatingPointFlags.None;
                return Float32CanonicalNaN;
            }
            return (uint)value << BFloat16ConversionShift;
        }

        private void ConvertScalarBFloat16(ulong opcode, bool narrow)
        {
            if(!RequireFloatingPoint() || (narrow && !RequireRoundingMode()))
            {
                return;
            }
            var value = cpu.GetRegister((int)RiscV32Registers.F0 + BaseRiscV.GetRs2(opcode)).RawValue;
            var doublePrecision = cpu.SupportsInstructionSet(BaseRiscV.InstructionSet.D);
            FloatingPointFlags flags;
            ulong result;
            if(narrow)
            {
                var input = doublePrecision && (value & NaNBox32In64Mask) != NaNBox32In64Mask ? Float32CanonicalNaN : (uint)value;
                result = NaNBox16In64Mask | NarrowBFloat16(input, out flags);
            }
            else
            {
                var box = doublePrecision ? NaNBox16In64Mask : NaNBox16In32Mask;
                var input = (value & box) == box ? (ushort)value : BFloat16CanonicalNaN;
                result = NaNBox32In64Mask | WidenBFloat16(input, out flags);
            }
            cpu.SetRegister((int)RiscV32Registers.F0 + BaseRiscV.GetRd(opcode), result);
            AccrueFloatingPointFlags(flags);
        }

        private ushort NarrowBFloat16(uint value, out FloatingPointFlags flags)
        {
            flags = FloatingPointFlags.None;
            if((value & Float32ExponentMask) == Float32ExponentMask)
            {
                if((value & Float32FractionMask) == 0)
                {
                    return (ushort)(value >> BFloat16ConversionShift);
                }
                flags = (value & Float32QuietNaNBit) == 0 ? FloatingPointFlags.InvalidOperation : FloatingPointFlags.None;
                return BFloat16CanonicalNaN;
            }
            var exponent = (int)((value & Float32ExponentMask) >> Float32FractionBits);
            var significand = (value & Float32FractionMask) | (exponent == 0 ? 0u : Float32ImplicitBit);
            return (ushort)RoundBinary(significand, (exponent == 0 ? 1 - Float32ExponentBias : exponent - Float32ExponentBias) - Float32FractionBits,
                (value & Float32SignMask) != 0, BFloat16FractionBits, BFloat16ExponentBits, out flags);
        }

        private ulong RoundBinary(BigInteger magnitude, int exponent, bool negative, int fractionBits, int exponentBits, out FloatingPointFlags flags)
        {
            flags = FloatingPointFlags.None;
            var sign = negative ? 1ul << (fractionBits + exponentBits) : 0;
            if(magnitude.IsZero)
            {
                return sign;
            }

            var bias = (1 << (exponentBits - 1)) - 1;
            var top = 0;
            for(var value = magnitude; value > 1; value >>= 1)
            {
                ++top;
            }
            var highestExponent = top + exponent;
            var unitExponent = Math.Max(highestExponent, 1 - bias) - fractionBits;
            var discarded = unitExponent - exponent;
            var rounded = RoundSignificand(magnitude, discarded, negative, out var inexact);
            if(inexact)
            {
                flags |= FloatingPointFlags.Inexact;
            }

            if(rounded >= (BigInteger.One << (fractionBits + 1)))
            {
                rounded >>= 1;
                unitExponent++;
            }

            var resultExponent = rounded < (BigInteger.One << fractionBits) ? 0 : unitExponent + fractionBits + bias;
            var infinity = ((1ul << exponentBits) - 1) << fractionBits;
            if(resultExponent >= (1 << exponentBits) - 1)
            {
                flags |= FloatingPointFlags.Overflow | FloatingPointFlags.Inexact;
                var roundToInfinity = CurrentRoundingMode switch
                {
                    RoundingMode.NearestTiesToEven => true,
                    RoundingMode.TowardNegativeInfinity => negative,
                    RoundingMode.TowardPositiveInfinity => !negative,
                    RoundingMode.NearestTiesToMaxMagnitude => true,
                    _ => false,
                };
                return sign | (roundToInfinity ? infinity : infinity - 1);
            }

            if(inexact && highestExponent < 1 - bias)
            {
                // Detect tininess after rounding to normal precision with an unbounded exponent.
                // The final subnormal rounding can produce a normal result that still underflows.
                var tiny = highestExponent < -bias
                    || RoundSignificand(magnitude, top - fractionBits, negative, out _) < (BigInteger.One << (fractionBits + 1));
                if(tiny)
                {
                    flags |= FloatingPointFlags.Underflow;
                }
            }

            return sign | ((ulong)resultExponent << fractionBits) | ((ulong)rounded & ((1ul << fractionBits) - 1));
        }

        private BigInteger RoundSignificand(BigInteger magnitude, int discarded, bool negative, out bool inexact)
        {
            if(discarded <= 0)
            {
                inexact = false;
                return magnitude << -discarded;
            }

            var rounded = magnitude >> discarded;
            var remainder = magnitude - (rounded << discarded);
            inexact = !remainder.IsZero;
            if(!inexact)
            {
                return rounded;
            }

            var midpoint = BigInteger.One << (discarded - 1);
            var increment = CurrentRoundingMode switch
            {
                RoundingMode.NearestTiesToEven => remainder > midpoint || (remainder == midpoint && !rounded.IsEven),
                RoundingMode.TowardNegativeInfinity => negative,
                RoundingMode.TowardPositiveInfinity => !negative,
                RoundingMode.NearestTiesToMaxMagnitude => remainder >= midpoint,
                _ => false,
            };
            return increment ? rounded + 1 : rounded;
        }

        private bool RequireFloatingPoint()
        {
            if(!cpu.IsInstructionSetEnabled(BaseRiscV.InstructionSet.F) || (ReadStatus() & FloatingPointStateMask) == 0)
            {
                IllegalInstruction();
                return false;
            }
            return true;
        }

        private bool RequireRoundingMode()
        {
            if(CurrentRoundingMode > RoundingMode.NearestTiesToMaxMagnitude)
            {
                IllegalInstruction();
                return false;
            }
            return true;
        }

        private void AccrueFloatingPointFlags(FloatingPointFlags flags)
        {
            cpu.FFLAGSField = cpu.FFLAGSField.RawValue | (uint)flags;
            MarkStateDirty(cpu, FloatingPointStateMask);
        }

        private ulong ReadStatus() => cpu.GetRegister((int)RiscV32Registers.MSTATUS).RawValue;

        private void IllegalInstruction()
        {
            // Keep the faulting PC when the custom-instruction dispatcher returns to tlib.
            cpu.PC = cpu.PC.RawValue;
            cpu.RaiseException((uint)BaseRiscV.ExceptionCodes.IllegalInstruction);
        }

        private RoundingMode CurrentRoundingMode => (RoundingMode)cpu.FRMField.RawValue;

        private const ulong FloatingPointStateMask = 0x6000;

        private const int BFloat16FractionBits = 7;
        private const int BFloat16ExponentBits = 8;
        private const int BFloat16ConversionShift = Float32FractionBits - BFloat16FractionBits;
        private const ushort BFloat16FractionMask = (1 << BFloat16FractionBits) - 1;
        private const ushort BFloat16ExponentMask = ((1 << BFloat16ExponentBits) - 1) << BFloat16FractionBits;
        private const ushort BFloat16QuietNaNBit = 1 << (BFloat16FractionBits - 1);
        private const ushort BFloat16CanonicalNaN = BFloat16ExponentMask | BFloat16QuietNaNBit;

        private const int Float32FractionBits = 23;
        private const int Float32ExponentBits = 8;
        private const int Float32ExponentBias = (1 << (Float32ExponentBits - 1)) - 1;
        private const uint Float32FractionMask = (1u << Float32FractionBits) - 1;
        private const uint Float32ExponentMask = ((1u << Float32ExponentBits) - 1) << Float32FractionBits;
        private const uint Float32SignMask = 1u << (Float32FractionBits + Float32ExponentBits);
        private const uint Float32ImplicitBit = 1u << Float32FractionBits;
        private const uint Float32QuietNaNBit = 1u << (Float32FractionBits - 1);
        private const uint Float32CanonicalNaN = Float32ExponentMask | Float32QuietNaNBit;

        private const ulong NaNBox16In32Mask = 0xffff0000ul;
        private const ulong NaNBox16In64Mask = 0xffffffffffff0000ul;
        private const ulong NaNBox32In64Mask = 0xffffffff00000000ul;

        [Flags]
        private enum FloatingPointFlags : uint
        {
            None = 0,
            Inexact = 1 << 0,
            Underflow = 1 << 1,
            Overflow = 1 << 2,
            DivideByZero = 1 << 3,
            InvalidOperation = 1 << 4,
        }

        private enum RoundingMode : ulong
        {
            NearestTiesToEven = 0,
            TowardZero = 1,
            TowardNegativeInfinity = 2,
            TowardPositiveInfinity = 3,
            NearestTiesToMaxMagnitude = 4,
        }
    }
}
