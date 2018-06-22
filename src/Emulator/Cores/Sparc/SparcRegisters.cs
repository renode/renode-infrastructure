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
    public partial class Sparc
    {
        public override void SetRegisterUnsafe(int register, ulong value)
        {
            if(!mapping.TryGetValue((SparcRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
        {
            if(!mapping.TryGetValue((SparcRegisters)register, out var r))
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
        public RegisterValue PSR
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.PSR);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.PSR, value);
            }
        }
        [Register]
        public RegisterValue TBR
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.TBR);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.TBR, value);
            }
        }
        [Register]
        public RegisterValue Y
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.Y);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.Y, value);
            }
        }
        [Register]
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.PC);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.PC, value);
                AfterPCSet(value);
            }
        }
        [Register]
        public RegisterValue NPC
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.NPC);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.NPC, value);
            }
        }
        [Register]
        public RegisterValue WIM
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.WIM);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.WIM, value);
            }
        }
        public RegistersGroup R { get; private set; }
        public RegistersGroup ASR { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapR = new Dictionary<int, SparcRegisters>
            {
                { 0, SparcRegisters.R0 },
                { 1, SparcRegisters.R1 },
                { 2, SparcRegisters.R2 },
                { 3, SparcRegisters.R3 },
                { 4, SparcRegisters.R4 },
                { 5, SparcRegisters.R5 },
                { 6, SparcRegisters.R6 },
                { 7, SparcRegisters.R7 },
                { 8, SparcRegisters.R8 },
                { 9, SparcRegisters.R9 },
                { 10, SparcRegisters.R10 },
                { 11, SparcRegisters.R11 },
                { 12, SparcRegisters.R12 },
                { 13, SparcRegisters.R13 },
                { 14, SparcRegisters.R14 },
                { 15, SparcRegisters.R15 },
                { 16, SparcRegisters.R16 },
                { 17, SparcRegisters.R17 },
                { 18, SparcRegisters.R18 },
                { 19, SparcRegisters.R19 },
                { 20, SparcRegisters.R20 },
                { 21, SparcRegisters.R21 },
                { 22, SparcRegisters.R22 },
                { 23, SparcRegisters.R23 },
                { 24, SparcRegisters.R24 },
                { 25, SparcRegisters.R25 },
                { 26, SparcRegisters.R26 },
                { 27, SparcRegisters.R27 },
                { 28, SparcRegisters.R28 },
                { 29, SparcRegisters.R29 },
                { 30, SparcRegisters.R30 },
                { 31, SparcRegisters.R31 },
            };
            R = new RegistersGroup(
                indexValueMapR.Keys,
                i => GetRegisterUnsafe((int)indexValueMapR[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapR[i], v));

            var indexValueMapASR = new Dictionary<int, SparcRegisters>
            {
                { 16, SparcRegisters.ASR16 },
                { 17, SparcRegisters.ASR17 },
                { 18, SparcRegisters.ASR18 },
                { 19, SparcRegisters.ASR19 },
                { 20, SparcRegisters.ASR20 },
                { 21, SparcRegisters.ASR21 },
                { 22, SparcRegisters.ASR22 },
                { 23, SparcRegisters.ASR23 },
                { 24, SparcRegisters.ASR24 },
                { 25, SparcRegisters.ASR25 },
                { 26, SparcRegisters.ASR26 },
                { 27, SparcRegisters.ASR27 },
                { 28, SparcRegisters.ASR28 },
                { 29, SparcRegisters.ASR29 },
                { 30, SparcRegisters.ASR30 },
                { 31, SparcRegisters.ASR31 },
            };
            ASR = new RegistersGroup(
                indexValueMapASR.Keys,
                i => GetRegisterUnsafe((int)indexValueMapASR[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapASR[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<SparcRegisters, CPURegister> mapping = new Dictionary<SparcRegisters, CPURegister>
        {
            { (SparcRegisters)0,  new CPURegister(0, 32, true) },
            { (SparcRegisters)1,  new CPURegister(1, 32, true) },
            { (SparcRegisters)2,  new CPURegister(2, 32, true) },
            { (SparcRegisters)3,  new CPURegister(3, 32, true) },
            { (SparcRegisters)4,  new CPURegister(4, 32, true) },
            { (SparcRegisters)5,  new CPURegister(5, 32, true) },
            { (SparcRegisters)6,  new CPURegister(6, 32, true) },
            { (SparcRegisters)7,  new CPURegister(7, 32, true) },
            { (SparcRegisters)8,  new CPURegister(8, 32, true) },
            { (SparcRegisters)9,  new CPURegister(9, 32, true) },
            { (SparcRegisters)10,  new CPURegister(10, 32, true) },
            { (SparcRegisters)11,  new CPURegister(11, 32, true) },
            { (SparcRegisters)12,  new CPURegister(12, 32, true) },
            { (SparcRegisters)13,  new CPURegister(13, 32, true) },
            { (SparcRegisters)14,  new CPURegister(14, 32, true) },
            { (SparcRegisters)15,  new CPURegister(15, 32, true) },
            { (SparcRegisters)16,  new CPURegister(16, 32, true) },
            { (SparcRegisters)17,  new CPURegister(17, 32, true) },
            { (SparcRegisters)18,  new CPURegister(18, 32, true) },
            { (SparcRegisters)19,  new CPURegister(19, 32, true) },
            { (SparcRegisters)20,  new CPURegister(20, 32, true) },
            { (SparcRegisters)21,  new CPURegister(21, 32, true) },
            { (SparcRegisters)22,  new CPURegister(22, 32, true) },
            { (SparcRegisters)23,  new CPURegister(23, 32, true) },
            { (SparcRegisters)24,  new CPURegister(24, 32, true) },
            { (SparcRegisters)25,  new CPURegister(25, 32, true) },
            { (SparcRegisters)26,  new CPURegister(26, 32, true) },
            { (SparcRegisters)27,  new CPURegister(27, 32, true) },
            { (SparcRegisters)28,  new CPURegister(28, 32, true) },
            { (SparcRegisters)29,  new CPURegister(29, 32, true) },
            { (SparcRegisters)30,  new CPURegister(30, 32, true) },
            { (SparcRegisters)31,  new CPURegister(31, 32, true) },
            { (SparcRegisters)32,  new CPURegister(32, 32, true) },
            { (SparcRegisters)33,  new CPURegister(33, 32, true) },
            { (SparcRegisters)34,  new CPURegister(34, 32, true) },
            { (SparcRegisters)35,  new CPURegister(35, 32, true) },
            { (SparcRegisters)36,  new CPURegister(36, 32, true) },
            { (SparcRegisters)37,  new CPURegister(37, 32, true) },
            { (SparcRegisters)38,  new CPURegister(38, 32, true) },
            { (SparcRegisters)39,  new CPURegister(39, 32, true) },
            { (SparcRegisters)40,  new CPURegister(40, 32, true) },
            { (SparcRegisters)41,  new CPURegister(41, 32, true) },
            { (SparcRegisters)42,  new CPURegister(42, 32, true) },
            { (SparcRegisters)43,  new CPURegister(43, 32, true) },
            { (SparcRegisters)44,  new CPURegister(44, 32, true) },
            { (SparcRegisters)45,  new CPURegister(45, 32, true) },
            { (SparcRegisters)46,  new CPURegister(46, 32, true) },
            { (SparcRegisters)47,  new CPURegister(47, 32, true) },
            { (SparcRegisters)48,  new CPURegister(48, 32, true) },
            { (SparcRegisters)49,  new CPURegister(49, 32, true) },
            { (SparcRegisters)50,  new CPURegister(50, 32, true) },
            { (SparcRegisters)51,  new CPURegister(51, 32, true) },
            { (SparcRegisters)52,  new CPURegister(52, 32, true) },
            { (SparcRegisters)53,  new CPURegister(53, 32, true) },
        };
    }

    public enum SparcRegisters
    {
        PSR = 32,
        TBR = 33,
        Y = 34,
        PC = 35,
        NPC = 36,
        WIM = 53,
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
        ASR16 = 37,
        ASR17 = 38,
        ASR18 = 39,
        ASR19 = 40,
        ASR20 = 41,
        ASR21 = 42,
        ASR22 = 43,
        ASR23 = 44,
        ASR24 = 45,
        ASR25 = 46,
        ASR26 = 47,
        ASR27 = 48,
        ASR28 = 49,
        ASR29 = 50,
        ASR30 = 51,
        ASR31 = 52,
    }
}
