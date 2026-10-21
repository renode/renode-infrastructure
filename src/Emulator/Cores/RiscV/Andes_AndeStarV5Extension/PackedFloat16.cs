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
        internal void RegisterPackedFloat16()
        {
            if(packedFloat16Registered)
            {
                return;
            }
            packedFloat16Registered = true;

            cpu.InstallCustomInstruction("000010mssssssssss100ddddd1011011", opcode => PackedHalfMultiplyAdd(opcode, top: true), "VFPMADT.VF");
            cpu.InstallCustomInstruction("000011mssssssssss100ddddd1011011", opcode => PackedHalfMultiplyAdd(opcode, top: false), "VFPMADB.VF");
        }

        private void PackedHalfMultiplyAdd(ulong opcode, bool top)
        {
            var vector = GetVectorState();
            if(vector == null || !RequireFloatingPoint() || !RequireRoundingMode())
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var vs = BaseRiscV.GetRs2(opcode);
            if(!ValidateVector(vector.Width == 16 && vector.GroupValid(vd, vector.LogMultiplier)
                && vector.GroupValid(vs, vector.LogMultiplier) && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            var scalar = cpu.GetRegister((int)RiscV32Registers.F0 + BaseRiscV.GetRs1(opcode)).RawValue;
            if(cpu.SupportsInstructionSet(BaseRiscV.InstructionSet.D) && (scalar & NaNBox32In64Mask) != NaNBox32In64Mask)
            {
                scalar = Float32CanonicalNaN;
            }

            var multiplier = (ushort)(scalar >> (top ? 16 : 0));
            var addend = (ushort)(scalar >> (top ? 0 : 16));
            var flags = FloatingPointFlags.None;
            for(var i = vector.Start; i < vector.Length; ++i)
            {
                if(vector.Active(opcode, i))
                {
                    var result = FusedHalfMultiplyAdd(multiplier, (ushort)vector.Read(vs, i, 16), addend, out var elementFlags);
                    vector.Write(vd, i, 16, result);
                    flags |= elementFlags;
                }
            }
            AccrueFloatingPointFlags(flags);
            vector.Complete();
        }

        private ushort FusedHalfMultiplyAdd(ushort a, ushort b, ushort c, out FloatingPointFlags flags)
        {
            flags = FloatingPointFlags.None;
            var x = new HalfOperand(a);
            var y = new HalfOperand(b);
            var z = new HalfOperand(c);
            var invalidProduct = (x.Infinite && y.Zero) || (x.Zero && y.Infinite);

            if(x.SignalingNaN || y.SignalingNaN || z.SignalingNaN || invalidProduct)
            {
                flags |= FloatingPointFlags.InvalidOperation;
            }

            if(x.NaN || y.NaN || z.NaN || invalidProduct)
            {
                return Float16CanonicalNaN;
            }

            var productNegative = x.Negative ^ y.Negative;

            if(x.Infinite || y.Infinite)
            {
                if(z.Infinite && z.Negative != productNegative)
                {
                    flags |= FloatingPointFlags.InvalidOperation;
                    return Float16CanonicalNaN;
                }
                return (ushort)((productNegative ? Float16SignMask : 0) | Float16ExponentMask);
            }

            if(z.Infinite)
            {
                return c;
            }

            // Keep the exact product and sum as integers times powers of two, then round once.
            var exponent = Math.Min(x.Exponent + y.Exponent, z.Exponent);
            var product = (BigInteger)x.Significand * y.Significand << (x.Exponent + y.Exponent - exponent);
            var addend = (BigInteger)z.Significand << (z.Exponent - exponent);
            var sum = (productNegative ? -product : product) + (z.Negative ? -addend : addend);
            var negative = sum.Sign < 0;
            if(sum.IsZero)
            {
                if(product.IsZero && addend.IsZero && productNegative == z.Negative)
                {
                    negative = productNegative;
                }
                else
                {
                    negative = CurrentRoundingMode == RoundingMode.TowardNegativeInfinity;
                }
            }
            return (ushort)RoundBinary(BigInteger.Abs(sum), exponent, negative, Float16FractionBits, Float16ExponentBits, out flags);
        }

        private const int Float16FractionBits = 10;
        private const int Float16ExponentBits = 5;
        private const int Float16ExponentBias = (1 << (Float16ExponentBits - 1)) - 1;
        private const ushort Float16FractionMask = (1 << Float16FractionBits) - 1;
        private const ushort Float16ExponentMask = ((1 << Float16ExponentBits) - 1) << Float16FractionBits;
        private const ushort Float16SignMask = 1 << (Float16FractionBits + Float16ExponentBits);
        private const ushort Float16ImplicitBit = 1 << Float16FractionBits;
        private const ushort Float16QuietNaNBit = 1 << (Float16FractionBits - 1);
        private const ushort Float16CanonicalNaN = Float16ExponentMask | Float16QuietNaNBit;

        private readonly struct HalfOperand
        {
            public HalfOperand(ushort value)
            {
                Negative = (value & Float16SignMask) != 0;
                var exponent = (value & Float16ExponentMask) >> Float16FractionBits;
                var fraction = value & Float16FractionMask;
                NaN = (value & Float16ExponentMask) == Float16ExponentMask && fraction != 0;
                SignalingNaN = NaN && (fraction & Float16QuietNaNBit) == 0;
                Infinite = (value & Float16ExponentMask) == Float16ExponentMask && fraction == 0;
                Zero = exponent == 0 && fraction == 0;
                Significand = (uint)(fraction | (exponent == 0 ? 0 : Float16ImplicitBit));
                Exponent = (exponent == 0 ? 1 - Float16ExponentBias : exponent - Float16ExponentBias) - Float16FractionBits;
            }

            public readonly bool Negative;
            public readonly bool NaN;
            public readonly bool SignalingNaN;
            public readonly bool Infinite;
            public readonly bool Zero;
            public readonly uint Significand;
            public readonly int Exponent;
        }
    }
}
