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
        public override void SetRegisterUnsafe(int register, RegisterValue value)
        {
            if(!mapping.TryGetValue((XtensaRegisters)register, out var r))
            {
                throw new RecoverableException($"Wrong register index: {register}");
            }

            SetRegisterValue32(r.Index, checked((UInt32)value));
        }

        public override RegisterValue GetRegisterUnsafe(int register)
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
        public RegistersGroup AR { get; private set; }

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
            };
            AR = new RegistersGroup(
                indexValueMapAR.Keys,
                i => GetRegisterUnsafe((int)indexValueMapAR[i]),
                (i, v) => SetRegisterUnsafe((int)indexValueMapAR[i], v));

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
            { XtensaRegisters.AR0,  new CPURegister(0, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR1,  new CPURegister(1, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR2,  new CPURegister(2, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR3,  new CPURegister(3, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR4,  new CPURegister(4, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR5,  new CPURegister(5, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR6,  new CPURegister(6, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR7,  new CPURegister(7, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR8,  new CPURegister(8, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR9,  new CPURegister(9, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR10,  new CPURegister(10, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR11,  new CPURegister(11, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR12,  new CPURegister(12, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR13,  new CPURegister(13, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR14,  new CPURegister(14, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.AR15,  new CPURegister(15, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.PC,  new CPURegister(16, 32, isGeneral: true, isReadonly: false) },
            { XtensaRegisters.SAR,  new CPURegister(17, 32, isGeneral: false, isReadonly: false) },
        };
    }

    public enum XtensaRegisters
    {
        PC = 16,
        SAR = 17,
        AR0 = 0,
        AR1 = 1,
        AR2 = 2,
        AR3 = 3,
        AR4 = 4,
        AR5 = 5,
        AR6 = 6,
        AR7 = 7,
        AR8 = 8,
        AR9 = 9,
        AR10 = 10,
        AR11 = 11,
        AR12 = 12,
        AR13 = 13,
        AR14 = 14,
        AR15 = 15,
    }
}
