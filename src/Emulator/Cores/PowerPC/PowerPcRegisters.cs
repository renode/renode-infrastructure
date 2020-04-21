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
        public RegistersGroup R { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapR = new Dictionary<int, PowerPcRegisters>
            {
                { 0, PowerPcRegisters.R0 },
                { 1, PowerPcRegisters.R1 },
                { 2, PowerPcRegisters.R2 },
                { 3, PowerPcRegisters.R3 },
                { 4, PowerPcRegisters.R4 },
                { 5, PowerPcRegisters.R5 },
                { 6, PowerPcRegisters.R6 },
                { 7, PowerPcRegisters.R7 },
                { 8, PowerPcRegisters.R8 },
                { 9, PowerPcRegisters.R9 },
                { 10, PowerPcRegisters.R10 },
                { 11, PowerPcRegisters.R11 },
                { 12, PowerPcRegisters.R12 },
                { 13, PowerPcRegisters.R13 },
                { 14, PowerPcRegisters.R14 },
                { 15, PowerPcRegisters.R15 },
                { 16, PowerPcRegisters.R16 },
                { 17, PowerPcRegisters.R17 },
                { 18, PowerPcRegisters.R18 },
                { 19, PowerPcRegisters.R19 },
                { 20, PowerPcRegisters.R20 },
                { 21, PowerPcRegisters.R21 },
                { 22, PowerPcRegisters.R22 },
                { 23, PowerPcRegisters.R23 },
                { 24, PowerPcRegisters.R24 },
                { 25, PowerPcRegisters.R25 },
                { 26, PowerPcRegisters.R26 },
                { 27, PowerPcRegisters.R27 },
                { 28, PowerPcRegisters.R28 },
                { 29, PowerPcRegisters.R29 },
                { 30, PowerPcRegisters.R30 },
                { 31, PowerPcRegisters.R31 },
            };
            R = new RegistersGroup(
                indexValueMapR.Keys,
                i => GetRegisterUnsafe((int)indexValueMapR[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapR[i], v));

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
            { PowerPcRegisters.R0,  new CPURegister(0, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R1,  new CPURegister(1, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R2,  new CPURegister(2, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R3,  new CPURegister(3, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R4,  new CPURegister(4, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R5,  new CPURegister(5, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R6,  new CPURegister(6, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R7,  new CPURegister(7, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R8,  new CPURegister(8, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R9,  new CPURegister(9, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R10,  new CPURegister(10, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R11,  new CPURegister(11, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R12,  new CPURegister(12, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R13,  new CPURegister(13, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R14,  new CPURegister(14, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R15,  new CPURegister(15, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R16,  new CPURegister(16, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R17,  new CPURegister(17, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R18,  new CPURegister(18, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R19,  new CPURegister(19, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R20,  new CPURegister(20, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R21,  new CPURegister(21, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R22,  new CPURegister(22, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R23,  new CPURegister(23, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R24,  new CPURegister(24, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R25,  new CPURegister(25, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R26,  new CPURegister(26, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R27,  new CPURegister(27, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R28,  new CPURegister(28, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R29,  new CPURegister(29, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R30,  new CPURegister(30, 32, isGeneral: false, isReadonly: false) },
            { PowerPcRegisters.R31,  new CPURegister(31, 32, isGeneral: false, isReadonly: false) },
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
        R0 = 0,
        R1 = 1,
        R2 = 2,
        R3 = 3,
        R4 = 4,
        R5 = 5,
        R6 = 6,
        R7 = 7,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11,
        R12 = 12,
        R13 = 13,
        R14 = 14,
        R15 = 15,
        R16 = 16,
        R17 = 17,
        R18 = 18,
        R19 = 19,
        R20 = 20,
        R21 = 21,
        R22 = 22,
        R23 = 23,
        R24 = 24,
        R25 = 25,
        R26 = 26,
        R27 = 27,
        R28 = 28,
        R29 = 29,
        R30 = 30,
        R31 = 31,
    }
}
