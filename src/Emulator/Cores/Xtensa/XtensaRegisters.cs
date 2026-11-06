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

            SetRegisterValue32(r.Index, checked((uint)value));
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

        [Register]
        public RegisterValue LBEG
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.LBEG);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.LBEG, value);
            }
        }

        [Register]
        public RegisterValue LEND
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.LEND);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.LEND, value);
            }
        }

        [Register]
        public RegisterValue LCOUNT
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.LCOUNT);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.LCOUNT, value);
            }
        }

        [Register]
        public RegisterValue BR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.BR);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.BR, value);
            }
        }

        [Register]
        public RegisterValue ACCLO
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.ACCLO);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.ACCLO, value);
            }
        }

        [Register]
        public RegisterValue ACCHI
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.ACCHI);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.ACCHI, value);
            }
        }

        [Register]
        public RegisterValue CPENABLE
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.CPENABLE);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.CPENABLE, value);
            }
        }

        [Register]
        public RegisterValue THREADPTR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.THREADPTR);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.THREADPTR, value);
            }
        }

        [Register]
        public RegisterValue FCR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.FCR);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.FCR, value);
            }
        }

        [Register]
        public RegisterValue FSR
        {
            get
            {
                return GetRegisterValue32((int)XtensaRegisters.FSR);
            }

            set
            {
                SetRegisterValue32((int)XtensaRegisters.FSR, value);
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

        public RegistersGroup M { get; private set; }

        public RegistersGroup F { get; private set; }

#pragma warning disable SA1508
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
                { 32, XtensaRegisters.AR32 },
                { 33, XtensaRegisters.AR33 },
                { 34, XtensaRegisters.AR34 },
                { 35, XtensaRegisters.AR35 },
                { 36, XtensaRegisters.AR36 },
                { 37, XtensaRegisters.AR37 },
                { 38, XtensaRegisters.AR38 },
                { 39, XtensaRegisters.AR39 },
                { 40, XtensaRegisters.AR40 },
                { 41, XtensaRegisters.AR41 },
                { 42, XtensaRegisters.AR42 },
                { 43, XtensaRegisters.AR43 },
                { 44, XtensaRegisters.AR44 },
                { 45, XtensaRegisters.AR45 },
                { 46, XtensaRegisters.AR46 },
                { 47, XtensaRegisters.AR47 },
                { 48, XtensaRegisters.AR48 },
                { 49, XtensaRegisters.AR49 },
                { 50, XtensaRegisters.AR50 },
                { 51, XtensaRegisters.AR51 },
                { 52, XtensaRegisters.AR52 },
                { 53, XtensaRegisters.AR53 },
                { 54, XtensaRegisters.AR54 },
                { 55, XtensaRegisters.AR55 },
                { 56, XtensaRegisters.AR56 },
                { 57, XtensaRegisters.AR57 },
                { 58, XtensaRegisters.AR58 },
                { 59, XtensaRegisters.AR59 },
                { 60, XtensaRegisters.AR60 },
                { 61, XtensaRegisters.AR61 },
                { 62, XtensaRegisters.AR62 },
                { 63, XtensaRegisters.AR63 },
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

            var indexValueMapM = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.M0 },
                { 1, XtensaRegisters.M1 },
                { 2, XtensaRegisters.M2 },
                { 3, XtensaRegisters.M3 },
            };
            M = new RegistersGroup(
                indexValueMapM.Keys,
                i => GetRegister((int)indexValueMapM[i]),
                (i, v) => SetRegister((int)indexValueMapM[i], v));

            var indexValueMapF = new Dictionary<int, XtensaRegisters>
            {
                { 0, XtensaRegisters.F0 },
                { 1, XtensaRegisters.F1 },
                { 2, XtensaRegisters.F2 },
                { 3, XtensaRegisters.F3 },
                { 4, XtensaRegisters.F4 },
                { 5, XtensaRegisters.F5 },
                { 6, XtensaRegisters.F6 },
                { 7, XtensaRegisters.F7 },
                { 8, XtensaRegisters.F8 },
                { 9, XtensaRegisters.F9 },
                { 10, XtensaRegisters.F10 },
                { 11, XtensaRegisters.F11 },
                { 12, XtensaRegisters.F12 },
                { 13, XtensaRegisters.F13 },
                { 14, XtensaRegisters.F14 },
                { 15, XtensaRegisters.F15 },
            };
            F = new RegistersGroup(
                indexValueMapF.Keys,
                i => GetRegister((int)indexValueMapF[i]),
                (i, v) => SetRegister((int)indexValueMapF[i], v));

        }
#pragma warning restore SA1508

#pragma warning disable 649
        // 649:  Field '...' is never assigned to, and will always have its default value null
        [Import(Name = "tlib_set_register_value_32")]
        protected Action<int, uint> SetRegisterValue32;

        [Import(Name = "tlib_get_register_value_32")]
        protected Func<int, uint> GetRegisterValue32;
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
            { XtensaRegisters.AR32,  new CPURegister(111, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR32" }) },
            { XtensaRegisters.AR33,  new CPURegister(112, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR33" }) },
            { XtensaRegisters.AR34,  new CPURegister(113, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR34" }) },
            { XtensaRegisters.AR35,  new CPURegister(114, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR35" }) },
            { XtensaRegisters.AR36,  new CPURegister(115, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR36" }) },
            { XtensaRegisters.AR37,  new CPURegister(116, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR37" }) },
            { XtensaRegisters.AR38,  new CPURegister(117, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR38" }) },
            { XtensaRegisters.AR39,  new CPURegister(118, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR39" }) },
            { XtensaRegisters.AR40,  new CPURegister(119, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR40" }) },
            { XtensaRegisters.AR41,  new CPURegister(120, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR41" }) },
            { XtensaRegisters.AR42,  new CPURegister(121, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR42" }) },
            { XtensaRegisters.AR43,  new CPURegister(122, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR43" }) },
            { XtensaRegisters.AR44,  new CPURegister(123, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR44" }) },
            { XtensaRegisters.AR45,  new CPURegister(124, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR45" }) },
            { XtensaRegisters.AR46,  new CPURegister(125, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR46" }) },
            { XtensaRegisters.AR47,  new CPURegister(126, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR47" }) },
            { XtensaRegisters.AR48,  new CPURegister(127, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR48" }) },
            { XtensaRegisters.AR49,  new CPURegister(128, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR49" }) },
            { XtensaRegisters.AR50,  new CPURegister(129, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR50" }) },
            { XtensaRegisters.AR51,  new CPURegister(130, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR51" }) },
            { XtensaRegisters.AR52,  new CPURegister(131, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR52" }) },
            { XtensaRegisters.AR53,  new CPURegister(132, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR53" }) },
            { XtensaRegisters.AR54,  new CPURegister(133, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR54" }) },
            { XtensaRegisters.AR55,  new CPURegister(134, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR55" }) },
            { XtensaRegisters.AR56,  new CPURegister(135, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR56" }) },
            { XtensaRegisters.AR57,  new CPURegister(136, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR57" }) },
            { XtensaRegisters.AR58,  new CPURegister(137, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR58" }) },
            { XtensaRegisters.AR59,  new CPURegister(138, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR59" }) },
            { XtensaRegisters.AR60,  new CPURegister(139, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR60" }) },
            { XtensaRegisters.AR61,  new CPURegister(140, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR61" }) },
            { XtensaRegisters.AR62,  new CPURegister(141, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR62" }) },
            { XtensaRegisters.AR63,  new CPURegister(142, 32, isGeneral: false, isReadonly: false, aliases: new [] { "AR63" }) },
            { XtensaRegisters.LBEG,  new CPURegister(143, 32, isGeneral: false, isReadonly: false, aliases: new [] { "LBEG" }) },
            { XtensaRegisters.LEND,  new CPURegister(144, 32, isGeneral: false, isReadonly: false, aliases: new [] { "LEND" }) },
            { XtensaRegisters.LCOUNT,  new CPURegister(145, 32, isGeneral: false, isReadonly: false, aliases: new [] { "LCOUNT" }) },
            { XtensaRegisters.BR,  new CPURegister(146, 32, isGeneral: false, isReadonly: false, aliases: new [] { "BR" }) },
            { XtensaRegisters.ACCLO,  new CPURegister(147, 32, isGeneral: false, isReadonly: false, aliases: new [] { "ACCLO" }) },
            { XtensaRegisters.ACCHI,  new CPURegister(148, 32, isGeneral: false, isReadonly: false, aliases: new [] { "ACCHI" }) },
            { XtensaRegisters.M0,  new CPURegister(149, 32, isGeneral: false, isReadonly: false, aliases: new [] { "M0" }) },
            { XtensaRegisters.M1,  new CPURegister(150, 32, isGeneral: false, isReadonly: false, aliases: new [] { "M1" }) },
            { XtensaRegisters.M2,  new CPURegister(151, 32, isGeneral: false, isReadonly: false, aliases: new [] { "M2" }) },
            { XtensaRegisters.M3,  new CPURegister(152, 32, isGeneral: false, isReadonly: false, aliases: new [] { "M3" }) },
            { XtensaRegisters.CPENABLE,  new CPURegister(153, 32, isGeneral: false, isReadonly: false, aliases: new [] { "CPENABLE" }) },
            { XtensaRegisters.THREADPTR,  new CPURegister(154, 32, isGeneral: false, isReadonly: false, aliases: new [] { "THREADPTR" }) },
            { XtensaRegisters.FCR,  new CPURegister(155, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FCR" }) },
            { XtensaRegisters.FSR,  new CPURegister(156, 32, isGeneral: false, isReadonly: false, aliases: new [] { "FSR" }) },
            { XtensaRegisters.F0,  new CPURegister(157, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F0" }) },
            { XtensaRegisters.F1,  new CPURegister(158, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F1" }) },
            { XtensaRegisters.F2,  new CPURegister(159, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F2" }) },
            { XtensaRegisters.F3,  new CPURegister(160, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F3" }) },
            { XtensaRegisters.F4,  new CPURegister(161, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F4" }) },
            { XtensaRegisters.F5,  new CPURegister(162, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F5" }) },
            { XtensaRegisters.F6,  new CPURegister(163, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F6" }) },
            { XtensaRegisters.F7,  new CPURegister(164, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F7" }) },
            { XtensaRegisters.F8,  new CPURegister(165, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F8" }) },
            { XtensaRegisters.F9,  new CPURegister(166, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F9" }) },
            { XtensaRegisters.F10,  new CPURegister(167, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F10" }) },
            { XtensaRegisters.F11,  new CPURegister(168, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F11" }) },
            { XtensaRegisters.F12,  new CPURegister(169, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F12" }) },
            { XtensaRegisters.F13,  new CPURegister(170, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F13" }) },
            { XtensaRegisters.F14,  new CPURegister(171, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F14" }) },
            { XtensaRegisters.F15,  new CPURegister(172, 32, isGeneral: false, isReadonly: false, aliases: new [] { "F15" }) },
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
        LBEG = 143,
        LEND = 144,
        LCOUNT = 145,
        BR = 146,
        ACCLO = 147,
        ACCHI = 148,
        CPENABLE = 153,
        THREADPTR = 154,
        FCR = 155,
        FSR = 156,
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
        AR32 = 111,
        AR33 = 112,
        AR34 = 113,
        AR35 = 114,
        AR36 = 115,
        AR37 = 116,
        AR38 = 117,
        AR39 = 118,
        AR40 = 119,
        AR41 = 120,
        AR42 = 121,
        AR43 = 122,
        AR44 = 123,
        AR45 = 124,
        AR46 = 125,
        AR47 = 126,
        AR48 = 127,
        AR49 = 128,
        AR50 = 129,
        AR51 = 130,
        AR52 = 131,
        AR53 = 132,
        AR54 = 133,
        AR55 = 134,
        AR56 = 135,
        AR57 = 136,
        AR58 = 137,
        AR59 = 138,
        AR60 = 139,
        AR61 = 140,
        AR62 = 141,
        AR63 = 142,
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
        M0 = 149,
        M1 = 150,
        M2 = 151,
        M3 = 152,
        F0 = 157,
        F1 = 158,
        F2 = 159,
        F3 = 160,
        F4 = 161,
        F5 = 162,
        F6 = 163,
        F7 = 164,
        F8 = 165,
        F9 = 166,
        F10 = 167,
        F11 = 168,
        F12 = 169,
        F13 = 170,
        F14 = 171,
        F15 = 172,
    }
}