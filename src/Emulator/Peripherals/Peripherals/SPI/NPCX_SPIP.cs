//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToWord | AllowedTranslation.WordToByte)]
    public class NPCX_SPIP : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IKnownSize
    {
        public NPCX_SPIP(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            registerCollection.Reset();
        }

        public ushort ReadWord(long offset)
        {
            return registerCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            registerCollection.Write(offset, value);
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary <long, WordRegister>();

            registersMap[(long)Registers.DataInOut] = new WordRegister(this)
                .WithTag("DATA (SPIP Read/Write Data)", 0, 16);

            registersMap[(long)Registers.Control1] = new WordRegister(this)
                .WithTag("SPIEN (SPI Enable)", 0, 1)
                .WithReservedBits(1, 1)
                .WithTag("MOD (Data Interface Mode)", 2, 1)
                .WithReservedBits(3, 2)
                .WithTag("EIR (Enable Interrupt for Read)", 5, 1)
                .WithTag("EIW (Enable Interrupt for Write)", 6, 1)
                .WithTag("SCM (Clocking Mode)", 7, 1)
                .WithTag("SCIDL (Value of SPI_SCLK when Bus is Idle)", 8, 1)
                .WithTag("SCDV6-0 (Shift Clock Divider Value)", 9, 7);

            registersMap[(long)Registers.Status] = new WordRegister(this)
                .WithTag("BSY (Shift Register Busy)", 0, 1)
                .WithTag("RBF (Read Buffer Full)", 1, 1)
                .WithReservedBits(2, 14);

            registerCollection = new WordRegisterCollection(this, registersMap);
        }

        private WordRegisterCollection registerCollection;

        private enum Registers
        {
            DataInOut = 0x0,
            Control1 = 0x2,
            Status = 0x4
        }
    }
}
