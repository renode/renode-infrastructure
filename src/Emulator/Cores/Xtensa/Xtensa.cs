//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Xtensa : TranslationCPU
    {
        public Xtensa(string cpuType, Machine machine)
                : base(cpuType, machine, Endianess.LittleEndian)
        {
        }

        public override string Architecture { get { return "xtensa"; } }

        public override string GDBArchitecture { get { return "xtensa"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures => new List<GDBFeatureDescriptor>();

        protected override void AddNonMappedRegistersValues(ref Table table)
        {
            var nonMappedRegisterValues = new Dictionary<string, ulong>();
            var enumType = typeof(XtensaMaskedRegister);
            foreach(var register in enumType.GetEnumValues())
            {
                nonMappedRegisterValues.Add(
                    enumType.GetEnumName(register),
                    GetRegisterUnsafe((int)register).RawValue
                );
            }
            table.AddRows(nonMappedRegisterValues, x => x.Key, x => "0x{0:X}".FormatWith(x.Value));
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        protected bool TrySetNonMappedRegister(int register, RegisterValue value)
        {
            this.Log(LogLevel.Error, "Writing to PS* registers isn't implemented.");
            return false;
        }

        protected bool TryGetNonMappedRegister(int register, out RegisterValue value)
        {
            if(register >= (int)XtensaMaskedRegister.PSINTLEVEL
                && register <= (int)XtensaMaskedRegister.PSOWB)
            {
                if(GetRegisterMask(register, out var maskOffset, out var maskWidth))
                {
                    value = GetRegisterMaskedValue(PS, maskOffset, maskWidth);
                    return true;
                }
            }
            value = default(RegisterValue);
            return false;
        }

        protected IEnumerable<CPURegister> GetNonMappedRegisters()
        {
            var registers = new List<CPURegister>();
            foreach(var register in Enum.GetValues(typeof(XtensaMaskedRegister)))
            {
                registers.Add(new CPURegister((int)register, 32, false, false));
            }
            return registers;
        }

        private static bool GetRegisterMask(int register, out int offset, out int width)
        {
            switch((XtensaMaskedRegister)register)
            {
                case XtensaMaskedRegister.PSINTLEVEL:
                    offset = 0; width = 4;
                    break;
                case XtensaMaskedRegister.PSUM:
                    offset = 5; width = 1;
                    break;
                case XtensaMaskedRegister.PSWOE:
                    offset = 18; width = 1;
                    break;
                case XtensaMaskedRegister.PSEXCM:
                    offset = 4; width = 1;
                    break;
                case XtensaMaskedRegister.PSCALLINC:
                    offset = 16; width = 2;
                    break;
                case XtensaMaskedRegister.PSOWB:
                    offset = 8; width = 4;
                    break;
                default:
                    offset = -1; width = -1;
                    return false;
            }
            return true;
        }

        private RegisterValue GetRegisterMaskedValue(RegisterValue source, int maskOffset, int maskSize)
        {
            return RegisterValue.Create(
                BitHelper.GetMaskedValue((uint)source.RawValue, maskOffset, maskSize),
                (uint)maskSize);
        }

        private enum XtensaMaskedRegister
        {
            PSINTLEVEL = 105,
            PSUM = 106,
            PSWOE = 107,
            PSEXCM = 108,
            PSCALLINC = 109,
            PSOWB = 110,
        }
    }
}

