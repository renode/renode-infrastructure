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
        internal void RegisterDotProduct()
        {
            if(dotProductRegistered)
            {
                return;
            }
            dotProductRegistered = true;

            cpu.InstallCustomInstruction("000100mssssssssss100ddddd1011011", opcode => DotProduct(opcode, signedFirst: true, signedSecond: true), "VD4DOTS.VV");
            cpu.InstallCustomInstruction("000101mssssssssss100ddddd1011011", opcode => DotProduct(opcode, signedFirst: true, signedSecond: false), "VD4DOTSU.VV");
            cpu.InstallCustomInstruction("000111mssssssssss100ddddd1011011", opcode => DotProduct(opcode, signedFirst: false, signedSecond: false), "VD4DOTU.VV");
        }

        private void DotProduct(ulong opcode, bool signedFirst, bool signedSecond)
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
            if(!ValidateVector((width == 32 || width == 64) && vector.GroupValid(vd, vector.LogMultiplier)
                && vector.GroupValid(vs1, vector.LogMultiplier) && vector.GroupValid(vs2, vector.LogMultiplier)
                && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            var partWidth = width / 4;
            var mask = (1ul << partWidth) - 1;
            for(var i = vector.Start; i < vector.Length; ++i)
            {
                if(!vector.Active(opcode, i))
                {
                    continue;
                }

                var a = vector.Read(vs1, i, width);
                var b = vector.Read(vs2, i, width);
                var result = vector.Read(vd, i, width);
                for(var part = 0; part < 4; ++part)
                {
                    var x = (a >> (part * partWidth)) & mask;
                    var y = (b >> (part * partWidth)) & mask;
                    var firstPart = signedFirst ? (long)BitHelper.SignExtend(x, partWidth) : (long)x;
                    var secondPart = signedSecond ? (long)BitHelper.SignExtend(y, partWidth) : (long)y;
                    result = unchecked(result + (ulong)(firstPart * secondPart));
                }
                vector.Write(vd, i, width, result);
            }

            vector.Complete();
        }
    }
}
