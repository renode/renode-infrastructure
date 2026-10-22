/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/
#pragma warning disable IDE0005
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU.Registers;
using Antmicro.Renode.Utilities.Binding;
#pragma warning restore IDE0005

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

            switch(r.Width)
            {
            case 32:
                SetRegisterValue32(r.Index, checked((uint)value));
                break;
            case 64:
                SetRegisterValue64(r.Index, checked((ulong)value));
                break;
            default:
                throw new ArgumentException($"Unsupported register width: {r.Width}");
            }
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((ArmRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            switch(r.Width)
            {
            case 32:
                return GetRegisterValue32(r.Index);
            case 64:
                return GetRegisterValue64(r.Index);
            default:
                throw new ArgumentException($"Unsupported register width: {r.Width}");
            }
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

        [Register]
        public RegisterValue FPSCR
        {
            get
            {
                return GetRegisterValue32((int)ArmRegisters.FPSCR);
            }

            set
            {
                SetRegisterValue32((int)ArmRegisters.FPSCR, value);
            }
        }

        public RegistersGroup R { get; private set; }

        public RegistersGroup D { get; private set; }

        public RegistersGroup S { get; private set; }

#pragma warning disable SA1508
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

            var indexValueMapD = new Dictionary<int, ArmRegisters>
            {
                { 0, ArmRegisters.D0 },
                { 1, ArmRegisters.D1 },
                { 2, ArmRegisters.D2 },
                { 3, ArmRegisters.D3 },
                { 4, ArmRegisters.D4 },
                { 5, ArmRegisters.D5 },
                { 6, ArmRegisters.D6 },
                { 7, ArmRegisters.D7 },
                { 8, ArmRegisters.D8 },
                { 9, ArmRegisters.D9 },
                { 10, ArmRegisters.D10 },
                { 11, ArmRegisters.D11 },
                { 12, ArmRegisters.D12 },
                { 13, ArmRegisters.D13 },
                { 14, ArmRegisters.D14 },
                { 15, ArmRegisters.D15 },
                { 16, ArmRegisters.D16 },
                { 17, ArmRegisters.D17 },
                { 18, ArmRegisters.D18 },
                { 19, ArmRegisters.D19 },
                { 20, ArmRegisters.D20 },
                { 21, ArmRegisters.D21 },
                { 22, ArmRegisters.D22 },
                { 23, ArmRegisters.D23 },
                { 24, ArmRegisters.D24 },
                { 25, ArmRegisters.D25 },
                { 26, ArmRegisters.D26 },
                { 27, ArmRegisters.D27 },
                { 28, ArmRegisters.D28 },
                { 29, ArmRegisters.D29 },
                { 30, ArmRegisters.D30 },
                { 31, ArmRegisters.D31 },
            };
            D = new RegistersGroup(
                indexValueMapD.Keys,
                i => GetRegister((int)indexValueMapD[i]),
                (i, v) => SetRegister((int)indexValueMapD[i], v));

            var indexValueMapS = new Dictionary<int, ArmRegisters>
            {
                { 0, ArmRegisters.S0 },
                { 1, ArmRegisters.S1 },
                { 2, ArmRegisters.S2 },
                { 3, ArmRegisters.S3 },
                { 4, ArmRegisters.S4 },
                { 5, ArmRegisters.S5 },
                { 6, ArmRegisters.S6 },
                { 7, ArmRegisters.S7 },
                { 8, ArmRegisters.S8 },
                { 9, ArmRegisters.S9 },
                { 10, ArmRegisters.S10 },
                { 11, ArmRegisters.S11 },
                { 12, ArmRegisters.S12 },
                { 13, ArmRegisters.S13 },
                { 14, ArmRegisters.S14 },
                { 15, ArmRegisters.S15 },
                { 16, ArmRegisters.S16 },
                { 17, ArmRegisters.S17 },
                { 18, ArmRegisters.S18 },
                { 19, ArmRegisters.S19 },
                { 20, ArmRegisters.S20 },
                { 21, ArmRegisters.S21 },
                { 22, ArmRegisters.S22 },
                { 23, ArmRegisters.S23 },
                { 24, ArmRegisters.S24 },
                { 25, ArmRegisters.S25 },
                { 26, ArmRegisters.S26 },
                { 27, ArmRegisters.S27 },
                { 28, ArmRegisters.S28 },
                { 29, ArmRegisters.S29 },
                { 30, ArmRegisters.S30 },
                { 31, ArmRegisters.S31 },
            };
            S = new RegistersGroup(
                indexValueMapS.Keys,
                i => GetRegister((int)indexValueMapS[i]),
                (i, v) => SetRegister((int)indexValueMapS[i], v));

        }
#pragma warning restore SA1508

#pragma warning disable 649
        // 649:  Field '...' is never assigned to, and will always have its default value null
        [Import(Name = "tlib_set_register_value_32")]
        protected Action<int, uint> SetRegisterValue32;

        [Import(Name = "tlib_get_register_value_32")]
        protected Func<int, uint> GetRegisterValue32;
#pragma warning restore 649

#pragma warning disable 649
        // 649:  Field '...' is never assigned to, and will always have its default value null
        [Import(Name = "tlib_set_register_value_64")]
        protected Action<int, ulong> SetRegisterValue64;

        [Import(Name = "tlib_get_register_value_64")]
        protected Func<int, ulong> GetRegisterValue64;
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
            { ArmRegisters.D0,  new CPURegister(42, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D0" }) },
            { ArmRegisters.D1,  new CPURegister(43, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D1" }) },
            { ArmRegisters.D2,  new CPURegister(44, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D2" }) },
            { ArmRegisters.D3,  new CPURegister(45, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D3" }) },
            { ArmRegisters.D4,  new CPURegister(46, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D4" }) },
            { ArmRegisters.D5,  new CPURegister(47, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D5" }) },
            { ArmRegisters.D6,  new CPURegister(48, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D6" }) },
            { ArmRegisters.D7,  new CPURegister(49, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D7" }) },
            { ArmRegisters.D8,  new CPURegister(50, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D8" }) },
            { ArmRegisters.D9,  new CPURegister(51, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D9" }) },
            { ArmRegisters.D10,  new CPURegister(52, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D10" }) },
            { ArmRegisters.D11,  new CPURegister(53, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D11" }) },
            { ArmRegisters.D12,  new CPURegister(54, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D12" }) },
            { ArmRegisters.D13,  new CPURegister(55, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D13" }) },
            { ArmRegisters.D14,  new CPURegister(56, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D14" }) },
            { ArmRegisters.D15,  new CPURegister(57, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D15" }) },
            { ArmRegisters.D16,  new CPURegister(58, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D16" }) },
            { ArmRegisters.D17,  new CPURegister(59, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D17" }) },
            { ArmRegisters.D18,  new CPURegister(60, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D18" }) },
            { ArmRegisters.D19,  new CPURegister(61, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D19" }) },
            { ArmRegisters.D20,  new CPURegister(62, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D20" }) },
            { ArmRegisters.D21,  new CPURegister(63, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D21" }) },
            { ArmRegisters.D22,  new CPURegister(64, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D22" }) },
            { ArmRegisters.D23,  new CPURegister(65, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D23" }) },
            { ArmRegisters.D24,  new CPURegister(66, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D24" }) },
            { ArmRegisters.D25,  new CPURegister(67, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D25" }) },
            { ArmRegisters.D26,  new CPURegister(68, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D26" }) },
            { ArmRegisters.D27,  new CPURegister(69, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D27" }) },
            { ArmRegisters.D28,  new CPURegister(70, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D28" }) },
            { ArmRegisters.D29,  new CPURegister(71, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D29" }) },
            { ArmRegisters.D30,  new CPURegister(72, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D30" }) },
            { ArmRegisters.D31,  new CPURegister(73, 64, isGeneral: false, isReadonly: false, aliases: new [] { "D31" }) },
            { ArmRegisters.FPSCR,  new CPURegister(74, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FPSCR" }) },
            { ArmRegisters.S0,  new CPURegister(75, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S0" }) },
            { ArmRegisters.S1,  new CPURegister(76, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S1" }) },
            { ArmRegisters.S2,  new CPURegister(77, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S2" }) },
            { ArmRegisters.S3,  new CPURegister(78, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S3" }) },
            { ArmRegisters.S4,  new CPURegister(79, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S4" }) },
            { ArmRegisters.S5,  new CPURegister(80, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S5" }) },
            { ArmRegisters.S6,  new CPURegister(81, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S6" }) },
            { ArmRegisters.S7,  new CPURegister(82, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S7" }) },
            { ArmRegisters.S8,  new CPURegister(83, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S8" }) },
            { ArmRegisters.S9,  new CPURegister(84, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S9" }) },
            { ArmRegisters.S10,  new CPURegister(85, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S10" }) },
            { ArmRegisters.S11,  new CPURegister(86, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S11" }) },
            { ArmRegisters.S12,  new CPURegister(87, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S12" }) },
            { ArmRegisters.S13,  new CPURegister(88, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S13" }) },
            { ArmRegisters.S14,  new CPURegister(89, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S14" }) },
            { ArmRegisters.S15,  new CPURegister(90, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S15" }) },
            { ArmRegisters.S16,  new CPURegister(91, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S16" }) },
            { ArmRegisters.S17,  new CPURegister(92, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S17" }) },
            { ArmRegisters.S18,  new CPURegister(93, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S18" }) },
            { ArmRegisters.S19,  new CPURegister(94, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S19" }) },
            { ArmRegisters.S20,  new CPURegister(95, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S20" }) },
            { ArmRegisters.S21,  new CPURegister(96, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S21" }) },
            { ArmRegisters.S22,  new CPURegister(97, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S22" }) },
            { ArmRegisters.S23,  new CPURegister(98, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S23" }) },
            { ArmRegisters.S24,  new CPURegister(99, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S24" }) },
            { ArmRegisters.S25,  new CPURegister(100, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S25" }) },
            { ArmRegisters.S26,  new CPURegister(101, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S26" }) },
            { ArmRegisters.S27,  new CPURegister(102, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S27" }) },
            { ArmRegisters.S28,  new CPURegister(103, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S28" }) },
            { ArmRegisters.S29,  new CPURegister(104, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S29" }) },
            { ArmRegisters.S30,  new CPURegister(105, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S30" }) },
            { ArmRegisters.S31,  new CPURegister(106, 32, isGeneral: false, isReadonly: false, aliases: new [] { "S31" }) },
        };
    }

    public enum ArmRegisters
    {
        SP = 13,
        LR = 14,
        PC = 15,
        CPSR = 25,
        FPSCR = 74,
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
        D0 = 42,
        D1 = 43,
        D2 = 44,
        D3 = 45,
        D4 = 46,
        D5 = 47,
        D6 = 48,
        D7 = 49,
        D8 = 50,
        D9 = 51,
        D10 = 52,
        D11 = 53,
        D12 = 54,
        D13 = 55,
        D14 = 56,
        D15 = 57,
        D16 = 58,
        D17 = 59,
        D18 = 60,
        D19 = 61,
        D20 = 62,
        D21 = 63,
        D22 = 64,
        D23 = 65,
        D24 = 66,
        D25 = 67,
        D26 = 68,
        D27 = 69,
        D28 = 70,
        D29 = 71,
        D30 = 72,
        D31 = 73,
        S0 = 75,
        S1 = 76,
        S2 = 77,
        S3 = 78,
        S4 = 79,
        S5 = 80,
        S6 = 81,
        S7 = 82,
        S8 = 83,
        S9 = 84,
        S10 = 85,
        S11 = 86,
        S12 = 87,
        S13 = 88,
        S14 = 89,
        S15 = 90,
        S16 = 91,
        S17 = 92,
        S18 = 93,
        S19 = 94,
        S20 = 95,
        S21 = 96,
        S22 = 97,
        S23 = 98,
        S24 = 99,
        S25 = 100,
        S26 = 101,
        S27 = 102,
        S28 = 103,
        S29 = 104,
        S30 = 105,
        S31 = 106,
    }
}