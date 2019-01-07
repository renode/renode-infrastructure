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
            { PowerPcRegisters.NIP,  new CPURegister(0, 32, isGeneral: true, isReadonly: false) },
        };
    }

    public enum PowerPcRegisters
    {
        NIP = 0,
        PC = 0,
    }
}
