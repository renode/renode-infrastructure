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
    public partial class RiscV32
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((RiscV32Registers)register, out var r))
            {
                if(TrySetNonMappedRegister(register, value))
                {
                    return;
                }
                throw new RecoverableException($"Wrong register index: {register}");
            }
            if(r.IsReadonly)
            {
                throw new RecoverableException($"The '{(RiscV32Registers)register}' register is read-only.");
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
            if(!mapping.TryGetValue((RiscV32Registers)register, out var r))
            {
                if(TryGetNonMappedRegister(register, out var value))
                {
                    return value;
                }
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
            return mapping.Values.Concat(GetNonMappedRegisters()).OrderBy(x => x.Index);
        }

        [Register]
        public RegisterValue ZERO
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.ZERO);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.ZERO, value);
            }
        }
        [Register]
        public RegisterValue RA
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.RA);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.RA, value);
            }
        }
        [Register]
        public RegisterValue SP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SP, value);
            }
        }
        [Register]
        public RegisterValue GP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.GP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.GP, value);
            }
        }
        [Register]
        public RegisterValue TP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.TP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.TP, value);
            }
        }
        [Register]
        public RegisterValue FP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.FP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.FP, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.PC);
            }
            set
            {
                value = BeforePCWrite(value);
                SetRegisterValue32((int)RiscV32Registers.PC, value);
            }
        }
        [Register]
        public RegisterValue SSTATUS
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SSTATUS);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SSTATUS, value);
            }
        }
        [Register]
        public RegisterValue SIE
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SIE);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SIE, value);
            }
        }
        [Register]
        public RegisterValue STVEC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.STVEC);
            }
            set
            {
                value = BeforeSTVECWrite(value);
                SetRegisterValue32((int)RiscV32Registers.STVEC, value);
            }
        }
        [Register]
        public RegisterValue SSCRATCH
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SSCRATCH);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SSCRATCH, value);
            }
        }
        [Register]
        public RegisterValue SEPC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SEPC);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SEPC, value);
            }
        }
        [Register]
        public RegisterValue SCAUSE
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SCAUSE);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SCAUSE, value);
            }
        }
        [Register]
        public RegisterValue STVAL
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.STVAL);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.STVAL, value);
            }
        }
        [Register]
        public RegisterValue SIP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SIP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SIP, value);
            }
        }
        [Register]
        public RegisterValue SATP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SATP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SATP, value);
            }
        }
        [Register]
        public RegisterValue SPTBR
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.SPTBR);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.SPTBR, value);
            }
        }
        [Register]
        public RegisterValue MSTATUS
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MSTATUS);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MSTATUS, value);
            }
        }
        [Register]
        public RegisterValue MISA
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MISA);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MISA, value);
            }
        }
        [Register]
        public RegisterValue MEDELEG
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MEDELEG);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MEDELEG, value);
            }
        }
        [Register]
        public RegisterValue MIDELEG
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MIDELEG);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MIDELEG, value);
            }
        }
        [Register]
        public RegisterValue MIE
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MIE);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MIE, value);
            }
        }
        [Register]
        public RegisterValue MTVEC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MTVEC);
            }
            set
            {
                value = BeforeMTVECWrite(value);
                SetRegisterValue32((int)RiscV32Registers.MTVEC, value);
            }
        }
        [Register]
        public RegisterValue MSCRATCH
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MSCRATCH);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MSCRATCH, value);
            }
        }
        [Register]
        public RegisterValue MEPC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MEPC);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MEPC, value);
            }
        }
        [Register]
        public RegisterValue MCAUSE
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MCAUSE);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MCAUSE, value);
            }
        }
        [Register]
        public RegisterValue MTVAL
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MTVAL);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MTVAL, value);
            }
        }
        [Register]
        public RegisterValue MIP
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MIP);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MIP, value);
            }
        }
        [Register]
        public RegisterValue MSECCFG
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.MSECCFG);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.MSECCFG, value);
            }
        }
        [Register]
        public RegisterValue PRIV
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.PRIV);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.PRIV, value);
            }
        }
        [Register]
        public RegisterValue VSTART
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VSTART);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VSTART, value);
            }
        }
        [Register]
        public RegisterValue VXSAT
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VXSAT);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VXSAT, value);
            }
        }
        [Register]
        public RegisterValue VXRM
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VXRM);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VXRM, value);
            }
        }
        [Register]
        public RegisterValue VCSR
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VCSR);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VCSR, value);
            }
        }
        [Register]
        public RegisterValue VL
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VL);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VL, value);
            }
        }
        [Register]
        public RegisterValue VTYPE
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VTYPE);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VTYPE, value);
            }
        }
        [Register]
        public RegisterValue VLENB
        {
            get
            {
                BeforeVectorExtensionRegisterRead();
                return GetRegisterValue32((int)RiscV32Registers.VLENB);
            }
            set
            {
                value = BeforeVectorExtensionRegisterWrite(value);
                SetRegisterValue32((int)RiscV32Registers.VLENB, value);
            }
        }
        public RegistersGroup X { get; private set; }
        public RegistersGroup T { get; private set; }
        public RegistersGroup S { get; private set; }
        public RegistersGroup A { get; private set; }
        public RegistersGroup F { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.X0 },
                { 1, RiscV32Registers.X1 },
                { 2, RiscV32Registers.X2 },
                { 3, RiscV32Registers.X3 },
                { 4, RiscV32Registers.X4 },
                { 5, RiscV32Registers.X5 },
                { 6, RiscV32Registers.X6 },
                { 7, RiscV32Registers.X7 },
                { 8, RiscV32Registers.X8 },
                { 9, RiscV32Registers.X9 },
                { 10, RiscV32Registers.X10 },
                { 11, RiscV32Registers.X11 },
                { 12, RiscV32Registers.X12 },
                { 13, RiscV32Registers.X13 },
                { 14, RiscV32Registers.X14 },
                { 15, RiscV32Registers.X15 },
                { 16, RiscV32Registers.X16 },
                { 17, RiscV32Registers.X17 },
                { 18, RiscV32Registers.X18 },
                { 19, RiscV32Registers.X19 },
                { 20, RiscV32Registers.X20 },
                { 21, RiscV32Registers.X21 },
                { 22, RiscV32Registers.X22 },
                { 23, RiscV32Registers.X23 },
                { 24, RiscV32Registers.X24 },
                { 25, RiscV32Registers.X25 },
                { 26, RiscV32Registers.X26 },
                { 27, RiscV32Registers.X27 },
                { 28, RiscV32Registers.X28 },
                { 29, RiscV32Registers.X29 },
                { 30, RiscV32Registers.X30 },
                { 31, RiscV32Registers.X31 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegister((int)indexValueMapX[i]),
                (i, v) => SetRegister((int)indexValueMapX[i], v));

            var indexValueMapT = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.T0 },
                { 1, RiscV32Registers.T1 },
                { 2, RiscV32Registers.T2 },
                { 3, RiscV32Registers.T3 },
                { 4, RiscV32Registers.T4 },
                { 5, RiscV32Registers.T5 },
                { 6, RiscV32Registers.T6 },
            };
            T = new RegistersGroup(
                indexValueMapT.Keys,
                i => GetRegister((int)indexValueMapT[i]),
                (i, v) => SetRegister((int)indexValueMapT[i], v));

            var indexValueMapS = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.S0 },
                { 1, RiscV32Registers.S1 },
                { 2, RiscV32Registers.S2 },
                { 3, RiscV32Registers.S3 },
                { 4, RiscV32Registers.S4 },
                { 5, RiscV32Registers.S5 },
                { 6, RiscV32Registers.S6 },
                { 7, RiscV32Registers.S7 },
                { 8, RiscV32Registers.S8 },
                { 9, RiscV32Registers.S9 },
                { 10, RiscV32Registers.S10 },
                { 11, RiscV32Registers.S11 },
            };
            S = new RegistersGroup(
                indexValueMapS.Keys,
                i => GetRegister((int)indexValueMapS[i]),
                (i, v) => SetRegister((int)indexValueMapS[i], v));

            var indexValueMapA = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.A0 },
                { 1, RiscV32Registers.A1 },
                { 2, RiscV32Registers.A2 },
                { 3, RiscV32Registers.A3 },
                { 4, RiscV32Registers.A4 },
                { 5, RiscV32Registers.A5 },
                { 6, RiscV32Registers.A6 },
                { 7, RiscV32Registers.A7 },
            };
            A = new RegistersGroup(
                indexValueMapA.Keys,
                i => GetRegister((int)indexValueMapA[i]),
                (i, v) => SetRegister((int)indexValueMapA[i], v));

            var indexValueMapF = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.F0 },
                { 1, RiscV32Registers.F1 },
                { 2, RiscV32Registers.F2 },
                { 3, RiscV32Registers.F3 },
                { 4, RiscV32Registers.F4 },
                { 5, RiscV32Registers.F5 },
                { 6, RiscV32Registers.F6 },
                { 7, RiscV32Registers.F7 },
                { 8, RiscV32Registers.F8 },
                { 9, RiscV32Registers.F9 },
                { 10, RiscV32Registers.F10 },
                { 11, RiscV32Registers.F11 },
                { 12, RiscV32Registers.F12 },
                { 13, RiscV32Registers.F13 },
                { 14, RiscV32Registers.F14 },
                { 15, RiscV32Registers.F15 },
                { 16, RiscV32Registers.F16 },
                { 17, RiscV32Registers.F17 },
                { 18, RiscV32Registers.F18 },
                { 19, RiscV32Registers.F19 },
                { 20, RiscV32Registers.F20 },
                { 21, RiscV32Registers.F21 },
                { 22, RiscV32Registers.F22 },
                { 23, RiscV32Registers.F23 },
                { 24, RiscV32Registers.F24 },
                { 25, RiscV32Registers.F25 },
                { 26, RiscV32Registers.F26 },
                { 27, RiscV32Registers.F27 },
                { 28, RiscV32Registers.F28 },
                { 29, RiscV32Registers.F29 },
                { 30, RiscV32Registers.F30 },
                { 31, RiscV32Registers.F31 },
            };
            F = new RegistersGroup(
                indexValueMapF.Keys,
                i => GetRegister((int)indexValueMapF[i]),
                (i, v) => SetRegister((int)indexValueMapF[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected Action<int, uint> SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected Func<int, uint> GetRegisterValue32;

        #pragma warning restore 649

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;

        #pragma warning restore 649

        private static readonly Dictionary<RiscV32Registers, CPURegister> mapping = new Dictionary<RiscV32Registers, CPURegister>
        {
            { RiscV32Registers.ZERO,  new CPURegister(0, 32, isGeneral: true, isReadonly: true, aliases: new [] { "ZERO", "X0" }) },
            { RiscV32Registers.RA,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "RA", "X1" }) },
            { RiscV32Registers.SP,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "SP", "X2" }) },
            { RiscV32Registers.GP,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "GP", "X3" }) },
            { RiscV32Registers.TP,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "TP", "X4" }) },
            { RiscV32Registers.X5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X5", "T0" }) },
            { RiscV32Registers.X6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X6", "T1" }) },
            { RiscV32Registers.X7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X7", "T2" }) },
            { RiscV32Registers.FP,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "FP", "X8", "S0" }) },
            { RiscV32Registers.X9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X9", "S1" }) },
            { RiscV32Registers.X10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X10", "A0" }) },
            { RiscV32Registers.X11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X11", "A1" }) },
            { RiscV32Registers.X12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X12", "A2" }) },
            { RiscV32Registers.X13,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X13", "A3" }) },
            { RiscV32Registers.X14,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X14", "A4" }) },
            { RiscV32Registers.X15,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X15", "A5" }) },
            { RiscV32Registers.X16,  new CPURegister(16, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X16", "A6" }) },
            { RiscV32Registers.X17,  new CPURegister(17, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X17", "A7" }) },
            { RiscV32Registers.X18,  new CPURegister(18, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X18", "S2" }) },
            { RiscV32Registers.X19,  new CPURegister(19, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X19", "S3" }) },
            { RiscV32Registers.X20,  new CPURegister(20, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X20", "S4" }) },
            { RiscV32Registers.X21,  new CPURegister(21, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X21", "S5" }) },
            { RiscV32Registers.X22,  new CPURegister(22, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X22", "S6" }) },
            { RiscV32Registers.X23,  new CPURegister(23, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X23", "S7" }) },
            { RiscV32Registers.X24,  new CPURegister(24, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X24", "S8" }) },
            { RiscV32Registers.X25,  new CPURegister(25, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X25", "S9" }) },
            { RiscV32Registers.X26,  new CPURegister(26, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X26", "S10" }) },
            { RiscV32Registers.X27,  new CPURegister(27, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X27", "S11" }) },
            { RiscV32Registers.X28,  new CPURegister(28, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X28", "T3" }) },
            { RiscV32Registers.X29,  new CPURegister(29, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X29", "T4" }) },
            { RiscV32Registers.X30,  new CPURegister(30, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X30", "T5" }) },
            { RiscV32Registers.X31,  new CPURegister(31, 32, isGeneral: true, isReadonly: false, aliases: new [] { "X31", "T6" }) },
            { RiscV32Registers.PC,  new CPURegister(32, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PC" }) },
            { RiscV32Registers.F0,  new CPURegister(33, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F0" }) },
            { RiscV32Registers.F1,  new CPURegister(34, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F1" }) },
            { RiscV32Registers.F2,  new CPURegister(35, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F2" }) },
            { RiscV32Registers.F3,  new CPURegister(36, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F3" }) },
            { RiscV32Registers.F4,  new CPURegister(37, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F4" }) },
            { RiscV32Registers.F5,  new CPURegister(38, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F5" }) },
            { RiscV32Registers.F6,  new CPURegister(39, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F6" }) },
            { RiscV32Registers.F7,  new CPURegister(40, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F7" }) },
            { RiscV32Registers.F8,  new CPURegister(41, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F8" }) },
            { RiscV32Registers.F9,  new CPURegister(42, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F9" }) },
            { RiscV32Registers.F10,  new CPURegister(43, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F10" }) },
            { RiscV32Registers.F11,  new CPURegister(44, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F11" }) },
            { RiscV32Registers.F12,  new CPURegister(45, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F12" }) },
            { RiscV32Registers.F13,  new CPURegister(46, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F13" }) },
            { RiscV32Registers.F14,  new CPURegister(47, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F14" }) },
            { RiscV32Registers.F15,  new CPURegister(48, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F15" }) },
            { RiscV32Registers.F16,  new CPURegister(49, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F16" }) },
            { RiscV32Registers.F17,  new CPURegister(50, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F17" }) },
            { RiscV32Registers.F18,  new CPURegister(51, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F18" }) },
            { RiscV32Registers.F19,  new CPURegister(52, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F19" }) },
            { RiscV32Registers.F20,  new CPURegister(53, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F20" }) },
            { RiscV32Registers.F21,  new CPURegister(54, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F21" }) },
            { RiscV32Registers.F22,  new CPURegister(55, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F22" }) },
            { RiscV32Registers.F23,  new CPURegister(56, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F23" }) },
            { RiscV32Registers.F24,  new CPURegister(57, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F24" }) },
            { RiscV32Registers.F25,  new CPURegister(58, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F25" }) },
            { RiscV32Registers.F26,  new CPURegister(59, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F26" }) },
            { RiscV32Registers.F27,  new CPURegister(60, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F27" }) },
            { RiscV32Registers.F28,  new CPURegister(61, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F28" }) },
            { RiscV32Registers.F29,  new CPURegister(62, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F29" }) },
            { RiscV32Registers.F30,  new CPURegister(63, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F30" }) },
            { RiscV32Registers.F31,  new CPURegister(64, 64, isGeneral: false, isReadonly: false, aliases: new [] { "F31" }) },
            { RiscV32Registers.VSTART,  new CPURegister(101, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VSTART" }) },
            { RiscV32Registers.VXSAT,  new CPURegister(102, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VXSAT" }) },
            { RiscV32Registers.VXRM,  new CPURegister(103, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VXRM" }) },
            { RiscV32Registers.VCSR,  new CPURegister(104, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VCSR" }) },
            { RiscV32Registers.SSTATUS,  new CPURegister(321, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SSTATUS" }) },
            { RiscV32Registers.SIE,  new CPURegister(325, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SIE" }) },
            { RiscV32Registers.STVEC,  new CPURegister(326, 32, isGeneral: false, isReadonly: false, aliases: new [] { "STVEC" }) },
            { RiscV32Registers.SSCRATCH,  new CPURegister(385, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SSCRATCH" }) },
            { RiscV32Registers.SEPC,  new CPURegister(386, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SEPC" }) },
            { RiscV32Registers.SCAUSE,  new CPURegister(387, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SCAUSE" }) },
            { RiscV32Registers.STVAL,  new CPURegister(388, 32, isGeneral: false, isReadonly: false, aliases: new [] { "STVAL" }) },
            { RiscV32Registers.SIP,  new CPURegister(389, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SIP" }) },
            { RiscV32Registers.SATP,  new CPURegister(449, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SATP", "SPTBR" }) },
            { RiscV32Registers.MSTATUS,  new CPURegister(833, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MSTATUS" }) },
            { RiscV32Registers.MISA,  new CPURegister(834, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MISA" }) },
            { RiscV32Registers.MEDELEG,  new CPURegister(835, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MEDELEG" }) },
            { RiscV32Registers.MIDELEG,  new CPURegister(836, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MIDELEG" }) },
            { RiscV32Registers.MIE,  new CPURegister(837, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MIE" }) },
            { RiscV32Registers.MTVEC,  new CPURegister(838, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MTVEC" }) },
            { RiscV32Registers.MSCRATCH,  new CPURegister(897, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MSCRATCH" }) },
            { RiscV32Registers.MEPC,  new CPURegister(898, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MEPC" }) },
            { RiscV32Registers.MCAUSE,  new CPURegister(899, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MCAUSE" }) },
            { RiscV32Registers.MTVAL,  new CPURegister(900, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MTVAL" }) },
            { RiscV32Registers.MIP,  new CPURegister(901, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MIP" }) },
            { RiscV32Registers.MSECCFG,  new CPURegister(1863, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MSECCFG" }) },
            { RiscV32Registers.VL,  new CPURegister(3104, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VL" }) },
            { RiscV32Registers.VTYPE,  new CPURegister(3105, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VTYPE" }) },
            { RiscV32Registers.VLENB,  new CPURegister(3106, 32, isGeneral: false, isReadonly: true, aliases: new [] { "VLENB" }) },
            { RiscV32Registers.PRIV,  new CPURegister(4161, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PRIV" }) },
        };
    }

    public enum RiscV32Registers
    {
        ZERO = 0,
        RA = 1,
        SP = 2,
        GP = 3,
        TP = 4,
        FP = 8,
        PC = 32,
        SSTATUS = 321,
        SIE = 325,
        STVEC = 326,
        SSCRATCH = 385,
        SEPC = 386,
        SCAUSE = 387,
        STVAL = 388,
        SIP = 389,
        SATP = 449,
        SPTBR = 449,
        MSTATUS = 833,
        MISA = 834,
        MEDELEG = 835,
        MIDELEG = 836,
        MIE = 837,
        MTVEC = 838,
        MSCRATCH = 897,
        MEPC = 898,
        MCAUSE = 899,
        MTVAL = 900,
        MIP = 901,
        MSECCFG = 1863,
        PRIV = 4161,
        VSTART = 101,
        VXSAT = 102,
        VXRM = 103,
        VCSR = 104,
        VL = 3104,
        VTYPE = 3105,
        VLENB = 3106,
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
        X31 = 31,
        T0 = 5,
        T1 = 6,
        T2 = 7,
        T3 = 28,
        T4 = 29,
        T5 = 30,
        T6 = 31,
        S0 = 8,
        S1 = 9,
        S2 = 18,
        S3 = 19,
        S4 = 20,
        S5 = 21,
        S6 = 22,
        S7 = 23,
        S8 = 24,
        S9 = 25,
        S10 = 26,
        S11 = 27,
        A0 = 10,
        A1 = 11,
        A2 = 12,
        A3 = 13,
        A4 = 14,
        A5 = 15,
        A6 = 16,
        A7 = 17,
        F0 = 33,
        F1 = 34,
        F2 = 35,
        F3 = 36,
        F4 = 37,
        F5 = 38,
        F6 = 39,
        F7 = 40,
        F8 = 41,
        F9 = 42,
        F10 = 43,
        F11 = 44,
        F12 = 45,
        F13 = 46,
        F14 = 47,
        F15 = 48,
        F16 = 49,
        F17 = 50,
        F18 = 51,
        F19 = 52,
        F20 = 53,
        F21 = 54,
        F22 = 55,
        F23 = 56,
        F24 = 57,
        F25 = 58,
        F26 = 59,
        F27 = 60,
        F28 = 61,
        F29 = 62,
        F30 = 63,
        F31 = 64,
    }
}
