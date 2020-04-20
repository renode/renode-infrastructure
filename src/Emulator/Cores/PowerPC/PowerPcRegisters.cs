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
    public partial class PowerPc
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((PowerPcRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((PowerPcRegisters)register, out var r))
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
        public RegisterValue NIP
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.NIP);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.NIP, value);
            }
        }
        [Register]
        public RegisterValue MSR
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.MSR);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.MSR, value);
            }
        }
        [Register]
        public RegisterValue LR
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.LR);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.LR, value);
            }
        }
        [Register]
        public RegisterValue CTR
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.CTR);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.CTR, value);
            }
        }
        [Register]
        public RegisterValue XER
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.XER);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.XER, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)PowerPcRegisters.PC);
            }
            set
            {
                SetRegisterValue32((int)PowerPcRegisters.PC, value);
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

        private static readonly Dictionary<PowerPcRegisters, CPURegister> mapping = new Dictionary<PowerPcRegisters, CPURegister>
        {
            { PowerPcRegisters.NIP,  new CPURegister(64, 32, isGeneral: true, isReadonly: false) },
            { PowerPcRegisters.MSR,  new CPURegister(65, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.LR,  new CPURegister(67, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.CTR,  new CPURegister(68, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.XER,  new CPURegister(69, 32, isGeneral: false, isReadonly: false) },
        };
    }

    public enum PowerPcRegisters
    {
        NIP = 64,
        MSR = 65,
        LR = 67,
        CTR = 68,
        XER = 69,
        PC = 64,
    }
}
