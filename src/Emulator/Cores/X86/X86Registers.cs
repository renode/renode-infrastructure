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
    public partial class X86
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((X86Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((X86Registers)register, out var r))
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
        public RegisterValue EAX
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EAX);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EAX, value);
            }
        }
        [Register]
        public RegisterValue ECX
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.ECX);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.ECX, value);
            }
        }
        [Register]
        public RegisterValue EDX
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EDX);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EDX, value);
            }
        }
        [Register]
        public RegisterValue EBX
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EBX);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EBX, value);
            }
        }
        [Register]
        public RegisterValue ESP
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.ESP);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.ESP, value);
            }
        }
        [Register]
        public RegisterValue EBP
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EBP);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EBP, value);
            }
        }
        [Register]
        public RegisterValue ESI
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.ESI);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.ESI, value);
            }
        }
        [Register]
        public RegisterValue EDI
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EDI);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EDI, value);
            }
        }
        [Register]
        public RegisterValue EIP
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EIP);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EIP, value);
            }
        }
        [Register]
        public RegisterValue EFLAGS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.EFLAGS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.EFLAGS, value);
            }
        }
        [Register]
        public RegisterValue CS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CS, value);
            }
        }
        [Register]
        public RegisterValue SS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.SS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.SS, value);
            }
        }
        [Register]
        public RegisterValue DS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.DS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.DS, value);
            }
        }
        [Register]
        public RegisterValue ES
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.ES);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.ES, value);
            }
        }
        [Register]
        public RegisterValue FS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.FS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.FS, value);
            }
        }
        [Register]
        public RegisterValue GS
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.GS);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.GS, value);
            }
        }
        [Register]
        public RegisterValue CR0
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CR0);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CR0, value);
            }
        }
        [Register]
        public RegisterValue CR1
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CR1);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CR1, value);
            }
        }
        [Register]
        public RegisterValue CR2
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CR2);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CR2, value);
            }
        }
        [Register]
        public RegisterValue CR3
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CR3);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CR3, value);
            }
        }
        [Register]
        public RegisterValue CR4
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.CR4);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.CR4, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)X86Registers.PC);
            }
            set
            {
                SetRegisterValue32((int)X86Registers.PC, value);
            }
        }

        protected override void InitializeRegisters()
        {
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<X86Registers, CPURegister> mapping = new Dictionary<X86Registers, CPURegister>
        {
            { X86Registers.EAX,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EAX" }) },
            { X86Registers.ECX,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ECX" }) },
            { X86Registers.EDX,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EDX" }) },
            { X86Registers.EBX,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EBX" }) },
            { X86Registers.ESP,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ESP" }) },
            { X86Registers.EBP,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EBP" }) },
            { X86Registers.ESI,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ESI" }) },
            { X86Registers.EDI,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EDI" }) },
            { X86Registers.EIP,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EIP", "PC" }) },
            { X86Registers.EFLAGS,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "EFLAGS" }) },
            { X86Registers.CS,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CS" }) },
            { X86Registers.SS,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "SS" }) },
            { X86Registers.DS,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "DS" }) },
            { X86Registers.ES,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ES" }) },
            { X86Registers.FS,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "FS" }) },
            { X86Registers.GS,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "GS" }) },
            { X86Registers.CR0,  new CPURegister(16, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR0" }) },
            { X86Registers.CR1,  new CPURegister(17, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR1" }) },
            { X86Registers.CR2,  new CPURegister(18, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR2" }) },
            { X86Registers.CR3,  new CPURegister(19, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR3" }) },
            { X86Registers.CR4,  new CPURegister(20, 32, isGeneral: true, isReadonly: false, aliases: new [] { "CR4" }) },
        };
    }

    public enum X86Registers
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
