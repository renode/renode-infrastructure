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
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((SparcRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegister(int register)
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
        public RegisterValue FSR
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.FSR);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.FSR, value);
            }
        }
        [Register]
        public RegisterValue CSR
        {
            get
            {
                return GetRegisterValue32((int)SparcRegisters.CSR);
            }
            set
            {
                SetRegisterValue32((int)SparcRegisters.CSR, value);
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
                i => GetRegister((int)indexValueMapR[i]),
                (i, v) => SetRegister((int)indexValueMapR[i], v));

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
                i => GetRegister((int)indexValueMapASR[i]),
                (i, v) => SetRegister((int)indexValueMapASR[i], v));

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
            { SparcRegisters.R0,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R0" }) },
            { SparcRegisters.R1,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R1" }) },
            { SparcRegisters.R2,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R2" }) },
            { SparcRegisters.R3,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R3" }) },
            { SparcRegisters.R4,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R4" }) },
            { SparcRegisters.R5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R5" }) },
            { SparcRegisters.R6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R6" }) },
            { SparcRegisters.R7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R7" }) },
            { SparcRegisters.R8,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R8" }) },
            { SparcRegisters.R9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R9" }) },
            { SparcRegisters.R10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R10" }) },
            { SparcRegisters.R11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R11" }) },
            { SparcRegisters.R12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R12" }) },
            { SparcRegisters.R13,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R13" }) },
            { SparcRegisters.R14,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R14" }) },
            { SparcRegisters.R15,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R15" }) },
            { SparcRegisters.R16,  new CPURegister(16, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R16" }) },
            { SparcRegisters.R17,  new CPURegister(17, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R17" }) },
            { SparcRegisters.R18,  new CPURegister(18, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R18" }) },
            { SparcRegisters.R19,  new CPURegister(19, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R19" }) },
            { SparcRegisters.R20,  new CPURegister(20, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R20" }) },
            { SparcRegisters.R21,  new CPURegister(21, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R21" }) },
            { SparcRegisters.R22,  new CPURegister(22, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R22" }) },
            { SparcRegisters.R23,  new CPURegister(23, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R23" }) },
            { SparcRegisters.R24,  new CPURegister(24, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R24" }) },
            { SparcRegisters.R25,  new CPURegister(25, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R25" }) },
            { SparcRegisters.R26,  new CPURegister(26, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R26" }) },
            { SparcRegisters.R27,  new CPURegister(27, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R27" }) },
            { SparcRegisters.R28,  new CPURegister(28, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R28" }) },
            { SparcRegisters.R29,  new CPURegister(29, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R29" }) },
            { SparcRegisters.R30,  new CPURegister(30, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R30" }) },
            { SparcRegisters.R31,  new CPURegister(31, 32, isGeneral: true, isReadonly: false, aliases: new [] { "R31" }) },
            { SparcRegisters.ASR16,  new CPURegister(37, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR16" }) },
            { SparcRegisters.ASR17,  new CPURegister(38, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR17" }) },
            { SparcRegisters.ASR18,  new CPURegister(39, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR18" }) },
            { SparcRegisters.ASR19,  new CPURegister(40, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR19" }) },
            { SparcRegisters.ASR20,  new CPURegister(41, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR20" }) },
            { SparcRegisters.ASR21,  new CPURegister(42, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR21" }) },
            { SparcRegisters.ASR22,  new CPURegister(43, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR22" }) },
            { SparcRegisters.ASR23,  new CPURegister(44, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR23" }) },
            { SparcRegisters.ASR24,  new CPURegister(45, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR24" }) },
            { SparcRegisters.ASR25,  new CPURegister(46, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR25" }) },
            { SparcRegisters.ASR26,  new CPURegister(47, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR26" }) },
            { SparcRegisters.ASR27,  new CPURegister(48, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR27" }) },
            { SparcRegisters.ASR28,  new CPURegister(49, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR28" }) },
            { SparcRegisters.ASR29,  new CPURegister(50, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR29" }) },
            { SparcRegisters.ASR30,  new CPURegister(51, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR30" }) },
            { SparcRegisters.ASR31,  new CPURegister(52, 32, isGeneral: true, isReadonly: false, aliases: new [] { "ASR31" }) },
            { SparcRegisters.Y,  new CPURegister(64, 32, isGeneral: true, isReadonly: false, aliases: new [] { "Y" }) },
            { SparcRegisters.PSR,  new CPURegister(65, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PSR" }) },
            { SparcRegisters.WIM,  new CPURegister(66, 32, isGeneral: true, isReadonly: false, aliases: new [] { "WIM" }) },
            { SparcRegisters.TBR,  new CPURegister(67, 32, isGeneral: true, isReadonly: false, aliases: new [] { "TBR" }) },
            { SparcRegisters.PC,  new CPURegister(68, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PC" }) },
            { SparcRegisters.NPC,  new CPURegister(69, 32, isGeneral: true, isReadonly: false, aliases: new [] { "NPC" }) },
            { SparcRegisters.FSR,  new CPURegister(70, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FSR" }) },
            { SparcRegisters.CSR,  new CPURegister(71, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CSR" }) },
        };
    }

    public enum SparcRegisters
    {
        Y = 64,
        PSR = 65,
        WIM = 66,
        TBR = 67,
        PC = 68,
        NPC = 69,
        FSR = 70,
        CSR = 71,
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
