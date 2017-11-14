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
    public partial class CortexM
    {
        public override void SetRegisterUnsafe(int register, uint value)
        {
            SetRegisterValue32(register, value);
        }

        public override uint GetRegisterUnsafe(int register)
        {
            return GetRegisterValue32(register);
        }

        [Register]
        public UInt32 Control
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.Control);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.Control, value);
            }
        }

        [Register]
        public UInt32 BasePri
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.BasePri);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.BasePri, value);
            }
        }

        [Register]
        public UInt32 VecBase
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.VecBase);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.VecBase, value);
            }
        }

        [Register]
        public UInt32 CurrentSP
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.CurrentSP);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.CurrentSP, value);
            }
        }

        [Register]
        public UInt32 OtherSP
        {
            get
            {
                return GetRegisterValue32((int)CortexMRegisters.OtherSP);
            }
            set
            {
                SetRegisterValue32((int)CortexMRegisters.OtherSP, value);
            }
        }


        protected override void InitializeRegisters()
        {
            base.InitializeRegisters();
        }

    }

    public enum CortexMRegisters
    {
        Control = 18,
        BasePri = 19,
        VecBase = 20,
        CurrentSP = 21,
        OtherSP = 22,
    }
}
