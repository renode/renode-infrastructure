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
    public partial class RiscV64
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((RiscV64Registers)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue64(r.Index, checked((UInt64)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((RiscV64Registers)register, out var r))
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
        public RegisterValue ZERO
        {
            get
            {
                return GetRegisterValue64((int)RiscV64Registers.ZERO);
            }
            set
            {
                SetRegisterValue64((int)RiscV64Registers.ZERO, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue64((int)RiscV64Registers.PC);
            }
            set
            {
                SetRegisterValue64((int)RiscV64Registers.PC, value);
            }
        }
        public RegistersGroup X { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, RiscV64Registers>
            {
                { 0, RiscV64Registers.X0 },
                { 1, RiscV64Registers.X1 },
                { 2, RiscV64Registers.X2 },
                { 3, RiscV64Registers.X3 },
                { 4, RiscV64Registers.X4 },
                { 5, RiscV64Registers.X5 },
                { 6, RiscV64Registers.X6 },
                { 7, RiscV64Registers.X7 },
                { 8, RiscV64Registers.X8 },
                { 9, RiscV64Registers.X9 },
                { 10, RiscV64Registers.X10 },
                { 11, RiscV64Registers.X11 },
                { 12, RiscV64Registers.X12 },
                { 13, RiscV64Registers.X13 },
                { 14, RiscV64Registers.X14 },
                { 15, RiscV64Registers.X15 },
                { 16, RiscV64Registers.X16 },
                { 17, RiscV64Registers.X17 },
                { 18, RiscV64Registers.X18 },
                { 19, RiscV64Registers.X19 },
                { 20, RiscV64Registers.X20 },
                { 21, RiscV64Registers.X21 },
                { 22, RiscV64Registers.X22 },
                { 23, RiscV64Registers.X23 },
                { 24, RiscV64Registers.X24 },
                { 25, RiscV64Registers.X25 },
                { 26, RiscV64Registers.X26 },
                { 27, RiscV64Registers.X27 },
                { 28, RiscV64Registers.X28 },
                { 29, RiscV64Registers.X29 },
                { 30, RiscV64Registers.X30 },
                { 31, RiscV64Registers.X31 },
            };
            X = new RegistersGroup(
                indexValueMapX.Keys,
                i => GetRegisterUnsafe((int)indexValueMapX[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapX[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_64")]
        protected ActionInt32UInt64 SetRegisterValue64;
        [Import(Name = "tlib_get_register_value_64")]
        protected FuncUInt64Int32 GetRegisterValue64;

        #pragma warning restore 649

        private static readonly Dictionary<RiscV64Registers, CPURegister> mapping = new Dictionary<RiscV64Registers, CPURegister>
        {
            { RiscV64Registers.ZERO,  new CPURegister(0, 64, true) },
            { RiscV64Registers.X1,  new CPURegister(1, 64, true) },
            { RiscV64Registers.X2,  new CPURegister(2, 64, true) },
            { RiscV64Registers.X3,  new CPURegister(3, 64, true) },
            { RiscV64Registers.X4,  new CPURegister(4, 64, true) },
            { RiscV64Registers.X5,  new CPURegister(5, 64, true) },
            { RiscV64Registers.X6,  new CPURegister(6, 64, true) },
            { RiscV64Registers.X7,  new CPURegister(7, 64, true) },
            { RiscV64Registers.X8,  new CPURegister(8, 64, true) },
            { RiscV64Registers.X9,  new CPURegister(9, 64, true) },
            { RiscV64Registers.X10,  new CPURegister(10, 64, true) },
            { RiscV64Registers.X11,  new CPURegister(11, 64, true) },
            { RiscV64Registers.X12,  new CPURegister(12, 64, true) },
            { RiscV64Registers.X13,  new CPURegister(13, 64, true) },
            { RiscV64Registers.X14,  new CPURegister(14, 64, true) },
            { RiscV64Registers.X15,  new CPURegister(15, 64, true) },
            { RiscV64Registers.X16,  new CPURegister(16, 64, true) },
            { RiscV64Registers.X17,  new CPURegister(17, 64, true) },
            { RiscV64Registers.X18,  new CPURegister(18, 64, true) },
            { RiscV64Registers.X19,  new CPURegister(19, 64, true) },
            { RiscV64Registers.X20,  new CPURegister(20, 64, true) },
            { RiscV64Registers.X21,  new CPURegister(21, 64, true) },
            { RiscV64Registers.X22,  new CPURegister(22, 64, true) },
            { RiscV64Registers.X23,  new CPURegister(23, 64, true) },
            { RiscV64Registers.X24,  new CPURegister(24, 64, true) },
            { RiscV64Registers.X25,  new CPURegister(25, 64, true) },
            { RiscV64Registers.X26,  new CPURegister(26, 64, true) },
            { RiscV64Registers.X27,  new CPURegister(27, 64, true) },
            { RiscV64Registers.X28,  new CPURegister(28, 64, true) },
            { RiscV64Registers.X29,  new CPURegister(29, 64, true) },
            { RiscV64Registers.X30,  new CPURegister(30, 64, true) },
            { RiscV64Registers.X31,  new CPURegister(31, 64, true) },
            { RiscV64Registers.PC,  new CPURegister(32, 64, true) },
        };
    }

    public enum RiscV64Registers
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
