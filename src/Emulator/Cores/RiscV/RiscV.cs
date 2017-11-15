//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 3)]
    public partial class RiscV : TranslationCPU
    {
        public RiscV(string cpuType, Machine machine, Endianess endianness = Endianess.LittleEndian): base(cpuType, machine, endianness)
        {
        }

        public override void OnGPIO(int number, bool value)
        {
            IrqType decodedType;
            switch(number)
            {
            case 0:
                decodedType = IrqType.MachineTimerIrq;
                break;
            case 1:
                decodedType = IrqType.MachineExternalIrq;
                break;
            case 2:
                decodedType = IrqType.MachineSoftwareInterrupt;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            var mipState = TlibGetMip();
            BitHelper.SetBit(ref mipState, (byte)decodedType, value);
            TlibSetMip(mipState);

            base.OnGPIO(number, value);
        }

        public override string Architecture { get { return "riscv"; } }

        public uint EntryPoint { get; private set; }

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0)
            {
                return Interrupt.Hard;
            }
            if(number == 1 || number == 2)
            {
                return Interrupt.TargetExternal0;
            }
            throw InvalidInterruptNumberException;
        }

        [Export]
        private void SetMip(uint mip)
        {
            OnGPIO(2, true);
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private FuncUInt32 TlibGetMip;

        [Import]
        private ActionUInt32 TlibSetMip;
#pragma warning restore 649

        private enum IrqType
        {
            MachineSoftwareInterrupt = 0x3,
            MachineTimerIrq = 0x7,
            MachineExternalIrq = 0xb
        }
    }
}

