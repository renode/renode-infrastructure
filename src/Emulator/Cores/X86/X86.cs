//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Endianess = ELFSharp.ELF.Endianess;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Peripherals.IRQControllers;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 1)]
    public partial class X86 : TranslationCPU
    {
        const Endianess endianness = Endianess.LittleEndian;

        public X86(string cpuType, Machine machine, LAPIC lapic): base(cpuType, machine, endianness)
        {
            this.lapic = lapic;
        }

        public override string Architecture { get { return "i386"; } }

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0)
            {
                return Interrupt.Hard;
            }
            throw InvalidInterruptNumberException;
        }

        [Export]
        private uint ReadByteFromPort(uint address)
        {
            return ReadByteFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private uint ReadWordFromPort(uint address)
        {
            return ReadWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private uint ReadDoubleWordFromPort(uint address)
        {
            return ReadDoubleWordFromBus(IoPortBaseAddress + address);
        }

        [Export]
        private void WriteByteToPort(uint address, uint value)
        {
            WriteByteToBus(IoPortBaseAddress + address, value);

        }

        [Export]
        private void WriteWordToPort(uint address, uint value)
        {
            WriteWordToBus(IoPortBaseAddress + address, value);
        }

        [Export]
        private void WriteDoubleWordToPort(uint address, uint value)
        {
            WriteDoubleWordToBus(IoPortBaseAddress + address, value);
        }

        [Export]
        private int GetPendingInterrupt()
        {
            return lapic.GetPendingInterrupt();
        }

        [Export]
        private ulong GetInstructionCount()
        {
            return this.ExecutedInstructions;
        }

        private readonly LAPIC lapic;
        private const uint IoPortBaseAddress = 0xE0000000;
    }
}

