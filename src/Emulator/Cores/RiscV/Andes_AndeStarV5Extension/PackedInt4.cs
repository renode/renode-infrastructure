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
        internal void RegisterPackedInt4Loads()
        {
            if(packedInt4Registered)
            {
                return;
            }
            packedInt4Registered = true;

            cpu.InstallCustomInstruction("000001m00010sssss100ddddd1011011", opcode => LoadInt4(opcode, packed: false, signed: true), "VLN8.V");
            cpu.InstallCustomInstruction("000001m00011sssss100ddddd1011011", opcode => LoadInt4(opcode, packed: false, signed: false), "VLNU8.V");
        }

        private void LoadInt4(ulong opcode, bool packed, bool signed = false)
        {
            // TODO: MMU/PMP integration.
            var vector = GetVectorState();
            if(vector == null)
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var multiplier = vector.LogMultiplier + (packed ? 2 : 3) - Misc.Logarithm2(vector.Width);
            if(!ValidateVector(vector.GroupValid(vd, multiplier) && vector.MaskDestinationValid(opcode, vd)))
            {
                return;
            }

            var address = cpu.GetRegister(BaseRiscV.GetRs1(opcode)).RawValue;
            var start = packed ? vector.Start & ~1ul : vector.Start;
            for(var i = start; i < vector.Length; i += packed ? 2ul : 1ul)
            {
                if(!packed && !vector.Active(opcode, i))
                {
                    continue;
                }
                cpu.SetRegister((int)RiscV32Registers.VSTART, i);
                var data = bus.ReadByte(MaskAddress(address + i / 2), cpu);
                if(packed)
                {
                    // VLE4 may overwrite the final high nibble even when it is a tail element.
                    vector.Write(vd, i / 2, 8, data);
                }
                else
                {
                    var nibble = (ulong)((data >> (int)(4 * (i % 2))) & 15);
                    vector.Write(vd, i, 8, signed ? BitHelper.SignExtend(nibble, 4) : nibble);
                }
            }
            vector.Complete();
        }
    }
}
