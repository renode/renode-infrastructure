//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Utilities.Collections;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 3)]
    public partial class RiscV : TranslationCPU
    {
        public RiscV(long frequency, string cpuType, Machine machine, Endianess endianness = Endianess.LittleEndian) : base(cpuType, machine, endianness)
        {
            innerTimer = new ComparingTimer(machine, frequency, enabled: true, eventEnabled: true);

            intTypeToVal = new TwoWayDictionary<int, IrqType>();
            intTypeToVal.Add(0, IrqType.MachineTimerIrq);
            intTypeToVal.Add(1, IrqType.MachineExternalIrq);
            intTypeToVal.Add(2, IrqType.MachineSoftwareInterrupt);
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!intTypeToVal.TryGetValue(number, out IrqType decodedType))
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            var mipState = TlibGetMip();
            BitHelper.SetBit(ref mipState, (byte)decodedType, value);
            TlibSetMip(mipState);

            base.OnGPIO(number, mipState != 0);
        }

        public override string Architecture { get { return "riscv"; } }

        public uint EntryPoint { get; private set; }

        public readonly ComparingTimer innerTimer;

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0 || number == 1 || number == 2)
            {
                return Interrupt.Hard;
            }
            throw InvalidInterruptNumberException;
        }

        [Export]
        private void MipChanged(uint mip)
        {
            var previousMip = BitHelper.GetBits(TlibGetMip());
            var currentMip = BitHelper.GetBits(mip);

            foreach(var gpio in intTypeToVal.Lefts)
            {
                intTypeToVal.TryGetValue(gpio, out IrqType decodedType);
                if(previousMip[(int)decodedType] != currentMip[(int)decodedType])
                {
                    OnGPIO(gpio, currentMip[(int)decodedType]);
                }
            }
        }

        private TwoWayDictionary<int, IrqType> intTypeToVal;

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

