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
    public partial class PowerPc
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
            };
        }

        [Register]
        public UInt32 NIP
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
        public override UInt32 PC
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

    }

    public enum PowerPcRegisters
    {
        NIP = 0,
        PC = 0,
    }
}
