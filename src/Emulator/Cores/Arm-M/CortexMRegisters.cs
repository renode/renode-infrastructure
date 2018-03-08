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
    public partial class CortexM
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((CortexMRegisters)register, out var r))
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
        public RegisterValue Control
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
        public RegisterValue BasePri
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
        public RegisterValue VecBase
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
        public RegisterValue CurrentSP
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
        public RegisterValue OtherSP
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

        private static readonly Dictionary<CortexMRegisters, CPURegister> mapping = new Dictionary<CortexMRegisters, CPURegister>
        {
            { CortexMRegisters.Control,  new CPURegister(18, 32, false) },
            { CortexMRegisters.BasePri,  new CPURegister(19, 32, false) },
            { CortexMRegisters.VecBase,  new CPURegister(20, 32, false) },
            { CortexMRegisters.CurrentSP,  new CPURegister(21, 32, false) },
            { CortexMRegisters.OtherSP,  new CPURegister(22, 32, false) },
        };
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
