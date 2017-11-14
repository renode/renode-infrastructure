/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class X86
    {
        public override void SetRegisterUnsafe(int register, uint value)
        {
            SetRegisterValue32(register, value);
        }

        public override uint GetRegisterUnsafe(int register)
        {
            return GetRegisterValue32(register);
        }
        
        public override int[] GetRegisters()
        {
            return new int[] {
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16,
                17,
                18,
                19,
                20,
            };
        }

        [Register]
        public UInt32 EAX
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
        public UInt32 ECX
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
        public UInt32 EDX
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
        public UInt32 EBX
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
        public UInt32 ESP
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
        public UInt32 EBP
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
        public UInt32 ESI
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
        public UInt32 EDI
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
        public UInt32 EIP
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
        public UInt32 EFLAGS
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
        public UInt32 CS
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
        public UInt32 SS
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
        public UInt32 DS
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
        public UInt32 ES
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
        public UInt32 FS
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
        public UInt32 GS
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
        public UInt32 CR0
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
        public UInt32 CR1
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
        public UInt32 CR2
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
        public UInt32 CR3
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
        public UInt32 CR4
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
        public override UInt32 PC
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
