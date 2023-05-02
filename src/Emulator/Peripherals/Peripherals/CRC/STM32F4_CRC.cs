//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32F4_CRC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F4_CRC(Machine machine) : base(machine)
        {
            crc = new CRCEngine(CRCPolynomial.CRC32, init: 0xFFFFFFFF, xorOutput: 0xFFFFFFFF);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            crc.Reset();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithValueField(0, 32, name: "DR", valueProviderCallback: _ => crc.Value,
                    writeCallback: (_, value) => crc.Update(BitHelper.GetBytesFromValue(value, 4)));

            // This is a GP register that software can use to hold temporary data
            Registers.IndependentData.Define(this)
                .WithValueField(0, 8, name: "IDR")
                .WithReservedBits(8, 24);

            Registers.Control.Define(this)
                .WithFlag(0, name: "RESET", valueProviderCallback: _ => false, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            crc.Reset();
                        }
                    })
                .WithReservedBits(1, 31);
        }

        private readonly CRCEngine crc;

        private enum Registers
        {
            Data = 0x00,                // CRC_DR
            IndependentData = 0x04,     // CRC_IDR
            Control = 0x08              // CRC_CR
        }
    }
}
