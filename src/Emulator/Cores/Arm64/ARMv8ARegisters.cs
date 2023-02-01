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
    public partial class ARMv8A
    {
        public override void SetRegisterUnsafe(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((ARMv8ARegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            switch(r.Width)
            {
            case 32:
                SetRegisterValue32(r.Index, checked((UInt32)value));
                break;
            case 64:
                SetRegisterValue64(r.Index, checked((UInt64)value));
                break;
            default:
                throw new ArgumentException($"Unsupported register width: {r.Width}");
            }
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((ARMv8ARegisters)register, out var r))
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
        public RegisterValue LR
        {
            get
            {
                return GetRegisterValue64((int)ARMv8ARegisters.LR);
            }
            set
            {
                SetRegisterValue64((int)ARMv8ARegisters.LR, value);
            }
        }
        [Register]
        public RegisterValue SP
        {
            get
            {
                return GetRegisterValue64((int)ARMv8ARegisters.SP);
            }
            set
            {
                SetRegisterValue64((int)ARMv8ARegisters.SP, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)ARMv8ARegisters.PC);
            }
            set
            {
                SetRegisterValue64((int)ARMv8ARegisters.PC, value);
            }
        }
        [Register]
        public RegisterValue PSTATE
        {
            get
            {
                return GetRegisterValue32((int)ARMv8ARegisters.PSTATE);
            }
            set
            {
                SetRegisterValue32((int)ARMv8ARegisters.PSTATE, value);
            }
        }
        [Register]
        public RegisterValue CPSR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8ARegisters.CPSR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8ARegisters.CPSR, value);
            }
        }
        [Register]
        public RegisterValue FPSR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8ARegisters.FPSR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8ARegisters.FPSR, value);
            }
        }
        [Register]
        public RegisterValue FPCR
        {
            get
            {
                return GetRegisterValue32((int)ARMv8ARegisters.FPCR);
            }
            set
            {
                SetRegisterValue32((int)ARMv8ARegisters.FPCR, value);
            }
        }
        public RegistersGroup X { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, ARMv8ARegisters>
            {
                { 0, ARMv8ARegisters.X0 },
                { 1, ARMv8ARegisters.X1 },
                { 2, ARMv8ARegisters.X2 },
                { 3, ARMv8ARegisters.X3 },
                { 4, ARMv8ARegisters.X4 },
                { 5, ARMv8ARegisters.X5 },
                { 6, ARMv8ARegisters.X6 },
                { 7, ARMv8ARegisters.X7 },
                { 8, ARMv8ARegisters.X8 },
                { 9, ARMv8ARegisters.X9 },
                { 10, ARMv8ARegisters.X10 },
                { 11, ARMv8ARegisters.X11 },
                { 12, ARMv8ARegisters.X12 },
                { 13, ARMv8ARegisters.X13 },
                { 14, ARMv8ARegisters.X14 },
                { 15, ARMv8ARegisters.X15 },
                { 16, ARMv8ARegisters.X16 },
                { 17, ARMv8ARegisters.X17 },
                { 18, ARMv8ARegisters.X18 },
                { 19, ARMv8ARegisters.X19 },
                { 20, ARMv8ARegisters.X20 },
                { 21, ARMv8ARegisters.X21 },
                { 22, ARMv8ARegisters.X22 },
                { 23, ARMv8ARegisters.X23 },
                { 24, ARMv8ARegisters.X24 },
                { 25, ARMv8ARegisters.X25 },
                { 26, ARMv8ARegisters.X26 },
                { 27, ARMv8ARegisters.X27 },
                { 28, ARMv8ARegisters.X28 },
                { 29, ARMv8ARegisters.X29 },
                { 30, ARMv8ARegisters.X30 },
                { 31, ARMv8ARegisters.X31 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegisterUnsafe((int)indexValueMapX[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapX[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected ActionInt32UInt64 SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected FuncUInt64Int32 GetRegisterValue64;

        #pragma warning restore 649

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<ARMv8ARegisters, CPURegister> mapping = new Dictionary<ARMv8ARegisters, CPURegister>
        {
            { ARMv8ARegisters.X0,  new CPURegister(0, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X1,  new CPURegister(1, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X2,  new CPURegister(2, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X3,  new CPURegister(3, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X4,  new CPURegister(4, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X5,  new CPURegister(5, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X6,  new CPURegister(6, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X7,  new CPURegister(7, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X8,  new CPURegister(8, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X9,  new CPURegister(9, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X10,  new CPURegister(10, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X11,  new CPURegister(11, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X12,  new CPURegister(12, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X13,  new CPURegister(13, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X14,  new CPURegister(14, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X15,  new CPURegister(15, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X16,  new CPURegister(16, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X17,  new CPURegister(17, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X18,  new CPURegister(18, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X19,  new CPURegister(19, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X20,  new CPURegister(20, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X21,  new CPURegister(21, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X22,  new CPURegister(22, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X23,  new CPURegister(23, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X24,  new CPURegister(24, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X25,  new CPURegister(25, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X26,  new CPURegister(26, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X27,  new CPURegister(27, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X28,  new CPURegister(28, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.X29,  new CPURegister(29, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.LR,  new CPURegister(30, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.SP,  new CPURegister(31, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.PC,  new CPURegister(32, 64, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.PSTATE,  new CPURegister(33, 32, isGeneral: true, isReadonly: false) },
            { ARMv8ARegisters.FPSR,  new CPURegister(66, 32, isGeneral: false, isReadonly: false) },
            { ARMv8ARegisters.FPCR,  new CPURegister(67, 32, isGeneral: false, isReadonly: false) },
        };
    }

    public enum ARMv8ARegisters
    {
        LR = 30,
        SP = 31,
        PC = 32,
        PSTATE = 33,
        CPSR = 33,
        FPSR = 66,
        FPCR = 67,
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
    }
}
