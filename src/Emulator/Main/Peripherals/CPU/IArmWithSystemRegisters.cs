//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IArmWithSystemRegisters : ICPU
    {
        bool TryGetSystemRegisterValue(ArmSystemRegisterEncoding encoding, out ulong value);

        bool TrySetSystemRegisterValue(ArmSystemRegisterEncoding encoding, ulong value);

        ExecutionState ExecutionState { get; }

        ExecutionState[] SupportedExecutionStates { get; }
    }

    public enum ExecutionState
    {
        AArch32,
        AArch64,
    }

    public class ArmSystemRegisterEncoding
    {
        // Parts of Arm instructions accessing system registers (AKA coprocessor register
        // before ARMv8) defining the register accessed.
        public ArmSystemRegisterEncoding(CoprocessorEnum coprocessor, byte crm, byte op1,
            byte? crn = null, byte? op0 = null, byte? op2 = null, uint width = 64)
        {
            var hasCrn = crn.HasValue;
            var hasOp0 = op0.HasValue;
            var hasOp2 = op2.HasValue;
            switch(coprocessor)
            {
            case CoprocessorEnum.AArch64:
                if(width != 64)
                {
                    throw new ArgumentException($"AArch64 can only be accessed with 64-bit widths. Given width is {width}");
                }

                if(!hasOp0 || !hasCrn || !hasOp2)
                {
                    throw new ArgumentException("AArch64 must have op0, crn and op2");
                }
                break;
            case CoprocessorEnum.CP14:
            case CoprocessorEnum.CP15:
                // It isn't `!hasOp0` to make construction easier in code common for all coprocessors.
                if((op0 ?? 0) != 0)
                {
                    throw new ArgumentException($"Coprocessor ${coprocessor} requires op0 to be 0 but is ${op0}");
                }

                // 32-bit AArch32 system register instructions require crn and op2 fields
                // which are missing in 64-bit AArch32 system register encodings.
                var isValidAarch32SystemRegister = width == 32 && hasCrn && hasOp2;
                var isValidAarch64SystemRegister = width == 64 && (crn ?? 0) == 0 && (op2 ?? 0) == 0;
                if(!isValidAarch32SystemRegister && !isValidAarch64SystemRegister)
                {
                    throw new ArgumentException("Encoding must either be valid AArch32 or AArch64 system register");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException($"{coprocessor} is not supported.");
            }

            Coprocessor = coprocessor;
            Width = width;

            Op0 = op0;
            Op1 = op1;
            Crn = crn;
            Crm = crm;
            Op2 = op2;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ArmSystemRegisterEncoding);
        }

        public bool Equals(ArmSystemRegisterEncoding encoding)
        {
            return encoding != null && GetHashCode() == encoding.GetHashCode();
        }

        public override int GetHashCode()
        {
            var hash = 17;
            unchecked
            {
                hash = hash * 23 ^ Coprocessor.GetHashCode();
                hash = hash * 23 ^ Width.GetHashCode();
                hash = hash * 23 ^ Crm.GetHashCode();
                hash = hash * 23 ^ Crn.GetHashCode();
                hash = hash * 23 ^ (Op0?.GetHashCode() ?? 0);
                hash = hash * 23 ^ Op1.GetHashCode();
                hash = hash * 23 ^ Op2.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            return GetRegisterString();
        }

        public string GetRegisterString()
        {
            // For example: "CP15 64-bit register <op0=...>".
            return Coprocessor.ToString()
                .AppendIf(Coprocessor != CoprocessorEnum.AArch64, $" {Width}-bit")
                .Append($" register <op0={Op0}, op1={Op1}, crn={Crn}, crm={Crm}, op2={Op2}>")
                .ToString();
        }

        public CoprocessorEnum Coprocessor { get; }

        public uint Width { get; }

        public byte Crm { get; }

        public byte? Crn { get; }

        public byte? Op0 { get; }

        public byte Op1 { get; }

        public byte? Op2 { get; }

        public enum CoprocessorEnum
        {
            CP14 = 14,
            CP15 = 15,
            // AArch64 instructions accessing system registers don't really use a coprocessor
            // number but it's hardcoded as 16 in tlib (see `CP_REG_ARM64_SYSREG_CP` from
            // `tlib/arch/arm64/system_registers.h`).
            AArch64 = 16,
        }
    }
}