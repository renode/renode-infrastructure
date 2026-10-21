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
        internal void RegisterQuadMac()
        {
            if(quadMacRegistered)
            {
                return;
            }
            quadMacRegistered = true;

            cpu.InstallCustomInstruction("111100mssssssssss000ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: false, signedFirst: false, signedSecond: false), "VQMACCU.VV");
            cpu.InstallCustomInstruction("111100mssssssssss100ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: true, signedFirst: false, signedSecond: false), "VQMACCU.VX");
            cpu.InstallCustomInstruction("111101mssssssssss000ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: false, signedFirst: true, signedSecond: true), "VQMACC.VV");
            cpu.InstallCustomInstruction("111101mssssssssss100ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: true, signedFirst: true, signedSecond: true), "VQMACC.VX");
            cpu.InstallCustomInstruction("111110mssssssssss100ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: true, signedFirst: false, signedSecond: true), "VQMACCUS.VX");
            cpu.InstallCustomInstruction("111111mssssssssss000ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: false, signedFirst: true, signedSecond: false), "VQMACCSU.VV");
            cpu.InstallCustomInstruction("111111mssssssssss100ddddd1010111", opcode => QuadMultiplyAccumulate(opcode, scalar: true, signedFirst: true, signedSecond: false), "VQMACCSU.VX");
        }

        private void QuadMultiplyAccumulate(ulong opcode, bool scalar, bool signedFirst, bool signedSecond)
        {
            var vector = GetVectorState();
            if(vector == null)
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var vs1 = BaseRiscV.GetRs1(opcode);
            var vs2 = BaseRiscV.GetRs2(opcode);
            var width = vector.Width;
            var destinationMultiplier = vector.LogMultiplier + 2;
            if(!ValidateVector((width == 8 || width == 16) && 4 * width <= cpu.VectorElementMaxWidth
                && vector.GroupValid(vd, destinationMultiplier) && vector.GroupValid(vs2, vector.LogMultiplier)
                && (scalar || vector.GroupValid(vs1, vector.LogMultiplier))
                && vector.OverlapValid(vd, destinationMultiplier, vs2, vector.LogMultiplier, 4 * width, width)
                && (scalar || vector.OverlapValid(vd, destinationMultiplier, vs1, vector.LogMultiplier, 4 * width, width))
                && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            var scalarValue = scalar ? cpu.GetRegister(vs1).RawValue & ((1ul << width) - 1) : 0;
            for(var i = vector.Start; i < vector.Length; ++i)
            {
                if(!vector.Active(opcode, i))
                {
                    continue;
                }

                var a = scalar ? scalarValue : vector.Read(vs1, i, width);
                var b = vector.Read(vs2, i, width);
                var x = signedFirst ? (long)BitHelper.SignExtend(a, width) : (long)a;
                var y = signedSecond ? (long)BitHelper.SignExtend(b, width) : (long)b;
                var result = unchecked(vector.Read(vd, i, 4 * width) + (ulong)(x * y));
                vector.Write(vd, i, 4 * width, result);
            }
            vector.Complete();
        }
    }
}
