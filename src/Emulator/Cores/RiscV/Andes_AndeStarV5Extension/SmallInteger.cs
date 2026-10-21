//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Andes_AndeStarV5Extension
    {
        internal void RegisterSmallInteger()
        {
            if(smallIntegerRegistered)
            {
                return;
            }
            smallIntegerRegistered = true;

            cpu.InstallCustomInstruction("000001100000sssss100ddddd1011011", opcode => LoadInt4(opcode, packed: true), "VLE4.V");
            cpu.InstallCustomInstruction("000000msssss00100100ddddd1011011", opcode => ConvertSmallInteger(opcode, sourceWidth: 4, signed: true), "VFWCVT.F.N.V");
            cpu.InstallCustomInstruction("000000msssss00101100ddddd1011011", opcode => ConvertSmallInteger(opcode, sourceWidth: 4, signed: false), "VFWCVT.F.NU.V");
            cpu.InstallCustomInstruction("000000msssss00110100ddddd1011011", opcode => ConvertSmallInteger(opcode, sourceWidth: 8, signed: true), "VFWCVT.F.B.V");
            cpu.InstallCustomInstruction("000000msssss00111100ddddd1011011", opcode => ConvertSmallInteger(opcode, sourceWidth: 8, signed: false), "VFWCVT.F.BU.V");
            cpu.InstallCustomInstruction("010010msssss00010010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 8, signed: false), "VZEXT.VF8");
            cpu.InstallCustomInstruction("010010msssss00011010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 8, signed: true), "VSEXT.VF8");
            cpu.InstallCustomInstruction("010010msssss00100010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 4, signed: false), "VZEXT.VF4");
            cpu.InstallCustomInstruction("010010msssss00101010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 4, signed: true), "VSEXT.VF4");
            cpu.InstallCustomInstruction("010010msssss00110010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 2, signed: false), "VZEXT.VF2");
            cpu.InstallCustomInstruction("010010msssss00111010ddddd1010111", opcode => ExtendSmallInteger(opcode, factor: 2, signed: true), "VSEXT.VF2");
        }

        private void ConvertSmallInteger(ulong opcode, int sourceWidth, bool signed)
        {
            var vector = GetVectorState();
            if(vector == null || !RequireFloatingPoint())
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var vs = BaseRiscV.GetRs2(opcode);
            var sourceMultiplier = vector.LogMultiplier + Misc.Logarithm2(sourceWidth) - Misc.Logarithm2(vector.Width);
            if(!ValidateVector((vector.Width == 16 || vector.Width == 32) && vector.GroupValid(vd, vector.LogMultiplier)
                && vector.GroupValid(vs, sourceMultiplier)
                && vector.OverlapValid(vd, vector.LogMultiplier, vs, sourceMultiplier, vector.Width, sourceWidth)
                && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            for(var i = vector.Start; i < vector.Length; ++i)
            {
                if(!vector.Active(opcode, i))
                {
                    continue;
                }

                var raw = vector.Read(vs, i, sourceWidth);
                var value = signed ? (long)BitHelper.SignExtend(raw, sourceWidth) : (long)raw;
                // Every INT4 and INT8 value is exactly representable at either destination precision.
                var result = RoundBinary(System.Math.Abs(value), 0, value < 0,
                    vector.Width == 16 ? Float16FractionBits : Float32FractionBits,
                    vector.Width == 16 ? Float16ExponentBits : Float32ExponentBits, out _);
                vector.Write(vd, i, vector.Width, result);
            }

            AccrueFloatingPointFlags(FloatingPointFlags.None);
            vector.Complete();
        }

        private void ExtendSmallInteger(ulong opcode, int factor, bool signed)
        {
            var vector = GetVectorState();
            if(vector == null)
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var vs = BaseRiscV.GetRs2(opcode);
            var logFactor = Misc.Logarithm2(factor);
            var sourceWidth = vector.Width >> logFactor;
            var sourceMultiplier = vector.LogMultiplier - logFactor;
            if(!ValidateVector(sourceWidth >= 4 && vector.GroupValid(vd, vector.LogMultiplier)
                && vector.GroupValid(vs, sourceMultiplier)
                && vector.OverlapValid(vd, vector.LogMultiplier, vs, sourceMultiplier, vector.Width, sourceWidth)
                && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            for(var i = vector.Start; i < vector.Length; ++i)
            {
                if(vector.Active(opcode, i))
                {
                    var raw = vector.Read(vs, i, sourceWidth);
                    vector.Write(vd, i, vector.Width, signed ? BitHelper.SignExtend(raw, sourceWidth) : raw);
                }
            }

            vector.Complete();
        }
    }
}
