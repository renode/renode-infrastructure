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
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            switch(r.Width)
            {
            case 32:
                SetRegisterValue32(r.Index, checked((uint)value));
                break;
            case 64:
                SetRegisterValue64(r.Index, checked((ulong)value));
                break;
            default:
                throw new ArgumentException($"Unsupported register width: {r.Width}");
            }
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            switch(r.Width)
            {
            case 32:
                return GetRegisterValue32(r.Index);
            case 64:
                return GetRegisterValue64(r.Index);
            default:
                throw new ArgumentException($"Unsupported register width: {r.Width}");
            }
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
        [Register]
        public RegisterValue FAULTMASK
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.FAULTMASK);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.FAULTMASK, value);
            }
        }
        [Register]
        public RegisterValue FPSCR
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.FPSCR);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.FPSCR, value);
            }
        }
        public RegistersGroup D { get; private set; }

        protected override void InitializeRegisters()
        {
            base.InitializeRegisters();
            var indexValueMapD = new Dictionary<int, CortexMRegisters>
            {
                { 0, CortexMRegisters.D0 },
                { 1, CortexMRegisters.D1 },
                { 2, CortexMRegisters.D2 },
                { 3, CortexMRegisters.D3 },
                { 4, CortexMRegisters.D4 },
                { 5, CortexMRegisters.D5 },
                { 6, CortexMRegisters.D6 },
                { 7, CortexMRegisters.D7 },
                { 8, CortexMRegisters.D8 },
                { 9, CortexMRegisters.D9 },
                { 10, CortexMRegisters.D10 },
                { 11, CortexMRegisters.D11 },
                { 12, CortexMRegisters.D12 },
                { 13, CortexMRegisters.D13 },
                { 14, CortexMRegisters.D14 },
                { 15, CortexMRegisters.D15 },
                { 16, CortexMRegisters.D16 },
                { 17, CortexMRegisters.D17 },
                { 18, CortexMRegisters.D18 },
                { 19, CortexMRegisters.D19 },
                { 20, CortexMRegisters.D20 },
                { 21, CortexMRegisters.D21 },
                { 22, CortexMRegisters.D22 },
                { 23, CortexMRegisters.D23 },
                { 24, CortexMRegisters.D24 },
                { 25, CortexMRegisters.D25 },
                { 26, CortexMRegisters.D26 },
                { 27, CortexMRegisters.D27 },
                { 28, CortexMRegisters.D28 },
                { 29, CortexMRegisters.D29 },
                { 30, CortexMRegisters.D30 },
                { 31, CortexMRegisters.D31 },
            };
            D = new RegistersGroup(
                indexValueMapD.Keys,
                i => GetRegister((int)indexValueMapD[i]),
                (i, v) => SetRegister((int)indexValueMapD[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;

        #pragma warning restore 649

        private static readonly Dictionary<CortexMRegisters, CPURegister> mapping = new Dictionary<CortexMRegisters, CPURegister>
        {
            { CortexMRegisters.R0,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R0" }) },
            { CortexMRegisters.R1,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R1" }) },
            { CortexMRegisters.R2,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R2" }) },
            { CortexMRegisters.R3,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R3" }) },
            { CortexMRegisters.R4,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R4" }) },
            { CortexMRegisters.R5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R5" }) },
            { CortexMRegisters.R6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R6" }) },
            { CortexMRegisters.R7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R7" }) },
            { CortexMRegisters.R8,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { CortexMRegisters.R9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { CortexMRegisters.R10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { CortexMRegisters.R11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { CortexMRegisters.R12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { CortexMRegisters.SP,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "SP", "R13" }) },
            { CortexMRegisters.LR,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "LR", "R14" }) },
            { CortexMRegisters.PC,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PC", "R15" }) },
            { CortexMRegisters.Control,  new CPURegister(18, 32, isGeneral: false, isReadonly: false, aliases: new [] { "Control" }) },
            { CortexMRegisters.BasePri,  new CPURegister(19, 32, isGeneral: false, isReadonly: false, aliases: new [] { "BasePri" }) },
            { CortexMRegisters.VecBase,  new CPURegister(20, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VecBase" }) },
            { CortexMRegisters.CurrentSP,  new CPURegister(21, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CurrentSP" }) },
            { CortexMRegisters.OtherSP,  new CPURegister(22, 32, isGeneral: false, isReadonly: false, aliases: new [] { "OtherSP" }) },
            { CortexMRegisters.FPCCR,  new CPURegister(23, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPCCR" }) },
            { CortexMRegisters.FPCAR,  new CPURegister(24, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPCAR" }) },
            { CortexMRegisters.CPSR,  new CPURegister(25, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CPSR" }) },
            { CortexMRegisters.FPDSCR,  new CPURegister(26, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPDSCR" }) },
            { CortexMRegisters.CPACR,  new CPURegister(27, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CPACR" }) },
            { CortexMRegisters.PRIMASK,  new CPURegister(28, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PRIMASK" }) },
            { CortexMRegisters.FAULTMASK,  new CPURegister(30, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FAULTMASK" }) },
            { CortexMRegisters.D0,  new CPURegister(42, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D0" }) },
            { CortexMRegisters.D1,  new CPURegister(43, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D1" }) },
            { CortexMRegisters.D2,  new CPURegister(44, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D2" }) },
            { CortexMRegisters.D3,  new CPURegister(45, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D3" }) },
            { CortexMRegisters.D4,  new CPURegister(46, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D4" }) },
            { CortexMRegisters.D5,  new CPURegister(47, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D5" }) },
            { CortexMRegisters.D6,  new CPURegister(48, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D6" }) },
            { CortexMRegisters.D7,  new CPURegister(49, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D7" }) },
            { CortexMRegisters.D8,  new CPURegister(50, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D8" }) },
            { CortexMRegisters.D9,  new CPURegister(51, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D9" }) },
            { CortexMRegisters.D10,  new CPURegister(52, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D10" }) },
            { CortexMRegisters.D11,  new CPURegister(53, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D11" }) },
            { CortexMRegisters.D12,  new CPURegister(54, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D12" }) },
            { CortexMRegisters.D13,  new CPURegister(55, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D13" }) },
            { CortexMRegisters.D14,  new CPURegister(56, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D14" }) },
            { CortexMRegisters.D15,  new CPURegister(57, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D15" }) },
            { CortexMRegisters.D16,  new CPURegister(58, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D16" }) },
            { CortexMRegisters.D17,  new CPURegister(59, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D17" }) },
            { CortexMRegisters.D18,  new CPURegister(60, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D18" }) },
            { CortexMRegisters.D19,  new CPURegister(61, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D19" }) },
            { CortexMRegisters.D20,  new CPURegister(62, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D20" }) },
            { CortexMRegisters.D21,  new CPURegister(63, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D21" }) },
            { CortexMRegisters.D22,  new CPURegister(64, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D22" }) },
            { CortexMRegisters.D23,  new CPURegister(65, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D23" }) },
            { CortexMRegisters.D24,  new CPURegister(66, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D24" }) },
            { CortexMRegisters.D25,  new CPURegister(67, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D25" }) },
            { CortexMRegisters.D26,  new CPURegister(68, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D26" }) },
            { CortexMRegisters.D27,  new CPURegister(69, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D27" }) },
            { CortexMRegisters.D28,  new CPURegister(70, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D28" }) },
            { CortexMRegisters.D29,  new CPURegister(71, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D29" }) },
            { CortexMRegisters.D30,  new CPURegister(72, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D30" }) },
            { CortexMRegisters.D31,  new CPURegister(73, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D31" }) },
            { CortexMRegisters.FPSCR,  new CPURegister(74, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPSCR" }) },
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
        FAULTMASK = 30,
        FPSCR = 74,
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
        D0 = 42,
        D1 = 43,
        D2 = 44,
        D3 = 45,
        D4 = 46,
        D5 = 47,
        D6 = 48,
        D7 = 49,
        D8 = 50,
        D9 = 51,
        D10 = 52,
        D11 = 53,
        D12 = 54,
        D13 = 55,
        D14 = 56,
        D15 = 57,
        D16 = 58,
        D17 = 59,
        D18 = 60,
        D19 = 61,
        D20 = 62,
        D21 = 63,
        D22 = 64,
        D23 = 65,
        D24 = 66,
        D25 = 67,
        D26 = 68,
        D27 = 69,
        D28 = 70,
        D29 = 71,
        D30 = 72,
        D31 = 73,
    }
}
