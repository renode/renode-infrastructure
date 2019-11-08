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
            if(!mapping.TryGetValue((PowerPcRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((PowerPcRegisters)register, out var r))
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
                return GetRegisterValue64((int)PowerPcRegisters.NIP);
            }
            set
            {
                SetRegisterValue64((int)PowerPcRegisters.NIP, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.PC);
            }
            set
            {
                SetRegisterValue64((int)PowerPcRegisters.PC, value);
            }
        }
        [Register]
        public RegisterValue SRR0
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.SRR0);
            }
        }
                [Register]
        public RegisterValue SRR1
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.SRR1);
            }
        }
        [Register]
        public RegisterValue LPCR
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.LPCR);
            }
            set
            {
                SetRegisterValue64((int)PowerPcRegisters.LPCR, value);
            }
        }

        public RegisterValue MSR
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.MSR);
            }
            set
            {
                SetRegisterValue64((int)PowerPcRegisters.MSR, value);
            }
        }

        public RegisterValue LR
        {
            get
            {
                return GetRegisterValue64((int)PowerPcRegisters.LR);
            }
            set
            {
                SetRegisterValue64((int)PowerPcRegisters.LR, value);
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

        private static readonly Dictionary<PowerPcRegisters, CPURegister> mapping = new Dictionary<PowerPcRegisters, CPURegister>
        {
            { PowerPcRegisters.NIP,  new CPURegister(0, 64, isGeneral: true, isReadonly: false) },
            { PowerPcRegisters.MSR,  new CPURegister(2, 64, isGeneral: false, isReadonly: true) },
            { PowerPcRegisters.LR,   new CPURegister(3, 64, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.SRR0, new CPURegister(100, 64, isGeneral: false, isReadonly: true) },
            { PowerPcRegisters.SRR1, new CPURegister(101, 64, isGeneral: false, isReadonly: true) },
            { PowerPcRegisters.LPCR, new CPURegister(200, 64, isGeneral: false, isReadonly: false) },
        };
    }

    public enum PowerPcRegisters
    {
        NIP = 0,
        PC = 0,
        MSR = 2,
        LR = 3,
        SRR0 = 100,
        SRR1 = 101,
        LPCR = 200,
    }
}
