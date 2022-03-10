/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class CortexM
    {
        public override void SetRegisterUnsafe(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            return GetRegisterValue32(r.Index);
        }

        public override IEnumerable<CPURegister> GetRegisters()
        {
            return mapping.Values.OrderBy(x => x.Index);
        }

        [Register]
        public RegisterValue Control
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.Control);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.Control, value);
            }
        }
        [Register]
        public RegisterValue BasePri
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.BasePri);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.BasePri, value);
            }
        }
        [Register]
        public RegisterValue VecBase
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.VecBase);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.VecBase, value);
            }
        }
        [Register]
        public RegisterValue CurrentSP
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.CurrentSP);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.CurrentSP, value);
            }
        }
        [Register]
        public RegisterValue OtherSP
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.OtherSP);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.OtherSP, value);
            }
        }
        [Register]
        public RegisterValue FPCCR
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.FPCCR);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.FPCCR, value);
            }
        }
        [Register]
        public RegisterValue FPCAR
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.FPCAR);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.FPCAR, value);
            }
        }
        [Register]
        public RegisterValue FPDSCR
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.FPDSCR);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.FPDSCR, value);
            }
        }
        [Register]
        public RegisterValue CPACR
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.CPACR);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.CPACR, value);
            }
        }
        [Register]
        public RegisterValue PRIMASK
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.PRIMASK);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.PRIMASK, value);
            }
        }

        protected override void InitializeRegisters()
        {
            base.InitializeRegisters();
        }

        private static readonly Dictionary<CortexMRegisters, CPURegister> mapping = new Dictionary<CortexMRegisters, CPURegister>
        {
            { CortexMRegisters.R0,  new CPURegister(0, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R1,  new CPURegister(1, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R2,  new CPURegister(2, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R3,  new CPURegister(3, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R4,  new CPURegister(4, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R8,  new CPURegister(8, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.R12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.SP,  new CPURegister(13, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.LR,  new CPURegister(14, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.PC,  new CPURegister(15, 32, isGeneral: true, isReadonly: false) },
            { CortexMRegisters.Control,  new CPURegister(18, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.BasePri,  new CPURegister(19, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.VecBase,  new CPURegister(20, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.CurrentSP,  new CPURegister(21, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.OtherSP,  new CPURegister(22, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.FPCCR,  new CPURegister(23, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.FPCAR,  new CPURegister(24, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.CPSR,  new CPURegister(25, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.FPDSCR,  new CPURegister(26, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.CPACR,  new CPURegister(27, 32, isGeneral: false, isReadonly: false) },
            { CortexMRegisters.PRIMASK,  new CPURegister(28, 32, isGeneral: false, isReadonly: false) },
        };
    }

    public enum CortexMRegisters
    {
        SP = 13,
        LR = 14,
        PC = 15,
        CPSR = 25,
        Control = 18,
        BasePri = 19,
        VecBase = 20,
        CurrentSP = 21,
        OtherSP = 22,
        FPCCR = 23,
        FPCAR = 24,
        FPDSCR = 26,
        CPACR = 27,
        PRIMASK = 28,
        R0 = 0,
        R1 = 1,
        R2 = 2,
        R3 = 3,
        R4 = 4,
        R5 = 5,
        R6 = 6,
        R7 = 7,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11,
        R12 = 12,
        R13 = 13,
        R14 = 14,
        R15 = 15,
    }
}
