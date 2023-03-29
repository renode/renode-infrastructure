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
    public partial class ARMv8R
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((ARMv8RRegisters)register, out var r))
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
            if(!mapping.TryGetValue((ARMv8RRegisters)register, out var r))
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
        public RegisterValue SP
        {
            get
            {
                return GetRegisterValue64((int)ARMv8RRegisters.SP);
            }
            set
            {
                SetRegisterValue64((int)ARMv8RRegisters.SP, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)ARMv8RRegisters.PC);
            }
            set
            {
                SetRegisterValue64((int)ARMv8RRegisters.PC, value);
            }
        }
        [Register]
        public RegisterValue PSTATE
        {
            get
            {
                return GetRegisterValue32((int)ARMv8RRegisters.PSTATE);
            }
            set
            {
                SetRegisterValue32((int)ARMv8RRegisters.PSTATE, value);
            }
        }
        [Register]
        public RegisterValue FPSR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8RRegisters.FPSR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8RRegisters.FPSR, value);
            }
        }
        [Register]
        public RegisterValue FPCR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8RRegisters.FPCR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8RRegisters.FPCR, value);
            }
        }
        [Register]
        public RegisterValue CPSR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8RRegisters.CPSR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8RRegisters.CPSR, value);
            }
        }
        public RegistersGroup X { get; private set; }
        public RegistersGroup R { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, ARMv8RRegisters>
            {
                { 0, ARMv8RRegisters.X0 },
                { 1, ARMv8RRegisters.X1 },
                { 2, ARMv8RRegisters.X2 },
                { 3, ARMv8RRegisters.X3 },
                { 4, ARMv8RRegisters.X4 },
                { 5, ARMv8RRegisters.X5 },
                { 6, ARMv8RRegisters.X6 },
                { 7, ARMv8RRegisters.X7 },
                { 8, ARMv8RRegisters.X8 },
                { 9, ARMv8RRegisters.X9 },
                { 10, ARMv8RRegisters.X10 },
                { 11, ARMv8RRegisters.X11 },
                { 12, ARMv8RRegisters.X12 },
                { 13, ARMv8RRegisters.X13 },
                { 14, ARMv8RRegisters.X14 },
                { 15, ARMv8RRegisters.X15 },
                { 16, ARMv8RRegisters.X16 },
                { 17, ARMv8RRegisters.X17 },
                { 18, ARMv8RRegisters.X18 },
                { 19, ARMv8RRegisters.X19 },
                { 20, ARMv8RRegisters.X20 },
                { 21, ARMv8RRegisters.X21 },
                { 22, ARMv8RRegisters.X22 },
                { 23, ARMv8RRegisters.X23 },
                { 24, ARMv8RRegisters.X24 },
                { 25, ARMv8RRegisters.X25 },
                { 26, ARMv8RRegisters.X26 },
                { 27, ARMv8RRegisters.X27 },
                { 28, ARMv8RRegisters.X28 },
                { 29, ARMv8RRegisters.X29 },
                { 30, ARMv8RRegisters.X30 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegister((int)indexValueMapX[i]),
                (i, v) => SetRegister((int)indexValueMapX[i], v));

            var indexValueMapR = new Dictionary<int, ARMv8RRegisters>
            {
                { 0, ARMv8RRegisters.R0 },
                { 1, ARMv8RRegisters.R1 },
                { 2, ARMv8RRegisters.R2 },
                { 3, ARMv8RRegisters.R3 },
                { 4, ARMv8RRegisters.R4 },
                { 5, ARMv8RRegisters.R5 },
                { 6, ARMv8RRegisters.R6 },
                { 7, ARMv8RRegisters.R7 },
                { 8, ARMv8RRegisters.R8 },
                { 9, ARMv8RRegisters.R9 },
                { 10, ARMv8RRegisters.R10 },
                { 11, ARMv8RRegisters.R11 },
                { 12, ARMv8RRegisters.R12 },
                { 13, ARMv8RRegisters.R13 },
                { 14, ARMv8RRegisters.R14 },
                { 15, ARMv8RRegisters.R15 },
            };
            R = new RegistersGroup(
                indexValueMapR.Keys,
                i => GetRegister((int)indexValueMapR[i]),
                (i, v) => SetRegister((int)indexValueMapR[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;

        #pragma warning restore 649

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected Action<int, uint> SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected Func<int, uint> GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<ARMv8RRegisters, CPURegister> mapping = new Dictionary<ARMv8RRegisters, CPURegister>
        {
            { ARMv8RRegisters.X0,  new CPURegister(0, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X0" }) },
            { ARMv8RRegisters.X1,  new CPURegister(1, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X1" }) },
            { ARMv8RRegisters.X2,  new CPURegister(2, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X2" }) },
            { ARMv8RRegisters.X3,  new CPURegister(3, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X3" }) },
            { ARMv8RRegisters.X4,  new CPURegister(4, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X4" }) },
            { ARMv8RRegisters.X5,  new CPURegister(5, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X5" }) },
            { ARMv8RRegisters.X6,  new CPURegister(6, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X6" }) },
            { ARMv8RRegisters.X7,  new CPURegister(7, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X7" }) },
            { ARMv8RRegisters.X8,  new CPURegister(8, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X8" }) },
            { ARMv8RRegisters.X9,  new CPURegister(9, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X9" }) },
            { ARMv8RRegisters.X10,  new CPURegister(10, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X10" }) },
            { ARMv8RRegisters.X11,  new CPURegister(11, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X11" }) },
            { ARMv8RRegisters.X12,  new CPURegister(12, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X12" }) },
            { ARMv8RRegisters.X13,  new CPURegister(13, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X13" }) },
            { ARMv8RRegisters.X14,  new CPURegister(14, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X14" }) },
            { ARMv8RRegisters.X15,  new CPURegister(15, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X15" }) },
            { ARMv8RRegisters.X16,  new CPURegister(16, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X16" }) },
            { ARMv8RRegisters.X17,  new CPURegister(17, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X17" }) },
            { ARMv8RRegisters.X18,  new CPURegister(18, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X18" }) },
            { ARMv8RRegisters.X19,  new CPURegister(19, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X19" }) },
            { ARMv8RRegisters.X20,  new CPURegister(20, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X20" }) },
            { ARMv8RRegisters.X21,  new CPURegister(21, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X21" }) },
            { ARMv8RRegisters.X22,  new CPURegister(22, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X22" }) },
            { ARMv8RRegisters.X23,  new CPURegister(23, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X23" }) },
            { ARMv8RRegisters.X24,  new CPURegister(24, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X24" }) },
            { ARMv8RRegisters.X25,  new CPURegister(25, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X25" }) },
            { ARMv8RRegisters.X26,  new CPURegister(26, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X26" }) },
            { ARMv8RRegisters.X27,  new CPURegister(27, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X27" }) },
            { ARMv8RRegisters.X28,  new CPURegister(28, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X28" }) },
            { ARMv8RRegisters.X29,  new CPURegister(29, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X29" }) },
            { ARMv8RRegisters.X30,  new CPURegister(30, 64, isGeneral: false, isReadonly: false, aliases: new [] { "X30" }) },
            { ARMv8RRegisters.SP,  new CPURegister(31, 64, isGeneral: false, isReadonly: false, aliases: new [] { "SP" }) },
            { ARMv8RRegisters.PC,  new CPURegister(32, 64, isGeneral: false, isReadonly: false, aliases: new [] { "PC" }) },
            { ARMv8RRegisters.PSTATE,  new CPURegister(33, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSTATE" }) },
            { ARMv8RRegisters.FPSR,  new CPURegister(66, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPSR" }) },
            { ARMv8RRegisters.FPCR,  new CPURegister(67, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPCR" }) },
            { ARMv8RRegisters.R0,  new CPURegister(100, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R0" }) },
            { ARMv8RRegisters.R1,  new CPURegister(101, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R1" }) },
            { ARMv8RRegisters.R2,  new CPURegister(102, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R2" }) },
            { ARMv8RRegisters.R3,  new CPURegister(103, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R3" }) },
            { ARMv8RRegisters.R4,  new CPURegister(104, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R4" }) },
            { ARMv8RRegisters.R5,  new CPURegister(105, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R5" }) },
            { ARMv8RRegisters.R6,  new CPURegister(106, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R6" }) },
            { ARMv8RRegisters.R7,  new CPURegister(107, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R7" }) },
            { ARMv8RRegisters.R8,  new CPURegister(108, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { ARMv8RRegisters.R9,  new CPURegister(109, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { ARMv8RRegisters.R10,  new CPURegister(110, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { ARMv8RRegisters.R11,  new CPURegister(111, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { ARMv8RRegisters.R12,  new CPURegister(112, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { ARMv8RRegisters.R13,  new CPURegister(113, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R13" }) },
            { ARMv8RRegisters.R14,  new CPURegister(114, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R14" }) },
            { ARMv8RRegisters.R15,  new CPURegister(115, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R15" }) },
            { ARMv8RRegisters.CPSR,  new CPURegister(125, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CPSR" }) },
        };
    }

    public enum ARMv8RRegisters
    {
        SP = 31,
        PC = 32,
        PSTATE = 33,
        FPSR = 66,
        FPCR = 67,
        CPSR = 125,
        X0 = 0,
        X1 = 1,
        X2 = 2,
        X3 = 3,
        X4 = 4,
        X5 = 5,
        X6 = 6,
        X7 = 7,
        X8 = 8,
        X9 = 9,
        X10 = 10,
        X11 = 11,
        X12 = 12,
        X13 = 13,
        X14 = 14,
        X15 = 15,
        X16 = 16,
        X17 = 17,
        X18 = 18,
        X19 = 19,
        X20 = 20,
        X21 = 21,
        X22 = 22,
        X23 = 23,
        X24 = 24,
        X25 = 25,
        X26 = 26,
        X27 = 27,
        X28 = 28,
        X29 = 29,
        X30 = 30,
        R0 = 100,
        R1 = 101,
        R2 = 102,
        R3 = 103,
        R4 = 104,
        R5 = 105,
        R6 = 106,
        R7 = 107,
        R8 = 108,
        R9 = 109,
        R10 = 110,
        R11 = 111,
        R12 = 112,
        R13 = 113,
        R14 = 114,
        R15 = 115,
    }
}
