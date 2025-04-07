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
            { X86_64KVMRegisters.RCX,  new CPURegister(1, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RCX" }) },
            { X86_64KVMRegisters.RDX,  new CPURegister(2, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDX" }) },
            { X86_64KVMRegisters.RBX,  new CPURegister(3, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBX" }) },
            { X86_64KVMRegisters.RSP,  new CPURegister(4, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSP" }) },
            { X86_64KVMRegisters.RBP,  new CPURegister(5, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RBP" }) },
            { X86_64KVMRegisters.RSI,  new CPURegister(6, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RSI" }) },
            { X86_64KVMRegisters.RDI,  new CPURegister(7, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RDI" }) },
            { X86_64KVMRegisters.RIP,  new CPURegister(8, 64, isGeneral: true, isReadonly: false, aliases: new [] { "RIP", "PC" }) },
            { X86_64KVMRegisters.EFLAGS,  new CPURegister(9, 64, isGeneral: true, isReadonly: false, aliases: new [] { "EFLAGS" }) },
            { X86_64KVMRegisters.CS,  new CPURegister(10, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CS" }) },
            { X86_64KVMRegisters.SS,  new CPURegister(11, 64, isGeneral: true, isReadonly: false, aliases: new [] { "SS" }) },
            { X86_64KVMRegisters.DS,  new CPURegister(12, 64, isGeneral: true, isReadonly: false, aliases: new [] { "DS" }) },
            { X86_64KVMRegisters.ES,  new CPURegister(13, 64, isGeneral: true, isReadonly: false, aliases: new [] { "ES" }) },
            { X86_64KVMRegisters.FS,  new CPURegister(14, 64, isGeneral: true, isReadonly: false, aliases: new [] { "FS" }) },
            { X86_64KVMRegisters.GS,  new CPURegister(15, 64, isGeneral: true, isReadonly: false, aliases: new [] { "GS" }) },
            { X86_64KVMRegisters.CR0,  new CPURegister(16, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR0" }) },
            { X86_64KVMRegisters.CR1,  new CPURegister(17, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR1" }) },
            { X86_64KVMRegisters.CR2,  new CPURegister(18, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR2" }) },
            { X86_64KVMRegisters.CR3,  new CPURegister(19, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR3" }) },
            { X86_64KVMRegisters.CR4,  new CPURegister(20, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR4" }) },
            { X86_64KVMRegisters.CR8,  new CPURegister(24, 64, isGeneral: true, isReadonly: false, aliases: new [] { "CR8" }) },
            { X86_64KVMRegisters.EFER,  new CPURegister(25, 64, isGeneral: true, isReadonly: false, aliases: new [] { "EFER" }) },
        };
    }

    public enum X86_64KVMRegisters
    {
        RAX = 0,
        RCX = 1,
        RDX = 2,
        RBX = 3,
        RSP = 4,
        RBP = 5,
        RSI = 6,
        RDI = 7,
        RIP = 8,
        EFLAGS = 9,
        CS = 10,
        SS = 11,
        DS = 12,
        ES = 13,
        FS = 14,
        GS = 15,
        CR0 = 16,
        CR1 = 17,
        CR2 = 18,
        CR3 = 19,
        CR4 = 20,
        CR8 = 24,
        EFER = 25,
        PC = 8,
    }
}