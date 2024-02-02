//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithAArch64Support : ICPU
    {
        bool TryGetSystemRegisterValue(AArch64SystemRegisterEncoding encoding, out ulong value);
        bool TrySetSystemRegisterValue(AArch64SystemRegisterEncoding encoding, ulong value);
    }

    public struct AArch64SystemRegisterEncoding
    {
        // Parts of the MRS/MSR instructions unique for each system register.
        // The order is in line with the documentation.
        public AArch64SystemRegisterEncoding(byte op0, byte op1, byte crn, byte crm, byte op2)
        {
            Op0 = op0;
            Op1 = op1;
            Crn = crn;
            Crm = crm;
            Op2 = op2;
        }

        public override string ToString()
        {
            return $"op0={Op0}, op1={Op1}, crn={Crn}, crm={Crm}, op2={Op2}";
        }

        public byte Crm;
        public byte Crn;
        public byte Op0;
        public byte Op1;
        public byte Op2;
    }
}

