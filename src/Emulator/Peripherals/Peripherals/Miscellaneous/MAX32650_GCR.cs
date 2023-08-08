//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MAX32650_GCR : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_GCR(IMachine machine, IHasFrequency nvic) : base(machine)
        {
            DefineRegisters();

            nvic.Frequency = SysClk;
            SysClkChanged += (frequency) =>
            {
                nvic.Frequency = frequency;
            };
        }

        public Action<long> SysClkChanged;

        public long SysClk
        {
            get
            {
                return oscillators[(int)sysclkSelect.Value] >> (int)sysclkPrescaler.Value;
            }
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.ClockControl.Define(this, 0x0C0C0010)
                .WithReservedBits(0, 6)
                .WithValueField(6, 3, out sysclkPrescaler, name: "SYSCLK_PRESCALE",
                    changeCallback: (_, __) =>
                    {
                        SysClkChanged?.Invoke(SysClk);
                    })
                .WithValueField(9, 3, out sysclkSelect, name: "SYSOSC_SEL",
                    changeCallback: (_, __) =>
                    {
                        SysClkChanged?.Invoke(SysClk);
                    })
                .WithReservedBits(12, 1)
                .WithFlag(13, name: "SYSOSC_RDY", valueProviderCallback: _ => true)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CCD", 15)
                .WithReservedBits(16, 1)
                .WithFlag(17, name: "X32K_EN")
                .WithFlag(18, name: "HIRC50M_EN")
                .WithFlag(19, name: "HIRCMM_EN")
                .WithFlag(20, name: "HIRC7M_EN")
                .WithTaggedFlag("HIRC7M_VS", 21)
                .WithReservedBits(22, 3)
                .WithTaggedFlag("X32K_RDY", 25)
                .WithTaggedFlag("HIRC50M_RDY", 26)
                .WithTaggedFlag("HIRCMM_RDY", 27)
                .WithTaggedFlag("HIRC7M_RDY", 28)
                .WithTaggedFlag("LIRC8K_RDY", 29)
                .WithReservedBits(30, 2);
        }

        private IValueRegisterField sysclkPrescaler;
        private IValueRegisterField sysclkSelect;

        // Frequencies of oscillators (in Hz)
        private readonly List<long> oscillators = new List<long>
        {
            50000000,
            0, // Reserved
            0, // Reserved
            8000,
            120000000,
            7372800,
            32768,
            0 // Reserved
        };

        private enum Registers
        {
            SystemControl = 0x00,
            Reset0 = 0x04,
            ClockControl = 0x08,
            PowerManagement = 0x0C,
            PeripheralClockDivisor = 0x18,
            PeripheralClocksDisable0 = 0x24,
            MemoryClock = 0x2C,
            MemoryZeroize = 0x40,
            Reset1 = 0x44,
            PeripheralClocksDisable1 = 0x48,
            EventEnable = 0x4C,
            Revision = 0x50,
        }
    }
}
