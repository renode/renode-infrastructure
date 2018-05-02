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
    public partial class RiscV32
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((RiscV32Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((RiscV32Registers)register, out var r))
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
        public RegisterValue ZERO
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.ZERO);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.ZERO, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)RiscV32Registers.PC);
            }
            set
            {
                SetRegisterValue32((int)RiscV32Registers.PC, value);
            }
        }
        public RegistersGroup X { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, RiscV32Registers>
            {
                { 0, RiscV32Registers.X0 },
                { 1, RiscV32Registers.X1 },
                { 2, RiscV32Registers.X2 },
                { 3, RiscV32Registers.X3 },
                { 4, RiscV32Registers.X4 },
                { 5, RiscV32Registers.X5 },
                { 6, RiscV32Registers.X6 },
                { 7, RiscV32Registers.X7 },
                { 8, RiscV32Registers.X8 },
                { 9, RiscV32Registers.X9 },
                { 10, RiscV32Registers.X10 },
                { 11, RiscV32Registers.X11 },
                { 12, RiscV32Registers.X12 },
                { 13, RiscV32Registers.X13 },
                { 14, RiscV32Registers.X14 },
                { 15, RiscV32Registers.X15 },
                { 16, RiscV32Registers.X16 },
                { 17, RiscV32Registers.X17 },
                { 18, RiscV32Registers.X18 },
                { 19, RiscV32Registers.X19 },
                { 20, RiscV32Registers.X20 },
                { 21, RiscV32Registers.X21 },
                { 22, RiscV32Registers.X22 },
                { 23, RiscV32Registers.X23 },
                { 24, RiscV32Registers.X24 },
                { 25, RiscV32Registers.X25 },
                { 26, RiscV32Registers.X26 },
                { 27, RiscV32Registers.X27 },
                { 28, RiscV32Registers.X28 },
                { 29, RiscV32Registers.X29 },
                { 30, RiscV32Registers.X30 },
                { 31, RiscV32Registers.X31 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegisterUnsafe((int)indexValueMapX[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapX[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<RiscV32Registers, CPURegister> mapping = new Dictionary<RiscV32Registers, CPURegister>
        {
            { RiscV32Registers.ZERO,  new CPURegister(0, 32, true) },
            { RiscV32Registers.X1,  new CPURegister(1, 32, true) },
            { RiscV32Registers.X2,  new CPURegister(2, 32, true) },
            { RiscV32Registers.X3,  new CPURegister(3, 32, true) },
            { RiscV32Registers.X4,  new CPURegister(4, 32, true) },
            { RiscV32Registers.X5,  new CPURegister(5, 32, true) },
            { RiscV32Registers.X6,  new CPURegister(6, 32, true) },
            { RiscV32Registers.X7,  new CPURegister(7, 32, true) },
            { RiscV32Registers.X8,  new CPURegister(8, 32, true) },
            { RiscV32Registers.X9,  new CPURegister(9, 32, true) },
            { RiscV32Registers.X10,  new CPURegister(10, 32, true) },
            { RiscV32Registers.X11,  new CPURegister(11, 32, true) },
            { RiscV32Registers.X12,  new CPURegister(12, 32, true) },
            { RiscV32Registers.X13,  new CPURegister(13, 32, true) },
            { RiscV32Registers.X14,  new CPURegister(14, 32, true) },
            { RiscV32Registers.X15,  new CPURegister(15, 32, true) },
            { RiscV32Registers.X16,  new CPURegister(16, 32, true) },
            { RiscV32Registers.X17,  new CPURegister(17, 32, true) },
            { RiscV32Registers.X18,  new CPURegister(18, 32, true) },
            { RiscV32Registers.X19,  new CPURegister(19, 32, true) },
            { RiscV32Registers.X20,  new CPURegister(20, 32, true) },
            { RiscV32Registers.X21,  new CPURegister(21, 32, true) },
            { RiscV32Registers.X22,  new CPURegister(22, 32, true) },
            { RiscV32Registers.X23,  new CPURegister(23, 32, true) },
            { RiscV32Registers.X24,  new CPURegister(24, 32, true) },
            { RiscV32Registers.X25,  new CPURegister(25, 32, true) },
            { RiscV32Registers.X26,  new CPURegister(26, 32, true) },
            { RiscV32Registers.X27,  new CPURegister(27, 32, true) },
            { RiscV32Registers.X28,  new CPURegister(28, 32, true) },
            { RiscV32Registers.X29,  new CPURegister(29, 32, true) },
            { RiscV32Registers.X30,  new CPURegister(30, 32, true) },
            { RiscV32Registers.X31,  new CPURegister(31, 32, true) },
            { RiscV32Registers.PC,  new CPURegister(32, 32, true) },
        };
    }

    public enum RiscV32Registers
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
