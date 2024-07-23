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
    public partial class X86KVM
    {
        public void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((X86KVMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((uint)value));
        }

        public RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((X86KVMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            return GetRegisterValue32(r.Index);
        }

        public IEnumerable<CPURegister> GetRegisters()
        {
            return mapping.Values.OrderBy(x => x.Index);
        }

        [Register]
        public RegisterValue EAX
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EAX);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EAX, value);
            }
        }
        [Register]
        public RegisterValue ECX
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.ECX);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.ECX, value);
            }
        }
        [Register]
        public RegisterValue EDX
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EDX);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EDX, value);
            }
        }
        [Register]
        public RegisterValue EBX
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EBX);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EBX, value);
            }
        }
        [Register]
        public RegisterValue ESP
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.ESP);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.ESP, value);
            }
        }
        [Register]
        public RegisterValue EBP
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EBP);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EBP, value);
            }
        }
        [Register]
        public RegisterValue ESI
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.ESI);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.ESI, value);
            }
        }
        [Register]
        public RegisterValue EDI
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EDI);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EDI, value);
            }
        }
        [Register]
        public RegisterValue EIP
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EIP);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EIP, value);
            }
        }
        [Register]
        public RegisterValue EFLAGS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.EFLAGS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.EFLAGS, value);
            }
        }
        [Register]
        public RegisterValue CS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CS, value);
            }
        }
        [Register]
        public RegisterValue SS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.SS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.SS, value);
            }
        }
        [Register]
        public RegisterValue DS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.DS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.DS, value);
            }
        }
        [Register]
        public RegisterValue ES
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.ES);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.ES, value);
            }
        }
        [Register]
        public RegisterValue FS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.FS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.FS, value);
            }
        }
        [Register]
        public RegisterValue GS
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.GS);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.GS, value);
            }
        }
        [Register]
        public RegisterValue CR0
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CR0);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CR0, value);
            }
        }
        [Register]
        public RegisterValue CR1
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CR1);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CR1, value);
            }
        }
        [Register]
        public RegisterValue CR2
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CR2);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CR2, value);
            }
        }
        [Register]
        public RegisterValue CR3
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CR3);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CR3, value);
            }
        }
        [Register]
        public RegisterValue CR4
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.CR4);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.CR4, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)X86KVMRegisters.PC);
            }
            set
            {
                SetRegisterValue32((int)X86KVMRegisters.PC, value);
            }
        }

        protected void InitializeRegisters()
        {
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "kvm_set_register_value_32")]
        protected Action<int, uint> SetRegisterValue32;
        [Import(Name = "kvm_get_register_value_32")]
        protected Func<int, uint> GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<X86KVMRegisters, CPURegister> mapping = new Dictionary<X86KVMRegisters, CPURegister>
        {
            { X86KVMRegisters.EAX,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EAX" }) },
            { X86KVMRegisters.ECX,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ECX" }) },
            { X86KVMRegisters.EDX,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EDX" }) },
            { X86KVMRegisters.EBX,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EBX" }) },
            { X86KVMRegisters.ESP,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ESP" }) },
            { X86KVMRegisters.EBP,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EBP" }) },
            { X86KVMRegisters.ESI,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ESI" }) },
            { X86KVMRegisters.EDI,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EDI" }) },
            { X86KVMRegisters.EIP,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EIP", "PC" }) },
            { X86KVMRegisters.EFLAGS,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EFLAGS" }) },
            { X86KVMRegisters.CS,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CS" }) },
            { X86KVMRegisters.SS,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "SS" }) },
            { X86KVMRegisters.DS,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "DS" }) },
            { X86KVMRegisters.ES,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ES" }) },
            { X86KVMRegisters.FS,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "FS" }) },
            { X86KVMRegisters.GS,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "GS" }) },
            { X86KVMRegisters.CR0,  new CPURegister(16, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR0" }) },
            { X86KVMRegisters.CR1,  new CPURegister(17, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR1" }) },
            { X86KVMRegisters.CR2,  new CPURegister(18, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR2" }) },
            { X86KVMRegisters.CR3,  new CPURegister(19, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR3" }) },
            { X86KVMRegisters.CR4,  new CPURegister(20, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR4" }) },
        };
    }

    public enum X86KVMRegisters
    {
        EAX = 0,
        ECX = 1,
        EDX = 2,
        EBX = 3,
        ESP = 4,
        EBP = 5,
        ESI = 6,
        EDI = 7,
        EIP = 8,
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
        PC = 8,
    }
}
