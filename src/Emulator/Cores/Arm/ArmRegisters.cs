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
    public partial class Arm
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((ArmRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((ArmRegisters)register, out var r))
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
        public RegisterValue SP
        {
            get
            {
                return GetRegisterValue32((int)ArmRegisters.SP);
            }
            set
            {
                SetRegisterValue32((int)ArmRegisters.SP, value);
            }
        }
        [Register]
        public RegisterValue LR
        {
            get
            {
                return GetRegisterValue32((int)ArmRegisters.LR);
            }
            set
            {
                SetRegisterValue32((int)ArmRegisters.LR, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)ArmRegisters.PC);
            }
            set
            {
                value = BeforePCWrite(value);
                SetRegisterValue32((int)ArmRegisters.PC, value);
            }
        }
        [Register]
        public RegisterValue CPSR
        {
            get
            {
                return GetRegisterValue32((int)ArmRegisters.CPSR);
            }
            set
            {
                SetRegisterValue32((int)ArmRegisters.CPSR, value);
            }
        }
        public RegistersGroup R { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapR = new Dictionary<int, ArmRegisters>
            {
                { 0, ArmRegisters.R0 },
                { 1, ArmRegisters.R1 },
                { 2, ArmRegisters.R2 },
                { 3, ArmRegisters.R3 },
                { 4, ArmRegisters.R4 },
                { 5, ArmRegisters.R5 },
                { 6, ArmRegisters.R6 },
                { 7, ArmRegisters.R7 },
                { 8, ArmRegisters.R8 },
                { 9, ArmRegisters.R9 },
                { 10, ArmRegisters.R10 },
                { 11, ArmRegisters.R11 },
                { 12, ArmRegisters.R12 },
                { 13, ArmRegisters.R13 },
                { 14, ArmRegisters.R14 },
                { 15, ArmRegisters.R15 },
            };
            R = new RegistersGroup(
                indexValueMapR.Keys,
                i => GetRegister((int)indexValueMapR[i]),
                (i, v) => SetRegister((int)indexValueMapR[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<ArmRegisters, CPURegister> mapping = new Dictionary<ArmRegisters, CPURegister>
        {
            { ArmRegisters.R0,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R0" }) },
            { ArmRegisters.R1,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R1" }) },
            { ArmRegisters.R2,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R2" }) },
            { ArmRegisters.R3,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R3" }) },
            { ArmRegisters.R4,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R4" }) },
            { ArmRegisters.R5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R5" }) },
            { ArmRegisters.R6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R6" }) },
            { ArmRegisters.R7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R7" }) },
            { ArmRegisters.R8,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { ArmRegisters.R9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { ArmRegisters.R10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { ArmRegisters.R11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { ArmRegisters.R12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { ArmRegisters.SP,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "SP", "R13" }) },
            { ArmRegisters.LR,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "LR", "R14" }) },
            { ArmRegisters.PC,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PC", "R15" }) },
            { ArmRegisters.CPSR,  new CPURegister(25, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CPSR" }) },
        };
    }

    public enum ArmRegisters
    {
        SP = 13,
        LR = 14,
        PC = 15,
        CPSR = 25,
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
    }
}
