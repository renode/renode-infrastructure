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
    public partial class PowerPc64
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((PowerPc64Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((UInt64)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((PowerPc64Registers)register, out var r))
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
        public RegisterValue NIP
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.NIP);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.NIP, value);
            }
        }
        [Register]
        public RegisterValue MSR
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.MSR);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.MSR, value);
            }
        }
        [Register]
        public RegisterValue LR
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.LR);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.LR, value);
            }
        }
        [Register]
        public RegisterValue CTR
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.CTR);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.CTR, value);
            }
        }
        [Register]
        public RegisterValue XER
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.XER);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.XER, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)PowerPc64Registers.PC);
            }
            set
            {
                SetRegisterValue64((int)PowerPc64Registers.PC, value);
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

        private static readonly Dictionary<PowerPc64Registers, CPURegister> mapping = new Dictionary<PowerPc64Registers, CPURegister>
        {
            { PowerPc64Registers.NIP,  new CPURegister(64, 64, isGeneral: true, isReadonly: false) },
            { PowerPc64Registers.MSR,  new CPURegister(65, 64, isGeneral: false, isReadonly: false) },
            { PowerPc64Registers.LR,  new CPURegister(67, 64, isGeneral: false, isReadonly: false) },
            { PowerPc64Registers.CTR,  new CPURegister(68, 64, isGeneral: false, isReadonly: false) },
            { PowerPc64Registers.XER,  new CPURegister(69, 64, isGeneral: false, isReadonly: false) },
        };
    }

    public enum PowerPc64Registers
    {
        NIP = 64,
        MSR = 65,
        LR = 67,
        CTR = 68,
        XER = 69,
        PC = 64,
    }
}
