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
    public partial class RiscV
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((RiscVRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((RiscVRegisters)register, out var r))
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
                return GetRegisterValue32((int)RiscVRegisters.ZERO);
            }
            set
            {
                SetRegisterValue32((int)RiscVRegisters.ZERO, value);
            }
        }
        [Register]
        public override RegisterValue PC
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
        public RegistersGroup X { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapX = new Dictionary<int, RiscVRegisters>
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

        private static readonly Dictionary<RiscVRegisters, CPURegister> mapping = new Dictionary<RiscVRegisters, CPURegister>
        {
            { (RiscVRegisters)0,  new CPURegister(0, 32, true) },
            { (RiscVRegisters)1,  new CPURegister(1, 32, true) },
            { (RiscVRegisters)2,  new CPURegister(2, 32, true) },
            { (RiscVRegisters)3,  new CPURegister(3, 32, true) },
            { (RiscVRegisters)4,  new CPURegister(4, 32, true) },
            { (RiscVRegisters)5,  new CPURegister(5, 32, true) },
            { (RiscVRegisters)6,  new CPURegister(6, 32, true) },
            { (RiscVRegisters)7,  new CPURegister(7, 32, true) },
            { (RiscVRegisters)8,  new CPURegister(8, 32, true) },
            { (RiscVRegisters)9,  new CPURegister(9, 32, true) },
            { (RiscVRegisters)10,  new CPURegister(10, 32, true) },
            { (RiscVRegisters)11,  new CPURegister(11, 32, true) },
            { (RiscVRegisters)12,  new CPURegister(12, 32, true) },
            { (RiscVRegisters)13,  new CPURegister(13, 32, true) },
            { (RiscVRegisters)14,  new CPURegister(14, 32, true) },
            { (RiscVRegisters)15,  new CPURegister(15, 32, true) },
            { (RiscVRegisters)16,  new CPURegister(16, 32, true) },
            { (RiscVRegisters)17,  new CPURegister(17, 32, true) },
            { (RiscVRegisters)18,  new CPURegister(18, 32, true) },
            { (RiscVRegisters)19,  new CPURegister(19, 32, true) },
            { (RiscVRegisters)20,  new CPURegister(20, 32, true) },
            { (RiscVRegisters)21,  new CPURegister(21, 32, true) },
            { (RiscVRegisters)22,  new CPURegister(22, 32, true) },
            { (RiscVRegisters)23,  new CPURegister(23, 32, true) },
            { (RiscVRegisters)24,  new CPURegister(24, 32, true) },
            { (RiscVRegisters)25,  new CPURegister(25, 32, true) },
            { (RiscVRegisters)26,  new CPURegister(26, 32, true) },
            { (RiscVRegisters)27,  new CPURegister(27, 32, true) },
            { (RiscVRegisters)28,  new CPURegister(28, 32, true) },
            { (RiscVRegisters)29,  new CPURegister(29, 32, true) },
            { (RiscVRegisters)30,  new CPURegister(30, 32, true) },
            { (RiscVRegisters)31,  new CPURegister(31, 32, true) },
            { (RiscVRegisters)32,  new CPURegister(32, 32, true) },
        };
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
