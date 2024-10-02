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
    public partial class X86_64
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((X86_64Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((UInt64)value));
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((X86_64Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            return GetRegisterValue64(r.Index);
        }

        public override IEnumerable<CPURegister> GetRegisters()
        {
            return mapping.Values.OrderBy(x => x.Index);
        }

        [Register]
        public RegisterValue RAX
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RAX);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RAX, value);
            }
        }
        [Register]
        public RegisterValue RCX
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RCX);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RCX, value);
            }
        }
        [Register]
        public RegisterValue RDX
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RDX);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RDX, value);
            }
        }
        [Register]
        public RegisterValue RBX
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RBX);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RBX, value);
            }
        }
        [Register]
        public RegisterValue RSP
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RSP);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RSP, value);
            }
        }
        [Register]
        public RegisterValue RBP
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RBP);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RBP, value);
            }
        }
        [Register]
        public RegisterValue RSI
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RSI);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RSI, value);
            }
        }
        [Register]
        public RegisterValue RDI
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RDI);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RDI, value);
            }
        }
        [Register]
        public RegisterValue R8
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R8);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R8, value);
            }
        }
        [Register]
        public RegisterValue R9
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R9);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R9, value);
            }
        }
        [Register]
        public RegisterValue R10
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R10);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R10, value);
            }
        }
        [Register]
        public RegisterValue R11
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R11);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R11, value);
            }
        }
        [Register]
        public RegisterValue R12
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R12);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R12, value);
            }
        }
        [Register]
        public RegisterValue R13
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R13);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R13, value);
            }
        }
        [Register]
        public RegisterValue R14
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R14);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R14, value);
            }
        }
        [Register]
        public RegisterValue R15
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.R15);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.R15, value);
            }
        }
        [Register]
        public RegisterValue RIP
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.RIP);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.RIP, value);
            }
        }
        [Register]
        public RegisterValue EFLAGS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.EFLAGS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.EFLAGS, value);
            }
        }
        [Register]
        public RegisterValue CS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CS, value);
            }
        }
        [Register]
        public RegisterValue SS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.SS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.SS, value);
            }
        }
        [Register]
        public RegisterValue DS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.DS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.DS, value);
            }
        }
        [Register]
        public RegisterValue ES
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ES);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ES, value);
            }
        }
        [Register]
        public RegisterValue FS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.FS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.FS, value);
            }
        }
        [Register]
        public RegisterValue GS
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.GS);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.GS, value);
            }
        }
        [Register]
        public RegisterValue ST0
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST0);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST0, value);
            }
        }
        [Register]
        public RegisterValue ST1
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST1);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST1, value);
            }
        }
        [Register]
        public RegisterValue ST2
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST2);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST2, value);
            }
        }
        [Register]
        public RegisterValue ST3
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST3);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST3, value);
            }
        }
        [Register]
        public RegisterValue ST4
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST4);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST4, value);
            }
        }
        [Register]
        public RegisterValue ST5
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST5);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST5, value);
            }
        }
        [Register]
        public RegisterValue ST6
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST6);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST6, value);
            }
        }
        [Register]
        public RegisterValue ST7
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.ST7);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.ST7, value);
            }
        }
        [Register]
        public RegisterValue CR0
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CR0);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CR0, value);
            }
        }
        [Register]
        public RegisterValue CR1
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CR1);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CR1, value);
            }
        }
        [Register]
        public RegisterValue CR2
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CR2);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CR2, value);
            }
        }
        [Register]
        public RegisterValue CR3
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CR3);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CR3, value);
            }
        }
        [Register]
        public RegisterValue CR4
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.CR4);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.CR4, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)X86_64Registers.PC);
            }
            set
            {
                SetRegisterValue64((int)X86_64Registers.PC, value);
            }
        }

        protected override void InitializeRegisters()
        {
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected ActionInt32UInt64 SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected FuncUInt64Int32 GetRegisterValue64;

        #pragma warning restore 649

        private static readonly Dictionary<X86_64Registers, CPURegister> mapping = new Dictionary<X86_64Registers, CPURegister>
        {
            { X86_64Registers.RAX,  new CPURegister(0, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RAX" }) },
            { X86_64Registers.RBX,  new CPURegister(1, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBX" }) },
            { X86_64Registers.RCX,  new CPURegister(2, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RCX" }) },
            { X86_64Registers.RDX,  new CPURegister(3, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDX" }) },
            { X86_64Registers.RSP,  new CPURegister(4, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSP" }) },
            { X86_64Registers.RBP,  new CPURegister(5, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBP" }) },
            { X86_64Registers.RSI,  new CPURegister(6, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSI" }) },
            { X86_64Registers.RDI,  new CPURegister(7, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDI" }) },
            { X86_64Registers.R8,  new CPURegister(8, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { X86_64Registers.R9,  new CPURegister(9, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { X86_64Registers.R10,  new CPURegister(10, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { X86_64Registers.R11,  new CPURegister(11, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { X86_64Registers.R12,  new CPURegister(12, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { X86_64Registers.R13,  new CPURegister(13, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R13" }) },
            { X86_64Registers.R14,  new CPURegister(14, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R14" }) },
            { X86_64Registers.R15,  new CPURegister(15, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R15" }) },
            { X86_64Registers.RIP,  new CPURegister(16, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RIP", "PC" }) },
            { X86_64Registers.EFLAGS,  new CPURegister(17, 64, isGeneral: true, isReadonly: false, aliases: new [] { "EFLAGS" }) },
            { X86_64Registers.CS,  new CPURegister(18, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CS" }) },
            { X86_64Registers.SS,  new CPURegister(19, 64, isGeneral: true, isReadonly: false, aliases: new [] { "SS" }) },
            { X86_64Registers.DS,  new CPURegister(20, 64, isGeneral: true, isReadonly: false, aliases: new [] { "DS" }) },
            { X86_64Registers.ES,  new CPURegister(21, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ES" }) },
            { X86_64Registers.FS,  new CPURegister(22, 64, isGeneral: true, isReadonly: false, aliases: new [] { "FS" }) },
            { X86_64Registers.GS,  new CPURegister(23, 64, isGeneral: true, isReadonly: false, aliases: new [] { "GS" }) },
            { X86_64Registers.ST0,  new CPURegister(24, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST0" }) },
            { X86_64Registers.ST1,  new CPURegister(25, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST1" }) },
            { X86_64Registers.ST2,  new CPURegister(26, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST2" }) },
            { X86_64Registers.ST3,  new CPURegister(27, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST3" }) },
            { X86_64Registers.ST4,  new CPURegister(28, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST4" }) },
            { X86_64Registers.ST5,  new CPURegister(29, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST5" }) },
            { X86_64Registers.ST6,  new CPURegister(30, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST6" }) },
            { X86_64Registers.ST7,  new CPURegister(31, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST7" }) },
            { X86_64Registers.CR0,  new CPURegister(32, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CR0" }) },
            { X86_64Registers.CR1,  new CPURegister(33, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CR1" }) },
            { X86_64Registers.CR2,  new CPURegister(34, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CR2" }) },
            { X86_64Registers.CR3,  new CPURegister(35, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CR3" }) },
            { X86_64Registers.CR4,  new CPURegister(36, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CR4" }) },
        };
    }

    public enum X86_64Registers
    {
        RAX = 0,
        RCX = 2,
        RDX = 3,
        RBX = 1,
        RSP = 4,
        RBP = 5,
        RSI = 6,
        RDI = 7,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11,
        R12 = 12,
        R13 = 13,
        R14 = 14,
        R15 = 15,
        RIP = 16,
        EFLAGS = 17,
        CS = 18,
        SS = 19,
        DS = 20,
        ES = 21,
        FS = 22,
        GS = 23,
        ST0 = 24,
        ST1 = 25,
        ST2 = 26,
        ST3 = 27,
        ST4 = 28,
        ST5 = 29,
        ST6 = 30,
        ST7 = 31,
        CR0 = 32,
        CR1 = 33,
        CR2 = 34,
        CR3 = 35,
        CR4 = 36,
        PC = 16,
    }
}
