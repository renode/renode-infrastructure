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
    public partial class Arm
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
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16,
                17,
            };
        }

        [Register]
        public UInt32 SP
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
        public UInt32 LR
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
        public override UInt32 PC
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
        public UInt32 CPSR
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

        public RegistersGroup<UInt32> R { get; private set; }

        protected override void InitializeRegisters()
        {
            indexValueMapR = new Dictionary<int, ArmRegisters>
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
            R = new RegistersGroup<UInt32>(
                indexValueMapR.Keys,
                i => GetRegisterValue32((int)indexValueMapR[i]),
                (i, v) => SetRegisterValue32((int)indexValueMapR[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private Dictionary<int, ArmRegisters> indexValueMapR;
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
