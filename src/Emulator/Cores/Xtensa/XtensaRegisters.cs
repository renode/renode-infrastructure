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
    public partial class Xtensa
    {
        public override void SetRegister(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((XtensaRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }
            if(r.IsReadonly)
            {
                throw new RecoverableException($"The '{(XtensaRegisters)register}' register is read-only.");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegister(int register)
        {
            if(!mapping.TryGetValue((XtensaRegisters)register, out var r))
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
        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PC);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PC, value);
            }
        }
        [Register]
        public RegisterValue SAR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.SAR);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.SAR, value);
            }
        }
        [Register]
        public RegisterValue WINDOWBASE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.WINDOWBASE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.WINDOWBASE, value);
            }
        }
        [Register]
        public RegisterValue WINDOWSTART
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.WINDOWSTART);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.WINDOWSTART, value);
            }
        }
        [Register]
        public RegisterValue PS
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PS);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PS, value);
            }
        }
        [Register]
        public RegisterValue EXPSTATE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.EXPSTATE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.EXPSTATE, value);
            }
        }
        [Register]
        public RegisterValue MMID
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.MMID);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.MMID, value);
            }
        }
        [Register]
        public RegisterValue IBREAKENABLE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.IBREAKENABLE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.IBREAKENABLE, value);
            }
        }
        [Register]
        public RegisterValue ATOMCTL
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.ATOMCTL);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.ATOMCTL, value);
            }
        }
        [Register]
        public RegisterValue DDR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.DDR);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.DDR, value);
            }
        }
        [Register]
        public RegisterValue DEPC
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.DEPC);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.DEPC, value);
            }
        }
        [Register]
        public RegisterValue INTERRUPT
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.INTERRUPT);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.INTERRUPT, value);
            }
        }
        [Register]
        public RegisterValue INTSET
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.INTSET);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.INTSET, value);
            }
        }
        [Register]
        public RegisterValue INTCLEAR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.INTCLEAR);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.INTCLEAR, value);
            }
        }
        [Register]
        public RegisterValue INTENABLE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.INTENABLE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.INTENABLE, value);
            }
        }
        [Register]
        public RegisterValue VECBASE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.VECBASE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.VECBASE, value);
            }
        }
        [Register]
        public RegisterValue EXCCAUSE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.EXCCAUSE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.EXCCAUSE, value);
            }
        }
        [Register]
        public RegisterValue DEBUGCAUSE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.DEBUGCAUSE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.DEBUGCAUSE, value);
            }
        }
        [Register]
        public RegisterValue CCOUNT
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.CCOUNT);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.CCOUNT, value);
            }
        }
        [Register]
        public RegisterValue PRID
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PRID);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PRID, value);
            }
        }
        [Register]
        public RegisterValue ICOUNT
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.ICOUNT);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.ICOUNT, value);
            }
        }
        [Register]
        public RegisterValue ICOUNTLEVEL
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.ICOUNTLEVEL);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.ICOUNTLEVEL, value);
            }
        }
        [Register]
        public RegisterValue EXCVADDR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.EXCVADDR);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.EXCVADDR, value);
            }
        }
        [Register]
        public RegisterValue PSINTLEVEL
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSINTLEVEL);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSINTLEVEL, value);
            }
        }
        [Register]
        public RegisterValue PSUM
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSUM);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSUM, value);
            }
        }
        [Register]
        public RegisterValue PSWOE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSWOE);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSWOE, value);
            }
        }
        [Register]
        public RegisterValue PSEXCM
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSEXCM);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSEXCM, value);
            }
        }
        [Register]
        public RegisterValue PSCALLINC
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSCALLINC);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSCALLINC, value);
            }
        }
        [Register]
        public RegisterValue PSOWB
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.PSOWB);
            }
            set
            {
                SetRegisterValue32((int)XtensaRegisters.PSOWB, value);
            }
        }
        public RegistersGroup AR { get; private set; }
        public RegistersGroup CONFIGID { get; private set; }
        public RegistersGroup SCOMPARE { get; private set; }
        public RegistersGroup IBREAKA { get; private set; }
        public RegistersGroup DBREAKA { get; private set; }
        public RegistersGroup DBREAKC { get; private set; }
        public RegistersGroup EPC { get; private set; }
        public RegistersGroup EPS { get; private set; }
        public RegistersGroup EXCSAVE { get; private set; }
        public RegistersGroup CCOMPARE { get; private set; }
        public RegistersGroup MISC { get; private set; }
        public RegistersGroup A { get; private set; }

        protected override void InitializeRegisters()
        {
            var indexValueMapAR = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.AR0 },
                { 1, XtensaRegisters.AR1 },
                { 2, XtensaRegisters.AR2 },
                { 3, XtensaRegisters.AR3 },
                { 4, XtensaRegisters.AR4 },
                { 5, XtensaRegisters.AR5 },
                { 6, XtensaRegisters.AR6 },
                { 7, XtensaRegisters.AR7 },
                { 8, XtensaRegisters.AR8 },
                { 9, XtensaRegisters.AR9 },
                { 10, XtensaRegisters.AR10 },
                { 11, XtensaRegisters.AR11 },
                { 12, XtensaRegisters.AR12 },
                { 13, XtensaRegisters.AR13 },
                { 14, XtensaRegisters.AR14 },
                { 15, XtensaRegisters.AR15 },
                { 16, XtensaRegisters.AR16 },
                { 17, XtensaRegisters.AR17 },
                { 18, XtensaRegisters.AR18 },
                { 19, XtensaRegisters.AR19 },
                { 20, XtensaRegisters.AR20 },
                { 21, XtensaRegisters.AR21 },
                { 22, XtensaRegisters.AR22 },
                { 23, XtensaRegisters.AR23 },
                { 24, XtensaRegisters.AR24 },
                { 25, XtensaRegisters.AR25 },
                { 26, XtensaRegisters.AR26 },
                { 27, XtensaRegisters.AR27 },
                { 28, XtensaRegisters.AR28 },
                { 29, XtensaRegisters.AR29 },
                { 30, XtensaRegisters.AR30 },
                { 31, XtensaRegisters.AR31 },
            };
            AR = new RegistersGroup(
                indexValueMapAR.Keys,
                i => GetRegister((int)indexValueMapAR[i]),
                (i, v) => SetRegister((int)indexValueMapAR[i], v));

            var indexValueMapCONFIGID = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.CONFIGID0 },
                { 1, XtensaRegisters.CONFIGID1 },
            };
            CONFIGID = new RegistersGroup(
                indexValueMapCONFIGID.Keys,
                i => GetRegister((int)indexValueMapCONFIGID[i]),
                (i, v) => SetRegister((int)indexValueMapCONFIGID[i], v));

            var indexValueMapSCOMPARE = new Dictionary<int, XtensaRegisters>
            {
                { 1, XtensaRegisters.SCOMPARE1 },
            };
            SCOMPARE = new RegistersGroup(
                indexValueMapSCOMPARE.Keys,
                i => GetRegister((int)indexValueMapSCOMPARE[i]),
                (i, v) => SetRegister((int)indexValueMapSCOMPARE[i], v));

            var indexValueMapIBREAKA = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.IBREAKA0 },
                { 1, XtensaRegisters.IBREAKA1 },
            };
            IBREAKA = new RegistersGroup(
                indexValueMapIBREAKA.Keys,
                i => GetRegister((int)indexValueMapIBREAKA[i]),
                (i, v) => SetRegister((int)indexValueMapIBREAKA[i], v));

            var indexValueMapDBREAKA = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.DBREAKA0 },
                { 1, XtensaRegisters.DBREAKA1 },
            };
            DBREAKA = new RegistersGroup(
                indexValueMapDBREAKA.Keys,
                i => GetRegister((int)indexValueMapDBREAKA[i]),
                (i, v) => SetRegister((int)indexValueMapDBREAKA[i], v));

            var indexValueMapDBREAKC = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.DBREAKC0 },
                { 1, XtensaRegisters.DBREAKC1 },
            };
            DBREAKC = new RegistersGroup(
                indexValueMapDBREAKC.Keys,
                i => GetRegister((int)indexValueMapDBREAKC[i]),
                (i, v) => SetRegister((int)indexValueMapDBREAKC[i], v));

            var indexValueMapEPC = new Dictionary<int, XtensaRegisters>
            {
                { 1, XtensaRegisters.EPC1 },
                { 2, XtensaRegisters.EPC2 },
                { 3, XtensaRegisters.EPC3 },
                { 4, XtensaRegisters.EPC4 },
                { 5, XtensaRegisters.EPC5 },
                { 6, XtensaRegisters.EPC6 },
                { 7, XtensaRegisters.EPC7 },
            };
            EPC = new RegistersGroup(
                indexValueMapEPC.Keys,
                i => GetRegister((int)indexValueMapEPC[i]),
                (i, v) => SetRegister((int)indexValueMapEPC[i], v));

            var indexValueMapEPS = new Dictionary<int, XtensaRegisters>
            {
                { 2, XtensaRegisters.EPS2 },
                { 3, XtensaRegisters.EPS3 },
                { 4, XtensaRegisters.EPS4 },
                { 5, XtensaRegisters.EPS5 },
                { 6, XtensaRegisters.EPS6 },
                { 7, XtensaRegisters.EPS7 },
            };
            EPS = new RegistersGroup(
                indexValueMapEPS.Keys,
                i => GetRegister((int)indexValueMapEPS[i]),
                (i, v) => SetRegister((int)indexValueMapEPS[i], v));

            var indexValueMapEXCSAVE = new Dictionary<int, XtensaRegisters>
            {
                { 1, XtensaRegisters.EXCSAVE1 },
                { 2, XtensaRegisters.EXCSAVE2 },
                { 3, XtensaRegisters.EXCSAVE3 },
                { 4, XtensaRegisters.EXCSAVE4 },
                { 5, XtensaRegisters.EXCSAVE5 },
                { 6, XtensaRegisters.EXCSAVE6 },
                { 7, XtensaRegisters.EXCSAVE7 },
            };
            EXCSAVE = new RegistersGroup(
                indexValueMapEXCSAVE.Keys,
                i => GetRegister((int)indexValueMapEXCSAVE[i]),
                (i, v) => SetRegister((int)indexValueMapEXCSAVE[i], v));

            var indexValueMapCCOMPARE = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.CCOMPARE0 },
                { 1, XtensaRegisters.CCOMPARE1 },
                { 2, XtensaRegisters.CCOMPARE2 },
            };
            CCOMPARE = new RegistersGroup(
                indexValueMapCCOMPARE.Keys,
                i => GetRegister((int)indexValueMapCCOMPARE[i]),
                (i, v) => SetRegister((int)indexValueMapCCOMPARE[i], v));

            var indexValueMapMISC = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.MISC0 },
                { 1, XtensaRegisters.MISC1 },
            };
            MISC = new RegistersGroup(
                indexValueMapMISC.Keys,
                i => GetRegister((int)indexValueMapMISC[i]),
                (i, v) => SetRegister((int)indexValueMapMISC[i], v));

            var indexValueMapA = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.A0 },
                { 1, XtensaRegisters.A1 },
                { 2, XtensaRegisters.A2 },
                { 3, XtensaRegisters.A3 },
                { 4, XtensaRegisters.A4 },
                { 5, XtensaRegisters.A5 },
                { 6, XtensaRegisters.A6 },
                { 7, XtensaRegisters.A7 },
                { 8, XtensaRegisters.A8 },
                { 9, XtensaRegisters.A9 },
                { 10, XtensaRegisters.A10 },
                { 11, XtensaRegisters.A11 },
                { 12, XtensaRegisters.A12 },
                { 13, XtensaRegisters.A13 },
                { 14, XtensaRegisters.A14 },
                { 15, XtensaRegisters.A15 },
            };
            A = new RegistersGroup(
                indexValueMapA.Keys,
                i => GetRegister((int)indexValueMapA[i]),
                (i, v) => SetRegister((int)indexValueMapA[i], v));

        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import(Name = "tlib_set_register_value_32")]
        protected ActionInt32UInt32 SetRegisterValue32;
        [Import(Name = "tlib_get_register_value_32")]
        protected FuncUInt32Int32 GetRegisterValue32;

        #pragma warning restore 649

        private static readonly Dictionary<XtensaRegisters, CPURegister> mapping = new Dictionary<XtensaRegisters, CPURegister>
        {
            { XtensaRegisters.PC,  new CPURegister(0, 32, isGeneral: true, isReadonly: false, aliases: new [] { "PC" }) },
            { XtensaRegisters.AR0,  new CPURegister(1, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR0" }) },
            { XtensaRegisters.AR1,  new CPURegister(2, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR1" }) },
            { XtensaRegisters.AR2,  new CPURegister(3, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR2" }) },
            { XtensaRegisters.AR3,  new CPURegister(4, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR3" }) },
            { XtensaRegisters.AR4,  new CPURegister(5, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR4" }) },
            { XtensaRegisters.AR5,  new CPURegister(6, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR5" }) },
            { XtensaRegisters.AR6,  new CPURegister(7, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR6" }) },
            { XtensaRegisters.AR7,  new CPURegister(8, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR7" }) },
            { XtensaRegisters.AR8,  new CPURegister(9, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR8" }) },
            { XtensaRegisters.AR9,  new CPURegister(10, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR9" }) },
            { XtensaRegisters.AR10,  new CPURegister(11, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR10" }) },
            { XtensaRegisters.AR11,  new CPURegister(12, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR11" }) },
            { XtensaRegisters.AR12,  new CPURegister(13, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR12" }) },
            { XtensaRegisters.AR13,  new CPURegister(14, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR13" }) },
            { XtensaRegisters.AR14,  new CPURegister(15, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR14" }) },
            { XtensaRegisters.AR15,  new CPURegister(16, 32, isGeneral: true, isReadonly: false, aliases: new [] { "AR15" }) },
            { XtensaRegisters.AR16,  new CPURegister(17, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR16" }) },
            { XtensaRegisters.AR17,  new CPURegister(18, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR17" }) },
            { XtensaRegisters.AR18,  new CPURegister(19, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR18" }) },
            { XtensaRegisters.AR19,  new CPURegister(20, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR19" }) },
            { XtensaRegisters.AR20,  new CPURegister(21, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR20" }) },
            { XtensaRegisters.AR21,  new CPURegister(22, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR21" }) },
            { XtensaRegisters.AR22,  new CPURegister(23, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR22" }) },
            { XtensaRegisters.AR23,  new CPURegister(24, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR23" }) },
            { XtensaRegisters.AR24,  new CPURegister(25, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR24" }) },
            { XtensaRegisters.AR25,  new CPURegister(26, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR25" }) },
            { XtensaRegisters.AR26,  new CPURegister(27, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR26" }) },
            { XtensaRegisters.AR27,  new CPURegister(28, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR27" }) },
            { XtensaRegisters.AR28,  new CPURegister(29, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR28" }) },
            { XtensaRegisters.AR29,  new CPURegister(30, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR29" }) },
            { XtensaRegisters.AR30,  new CPURegister(31, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR30" }) },
            { XtensaRegisters.AR31,  new CPURegister(32, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR31" }) },
            { XtensaRegisters.SAR,  new CPURegister(33, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SAR" }) },
            { XtensaRegisters.WINDOWBASE,  new CPURegister(34, 32, isGeneral: false, isReadonly: false, aliases: new [] { "WINDOWBASE" }) },
            { XtensaRegisters.WINDOWSTART,  new CPURegister(35, 32, isGeneral: false, isReadonly: false, aliases: new [] { "WINDOWSTART" }) },
            { XtensaRegisters.CONFIGID0,  new CPURegister(36, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CONFIGID0" }) },
            { XtensaRegisters.CONFIGID1,  new CPURegister(37, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CONFIGID1" }) },
            { XtensaRegisters.PS,  new CPURegister(38, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PS" }) },
            { XtensaRegisters.SCOMPARE1,  new CPURegister(39, 32, isGeneral: false, isReadonly: false, aliases: new [] { "SCOMPARE1" }) },
            { XtensaRegisters.EXPSTATE,  new CPURegister(40, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXPSTATE" }) },
            { XtensaRegisters.MMID,  new CPURegister(41, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MMID" }) },
            { XtensaRegisters.IBREAKENABLE,  new CPURegister(42, 32, isGeneral: false, isReadonly: false, aliases: new [] { "IBREAKENABLE" }) },
            { XtensaRegisters.ATOMCTL,  new CPURegister(43, 32, isGeneral: false, isReadonly: false, aliases: new [] { "ATOMCTL" }) },
            { XtensaRegisters.DDR,  new CPURegister(44, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DDR" }) },
            { XtensaRegisters.IBREAKA0,  new CPURegister(45, 32, isGeneral: false, isReadonly: false, aliases: new [] { "IBREAKA0" }) },
            { XtensaRegisters.IBREAKA1,  new CPURegister(46, 32, isGeneral: false, isReadonly: false, aliases: new [] { "IBREAKA1" }) },
            { XtensaRegisters.DBREAKA0,  new CPURegister(47, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DBREAKA0" }) },
            { XtensaRegisters.DBREAKA1,  new CPURegister(48, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DBREAKA1" }) },
            { XtensaRegisters.DBREAKC0,  new CPURegister(49, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DBREAKC0" }) },
            { XtensaRegisters.DBREAKC1,  new CPURegister(50, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DBREAKC1" }) },
            { XtensaRegisters.EPC1,  new CPURegister(51, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC1" }) },
            { XtensaRegisters.EPC2,  new CPURegister(52, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC2" }) },
            { XtensaRegisters.EPC3,  new CPURegister(53, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC3" }) },
            { XtensaRegisters.EPC4,  new CPURegister(54, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC4" }) },
            { XtensaRegisters.EPC5,  new CPURegister(55, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC5" }) },
            { XtensaRegisters.EPC6,  new CPURegister(56, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC6" }) },
            { XtensaRegisters.EPC7,  new CPURegister(57, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPC7" }) },
            { XtensaRegisters.DEPC,  new CPURegister(58, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DEPC" }) },
            { XtensaRegisters.EPS2,  new CPURegister(59, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS2" }) },
            { XtensaRegisters.EPS3,  new CPURegister(60, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS3" }) },
            { XtensaRegisters.EPS4,  new CPURegister(61, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS4" }) },
            { XtensaRegisters.EPS5,  new CPURegister(62, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS5" }) },
            { XtensaRegisters.EPS6,  new CPURegister(63, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS6" }) },
            { XtensaRegisters.EPS7,  new CPURegister(64, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EPS7" }) },
            { XtensaRegisters.EXCSAVE1,  new CPURegister(65, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE1" }) },
            { XtensaRegisters.EXCSAVE2,  new CPURegister(66, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE2" }) },
            { XtensaRegisters.EXCSAVE3,  new CPURegister(67, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE3" }) },
            { XtensaRegisters.EXCSAVE4,  new CPURegister(68, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE4" }) },
            { XtensaRegisters.EXCSAVE5,  new CPURegister(69, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE5" }) },
            { XtensaRegisters.EXCSAVE6,  new CPURegister(70, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE6" }) },
            { XtensaRegisters.EXCSAVE7,  new CPURegister(71, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCSAVE7" }) },
            { XtensaRegisters.INTERRUPT,  new CPURegister(72, 32, isGeneral: false, isReadonly: true, aliases: new [] { "INTERRUPT" }) },
            { XtensaRegisters.INTSET,  new CPURegister(73, 32, isGeneral: false, isReadonly: false, aliases: new [] { "INTSET" }) },
            { XtensaRegisters.INTCLEAR,  new CPURegister(74, 32, isGeneral: false, isReadonly: false, aliases: new [] { "INTCLEAR" }) },
            { XtensaRegisters.INTENABLE,  new CPURegister(75, 32, isGeneral: false, isReadonly: false, aliases: new [] { "INTENABLE" }) },
            { XtensaRegisters.VECBASE,  new CPURegister(76, 32, isGeneral: false, isReadonly: false, aliases: new [] { "VECBASE" }) },
            { XtensaRegisters.EXCCAUSE,  new CPURegister(77, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCCAUSE" }) },
            { XtensaRegisters.DEBUGCAUSE,  new CPURegister(78, 32, isGeneral: false, isReadonly: false, aliases: new [] { "DEBUGCAUSE" }) },
            { XtensaRegisters.CCOUNT,  new CPURegister(79, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CCOUNT" }) },
            { XtensaRegisters.PRID,  new CPURegister(80, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PRID" }) },
            { XtensaRegisters.ICOUNT,  new CPURegister(81, 32, isGeneral: false, isReadonly: false, aliases: new [] { "ICOUNT" }) },
            { XtensaRegisters.ICOUNTLEVEL,  new CPURegister(82, 32, isGeneral: false, isReadonly: false, aliases: new [] { "ICOUNTLEVEL" }) },
            { XtensaRegisters.EXCVADDR,  new CPURegister(83, 32, isGeneral: false, isReadonly: false, aliases: new [] { "EXCVADDR" }) },
            { XtensaRegisters.CCOMPARE0,  new CPURegister(84, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CCOMPARE0" }) },
            { XtensaRegisters.CCOMPARE1,  new CPURegister(85, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CCOMPARE1" }) },
            { XtensaRegisters.CCOMPARE2,  new CPURegister(86, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CCOMPARE2" }) },
            { XtensaRegisters.MISC0,  new CPURegister(87, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MISC0" }) },
            { XtensaRegisters.MISC1,  new CPURegister(88, 32, isGeneral: false, isReadonly: false, aliases: new [] { "MISC1" }) },
            { XtensaRegisters.A0,  new CPURegister(89, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A0" }) },
            { XtensaRegisters.A1,  new CPURegister(90, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A1" }) },
            { XtensaRegisters.A2,  new CPURegister(91, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A2" }) },
            { XtensaRegisters.A3,  new CPURegister(92, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A3" }) },
            { XtensaRegisters.A4,  new CPURegister(93, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A4" }) },
            { XtensaRegisters.A5,  new CPURegister(94, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A5" }) },
            { XtensaRegisters.A6,  new CPURegister(95, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A6" }) },
            { XtensaRegisters.A7,  new CPURegister(96, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A7" }) },
            { XtensaRegisters.A8,  new CPURegister(97, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A8" }) },
            { XtensaRegisters.A9,  new CPURegister(98, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A9" }) },
            { XtensaRegisters.A10,  new CPURegister(99, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A10" }) },
            { XtensaRegisters.A11,  new CPURegister(100, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A11" }) },
            { XtensaRegisters.A12,  new CPURegister(101, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A12" }) },
            { XtensaRegisters.A13,  new CPURegister(102, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A13" }) },
            { XtensaRegisters.A14,  new CPURegister(103, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A14" }) },
            { XtensaRegisters.A15,  new CPURegister(104, 32, isGeneral: false, isReadonly: false, aliases: new [] { "A15" }) },
            { XtensaRegisters.PSINTLEVEL,  new CPURegister(105, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSINTLEVEL" }) },
            { XtensaRegisters.PSUM,  new CPURegister(106, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSUM" }) },
            { XtensaRegisters.PSWOE,  new CPURegister(107, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSWOE" }) },
            { XtensaRegisters.PSEXCM,  new CPURegister(108, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSEXCM" }) },
            { XtensaRegisters.PSCALLINC,  new CPURegister(109, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSCALLINC" }) },
            { XtensaRegisters.PSOWB,  new CPURegister(110, 32, isGeneral: false, isReadonly: false, aliases: new [] { "PSOWB" }) },
        };
    }

    public enum XtensaRegisters
    {
        PC = 0,
        SAR = 33,
        WINDOWBASE = 34,
        WINDOWSTART = 35,
        PS = 38,
        EXPSTATE = 40,
        MMID = 41,
        IBREAKENABLE = 42,
        ATOMCTL = 43,
        DDR = 44,
        DEPC = 58,
        INTERRUPT = 72,
        INTSET = 73,
        INTCLEAR = 74,
        INTENABLE = 75,
        VECBASE = 76,
        EXCCAUSE = 77,
        DEBUGCAUSE = 78,
        CCOUNT = 79,
        PRID = 80,
        ICOUNT = 81,
        ICOUNTLEVEL = 82,
        EXCVADDR = 83,
        PSINTLEVEL = 105,
        PSUM = 106,
        PSWOE = 107,
        PSEXCM = 108,
        PSCALLINC = 109,
        PSOWB = 110,
        AR0 = 1,
        AR1 = 2,
        AR2 = 3,
        AR3 = 4,
        AR4 = 5,
        AR5 = 6,
        AR6 = 7,
        AR7 = 8,
        AR8 = 9,
        AR9 = 10,
        AR10 = 11,
        AR11 = 12,
        AR12 = 13,
        AR13 = 14,
        AR14 = 15,
        AR15 = 16,
        AR16 = 17,
        AR17 = 18,
        AR18 = 19,
        AR19 = 20,
        AR20 = 21,
        AR21 = 22,
        AR22 = 23,
        AR23 = 24,
        AR24 = 25,
        AR25 = 26,
        AR26 = 27,
        AR27 = 28,
        AR28 = 29,
        AR29 = 30,
        AR30 = 31,
        AR31 = 32,
        CONFIGID0 = 36,
        CONFIGID1 = 37,
        SCOMPARE1 = 39,
        IBREAKA0 = 45,
        IBREAKA1 = 46,
        DBREAKA0 = 47,
        DBREAKA1 = 48,
        DBREAKC0 = 49,
        DBREAKC1 = 50,
        EPC1 = 51,
        EPC2 = 52,
        EPC3 = 53,
        EPC4 = 54,
        EPC5 = 55,
        EPC6 = 56,
        EPC7 = 57,
        EPS2 = 59,
        EPS3 = 60,
        EPS4 = 61,
        EPS5 = 62,
        EPS6 = 63,
        EPS7 = 64,
        EXCSAVE1 = 65,
        EXCSAVE2 = 66,
        EXCSAVE3 = 67,
        EXCSAVE4 = 68,
        EXCSAVE5 = 69,
        EXCSAVE6 = 70,
        EXCSAVE7 = 71,
        CCOMPARE0 = 84,
        CCOMPARE1 = 85,
        CCOMPARE2 = 86,
        MISC0 = 87,
        MISC1 = 88,
        A0 = 89,
        A1 = 90,
        A2 = 91,
        A3 = 92,
        A4 = 93,
        A5 = 94,
        A6 = 95,
        A7 = 96,
        A8 = 97,
        A9 = 98,
        A10 = 99,
        A11 = 100,
        A12 = 101,
        A13 = 102,
        A14 = 103,
        A15 = 104,
    }
}
