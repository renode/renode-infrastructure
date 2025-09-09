/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/
#pragma warning disable IDE0005
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Utilities.Binding;
#pragma warning restore IDE0005

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86_64KVM
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((X86_64KVMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((ulong)value));
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((X86_64KVMRegisters)register, out var r))
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
                return GetRegisterValue64((int)X86_64KVMRegisters.RAX);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RAX, value);
            }
        }

        [Register]
        public RegisterValue RCX
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RCX);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RCX, value);
            }
        }

        [Register]
        public RegisterValue RDX
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RDX);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RDX, value);
            }
        }

        [Register]
        public RegisterValue RBX
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RBX);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RBX, value);
            }
        }

        [Register]
        public RegisterValue RSP
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RSP);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RSP, value);
            }
        }

        [Register]
        public RegisterValue RBP
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RBP);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RBP, value);
            }
        }

        [Register]
        public RegisterValue RSI
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RSI);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RSI, value);
            }
        }

        [Register]
        public RegisterValue RDI
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RDI);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RDI, value);
            }
        }

        [Register]
        public RegisterValue R8
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R8);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R8, value);
            }
        }

        [Register]
        public RegisterValue R9
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R9);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R9, value);
            }
        }

        [Register]
        public RegisterValue R10
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R10);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R10, value);
            }
        }

        [Register]
        public RegisterValue R11
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R11);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R11, value);
            }
        }

        [Register]
        public RegisterValue R12
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R12);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R12, value);
            }
        }

        [Register]
        public RegisterValue R13
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R13);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R13, value);
            }
        }

        [Register]
        public RegisterValue R14
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R14);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R14, value);
            }
        }

        [Register]
        public RegisterValue R15
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.R15);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.R15, value);
            }
        }

        [Register]
        public RegisterValue RIP
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.RIP);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.RIP, value);
            }
        }

        [Register]
        public RegisterValue EFLAGS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.EFLAGS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.EFLAGS, value);
            }
        }

        [Register]
        public RegisterValue CS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CS, value);
            }
        }

        [Register]
        public RegisterValue SS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.SS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.SS, value);
            }
        }

        [Register]
        public RegisterValue DS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.DS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.DS, value);
            }
        }

        [Register]
        public RegisterValue ES
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ES);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ES, value);
            }
        }

        [Register]
        public RegisterValue FS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.FS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.FS, value);
            }
        }

        [Register]
        public RegisterValue GS
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.GS);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.GS, value);
            }
        }

        [Register]
        public RegisterValue ST0
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST0);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST0, value);
            }
        }

        [Register]
        public RegisterValue ST1
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST1);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST1, value);
            }
        }

        [Register]
        public RegisterValue ST2
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST2);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST2, value);
            }
        }

        [Register]
        public RegisterValue ST3
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST3);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST3, value);
            }
        }

        [Register]
        public RegisterValue ST4
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST4);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST4, value);
            }
        }

        [Register]
        public RegisterValue ST5
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST5);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST5, value);
            }
        }

        [Register]
        public RegisterValue ST6
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST6);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST6, value);
            }
        }

        [Register]
        public RegisterValue ST7
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.ST7);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.ST7, value);
            }
        }

        [Register]
        public RegisterValue CR0
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR0);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR0, value);
            }
        }

        [Register]
        public RegisterValue CR1
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR1);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR1, value);
            }
        }

        [Register]
        public RegisterValue CR2
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR2);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR2, value);
            }
        }

        [Register]
        public RegisterValue CR3
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR3);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR3, value);
            }
        }

        [Register]
        public RegisterValue CR4
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR4);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR4, value);
            }
        }

        [Register]
        public RegisterValue CR8
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.CR8);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.CR8, value);
            }
        }

        [Register]
        public RegisterValue EFER
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.EFER);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.EFER, value);
            }
        }

        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)X86_64KVMRegisters.PC);
            }

            set
            {
                SetRegisterValue64((int)X86_64KVMRegisters.PC, value);
            }
        }

#pragma warning disable SA1508
        protected override void InitializeRegisters()
        {
        }
#pragma warning restore SA1508

#pragma warning disable 649
        // 649:  Field '...' is never assigned to, and will always have its default value null
        [Import(Name = "kvm_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;

        [Import(Name = "kvm_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;
#pragma warning restore 649

        private static readonly Dictionary<X86_64KVMRegisters, CPURegister> mapping = new Dictionary<X86_64KVMRegisters, CPURegister>
        {
            { X86_64KVMRegisters.RAX,  new CPURegister(0, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RAX" }) },
            { X86_64KVMRegisters.RBX,  new CPURegister(1, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBX" }) },
            { X86_64KVMRegisters.RCX,  new CPURegister(2, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RCX" }) },
            { X86_64KVMRegisters.RDX,  new CPURegister(3, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDX" }) },
            { X86_64KVMRegisters.RSP,  new CPURegister(4, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSP" }) },
            { X86_64KVMRegisters.RBP,  new CPURegister(5, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBP" }) },
            { X86_64KVMRegisters.RSI,  new CPURegister(6, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSI" }) },
            { X86_64KVMRegisters.RDI,  new CPURegister(7, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDI" }) },
            { X86_64KVMRegisters.R8,  new CPURegister(8, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { X86_64KVMRegisters.R9,  new CPURegister(9, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { X86_64KVMRegisters.R10,  new CPURegister(10, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { X86_64KVMRegisters.R11,  new CPURegister(11, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { X86_64KVMRegisters.R12,  new CPURegister(12, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { X86_64KVMRegisters.R13,  new CPURegister(13, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R13" }) },
            { X86_64KVMRegisters.R14,  new CPURegister(14, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R14" }) },
            { X86_64KVMRegisters.R15,  new CPURegister(15, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R15" }) },
            { X86_64KVMRegisters.RIP,  new CPURegister(16, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RIP", "PC" }) },
            { X86_64KVMRegisters.EFLAGS,  new CPURegister(17, 64, isGeneral: true, isReadonly: false, aliases: new [] { "EFLAGS" }) },
            { X86_64KVMRegisters.CS,  new CPURegister(18, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CS" }) },
            { X86_64KVMRegisters.SS,  new CPURegister(19, 64, isGeneral: true, isReadonly: false, aliases: new [] { "SS" }) },
            { X86_64KVMRegisters.DS,  new CPURegister(20, 64, isGeneral: true, isReadonly: false, aliases: new [] { "DS" }) },
            { X86_64KVMRegisters.ES,  new CPURegister(21, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ES" }) },
            { X86_64KVMRegisters.FS,  new CPURegister(22, 64, isGeneral: true, isReadonly: false, aliases: new [] { "FS" }) },
            { X86_64KVMRegisters.GS,  new CPURegister(23, 64, isGeneral: true, isReadonly: false, aliases: new [] { "GS" }) },
            { X86_64KVMRegisters.ST0,  new CPURegister(24, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST0" }) },
            { X86_64KVMRegisters.ST1,  new CPURegister(25, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST1" }) },
            { X86_64KVMRegisters.ST2,  new CPURegister(26, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST2" }) },
            { X86_64KVMRegisters.ST3,  new CPURegister(27, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST3" }) },
            { X86_64KVMRegisters.ST4,  new CPURegister(28, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST4" }) },
            { X86_64KVMRegisters.ST5,  new CPURegister(29, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST5" }) },
            { X86_64KVMRegisters.ST6,  new CPURegister(30, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST6" }) },
            { X86_64KVMRegisters.ST7,  new CPURegister(31, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ST7" }) },
            { X86_64KVMRegisters.CR0,  new CPURegister(32, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR0" }) },
            { X86_64KVMRegisters.CR1,  new CPURegister(33, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR1" }) },
            { X86_64KVMRegisters.CR2,  new CPURegister(34, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR2" }) },
            { X86_64KVMRegisters.CR3,  new CPURegister(35, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR3" }) },
            { X86_64KVMRegisters.CR4,  new CPURegister(36, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR4" }) },
            { X86_64KVMRegisters.CR8,  new CPURegister(40, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR8" }) },
            { X86_64KVMRegisters.EFER,  new CPURegister(41, 64, isGeneral: true, isReadonly: false, aliases: new [] { "EFER" }) },
        };
    }

    public enum X86_64KVMRegisters
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
        CR8 = 40,
        EFER = 41,
        PC = 16,
    }
}