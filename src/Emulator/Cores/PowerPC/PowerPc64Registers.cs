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
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((PowerPc64Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((ulong)value));
        }

        public override RegisterValue GetRegister(int register)
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
        public RegistersGroup R { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapR = new Dictionary<int, PowerPc64Registers>
            {
                { 0, PowerPc64Registers.R0 },
                { 1, PowerPc64Registers.R1 },
                { 2, PowerPc64Registers.R2 },
                { 3, PowerPc64Registers.R3 },
                { 4, PowerPc64Registers.R4 },
                { 5, PowerPc64Registers.R5 },
                { 6, PowerPc64Registers.R6 },
                { 7, PowerPc64Registers.R7 },
                { 8, PowerPc64Registers.R8 },
                { 9, PowerPc64Registers.R9 },
                { 10, PowerPc64Registers.R10 },
                { 11, PowerPc64Registers.R11 },
                { 12, PowerPc64Registers.R12 },
                { 13, PowerPc64Registers.R13 },
                { 14, PowerPc64Registers.R14 },
                { 15, PowerPc64Registers.R15 },
                { 16, PowerPc64Registers.R16 },
                { 17, PowerPc64Registers.R17 },
                { 18, PowerPc64Registers.R18 },
                { 19, PowerPc64Registers.R19 },
                { 20, PowerPc64Registers.R20 },
                { 21, PowerPc64Registers.R21 },
                { 22, PowerPc64Registers.R22 },
                { 23, PowerPc64Registers.R23 },
                { 24, PowerPc64Registers.R24 },
                { 25, PowerPc64Registers.R25 },
                { 26, PowerPc64Registers.R26 },
                { 27, PowerPc64Registers.R27 },
                { 28, PowerPc64Registers.R28 },
                { 29, PowerPc64Registers.R29 },
                { 30, PowerPc64Registers.R30 },
                { 31, PowerPc64Registers.R31 },
            };
            R = new RegistersGroup(
                indexValueMapR.Keys,
                i => GetRegister((int)indexValueMapR[i]),
                (i, v) => SetRegister((int)indexValueMapR[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;

        #pragma warning restore 649

        private static readonly Dictionary<PowerPc64Registers, CPURegister> mapping = new Dictionary<PowerPc64Registers, CPURegister>
        {
            { PowerPc64Registers.R0,  new CPURegister(0, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R0" }) },
            { PowerPc64Registers.R1,  new CPURegister(1, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R1" }) },
            { PowerPc64Registers.R2,  new CPURegister(2, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R2" }) },
            { PowerPc64Registers.R3,  new CPURegister(3, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R3" }) },
            { PowerPc64Registers.R4,  new CPURegister(4, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R4" }) },
            { PowerPc64Registers.R5,  new CPURegister(5, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R5" }) },
            { PowerPc64Registers.R6,  new CPURegister(6, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R6" }) },
            { PowerPc64Registers.R7,  new CPURegister(7, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R7" }) },
            { PowerPc64Registers.R8,  new CPURegister(8, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { PowerPc64Registers.R9,  new CPURegister(9, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { PowerPc64Registers.R10,  new CPURegister(10, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { PowerPc64Registers.R11,  new CPURegister(11, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { PowerPc64Registers.R12,  new CPURegister(12, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { PowerPc64Registers.R13,  new CPURegister(13, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R13" }) },
            { PowerPc64Registers.R14,  new CPURegister(14, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R14" }) },
            { PowerPc64Registers.R15,  new CPURegister(15, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R15" }) },
            { PowerPc64Registers.R16,  new CPURegister(16, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R16" }) },
            { PowerPc64Registers.R17,  new CPURegister(17, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R17" }) },
            { PowerPc64Registers.R18,  new CPURegister(18, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R18" }) },
            { PowerPc64Registers.R19,  new CPURegister(19, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R19" }) },
            { PowerPc64Registers.R20,  new CPURegister(20, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R20" }) },
            { PowerPc64Registers.R21,  new CPURegister(21, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R21" }) },
            { PowerPc64Registers.R22,  new CPURegister(22, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R22" }) },
            { PowerPc64Registers.R23,  new CPURegister(23, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R23" }) },
            { PowerPc64Registers.R24,  new CPURegister(24, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R24" }) },
            { PowerPc64Registers.R25,  new CPURegister(25, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R25" }) },
            { PowerPc64Registers.R26,  new CPURegister(26, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R26" }) },
            { PowerPc64Registers.R27,  new CPURegister(27, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R27" }) },
            { PowerPc64Registers.R28,  new CPURegister(28, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R28" }) },
            { PowerPc64Registers.R29,  new CPURegister(29, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R29" }) },
            { PowerPc64Registers.R30,  new CPURegister(30, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R30" }) },
            { PowerPc64Registers.R31,  new CPURegister(31, 64, isGeneral: true, isReadonly: false, aliases: new [] { "R31" }) },
            { PowerPc64Registers.NIP,  new CPURegister(64, 64, isGeneral: true, isReadonly: false, aliases: new [] { "NIP", "PC" }) },
            { PowerPc64Registers.MSR,  new CPURegister(65, 64, isGeneral: false, isReadonly: false, aliases: new [] { "MSR" }) },
            { PowerPc64Registers.LR,  new CPURegister(67, 64, isGeneral: false, isReadonly: false, aliases: new [] { "LR" }) },
            { PowerPc64Registers.CTR,  new CPURegister(68, 64, isGeneral: false, isReadonly: false, aliases: new [] { "CTR" }) },
            { PowerPc64Registers.XER,  new CPURegister(69, 64, isGeneral: false, isReadonly: false, aliases: new [] { "XER" }) },
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
