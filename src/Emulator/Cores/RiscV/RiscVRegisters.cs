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
    public partial class RiscV
    {
        public override void SetRegisterUnsafe(int register, uint value)
        {
            SetRegisterValue32(register, value);
        }

        public override uint GetRegisterUnsafe(int register)
        {
            return GetRegisterValue32(register);
        }

        public override IEnumerable<CPURegister> GetRegisters()
        {
            return new CPURegister[] {
                new CPURegister(0, true),
                new CPURegister(1, true),
                new CPURegister(2, true),
                new CPURegister(3, true),
                new CPURegister(4, true),
                new CPURegister(5, true),
                new CPURegister(6, true),
                new CPURegister(7, true),
                new CPURegister(8, true),
                new CPURegister(9, true),
                new CPURegister(10, true),
                new CPURegister(11, true),
                new CPURegister(12, true),
                new CPURegister(13, true),
                new CPURegister(14, true),
                new CPURegister(15, true),
                new CPURegister(16, true),
                new CPURegister(17, true),
                new CPURegister(18, true),
                new CPURegister(19, true),
                new CPURegister(20, true),
                new CPURegister(21, true),
                new CPURegister(22, true),
                new CPURegister(23, true),
                new CPURegister(24, true),
                new CPURegister(25, true),
                new CPURegister(26, true),
                new CPURegister(27, true),
                new CPURegister(28, true),
                new CPURegister(29, true),
                new CPURegister(30, true),
                new CPURegister(31, true),
                new CPURegister(32, true),
            };
        }

        [Register]
        public UInt32 ZERO
        {
            get
            {
                return GetRegisterValue32((int)RiscVRegisters.ZERO);
            }
            set
            {
                SetRegisterValue32((int)RiscVRegisters.ZERO, value);
            }
        }

        [Register]
        public override UInt32 PC
        {
            get
            {
                return GetRegisterValue32((int)RiscVRegisters.PC);
            }
            set
            {
                SetRegisterValue32((int)RiscVRegisters.PC, value);
            }
        }

        public RegistersGroup<UInt32> X { get; private set; }

        protected override void InitializeRegisters()
        {
            indexValueMapX = new Dictionary<int, RiscVRegisters>
            {
                { 0, RiscVRegisters.X0 },
                { 1, RiscVRegisters.X1 },
                { 2, RiscVRegisters.X2 },
                { 3, RiscVRegisters.X3 },
                { 4, RiscVRegisters.X4 },
                { 5, RiscVRegisters.X5 },
                { 6, RiscVRegisters.X6 },
                { 7, RiscVRegisters.X7 },
                { 8, RiscVRegisters.X8 },
                { 9, RiscVRegisters.X9 },
                { 10, RiscVRegisters.X10 },
                { 11, RiscVRegisters.X11 },
                { 12, RiscVRegisters.X12 },
                { 13, RiscVRegisters.X13 },
                { 14, RiscVRegisters.X14 },
                { 15, RiscVRegisters.X15 },
                { 16, RiscVRegisters.X16 },
                { 17, RiscVRegisters.X17 },
                { 18, RiscVRegisters.X18 },
                { 19, RiscVRegisters.X19 },
                { 20, RiscVRegisters.X20 },
                { 21, RiscVRegisters.X21 },
                { 22, RiscVRegisters.X22 },
                { 23, RiscVRegisters.X23 },
                { 24, RiscVRegisters.X24 },
                { 25, RiscVRegisters.X25 },
                { 26, RiscVRegisters.X26 },
                { 27, RiscVRegisters.X27 },
                { 28, RiscVRegisters.X28 },
                { 29, RiscVRegisters.X29 },
                { 30, RiscVRegisters.X30 },
                { 31, RiscVRegisters.X31 },
            };
            X = new RegistersGroup<UInt32>(
                indexValueMapX.Keys,
                i => GetRegisterValue32((int)indexValueMapX[i]),
                (i, v) => SetRegisterValue32((int)indexValueMapX[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private Dictionary<int, RiscVRegisters> indexValueMapX;
    }

    public enum RiscVRegisters
    {
        ZERO = 0,
        PC = 32,
        X0 = 0,
        X1 = 1,
        X2 = 2,
        X3 = 3,
        X4 = 4,
        X5 = 5,
        X6 = 6,
        X7 = 7,
        X8 = 8,
        X9 = 9,
        X10 = 10,
        X11 = 11,
        X12 = 12,
        X13 = 13,
        X14 = 14,
        X15 = 15,
        X16 = 16,
        X17 = 17,
        X18 = 18,
        X19 = 19,
        X20 = 20,
        X21 = 21,
        X22 = 22,
        X23 = 23,
        X24 = 24,
        X25 = 25,
        X26 = 26,
        X27 = 27,
        X28 = 28,
        X29 = 29,
        X30 = 30,
        X31 = 31,
    }
}
