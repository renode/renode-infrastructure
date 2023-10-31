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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class NPCX_MDMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public NPCX_MDMA(IMachine machine) : base(machine)
        {
            base.Reset();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            DefineChannel0Registers();
            DefineChannel1Registers();
        }

        private void DefineChannel0Registers()
        {
            Registers.Channel0Control.Define(this)
                .WithTag("MDMAEN (MDMA Enable)", 0, 1)
                .WithTag("MPD (MDMA Power-Down)", 1, 1)
                .WithReservedBits(2, 6)
                .WithTag("SIEN (Stop Interrupt Enable)", 8, 1)
                .WithReservedBits(9, 5)
                .WithTag("MPS (MDMA Power Save)", 14, 1)
                .WithReservedBits(15, 3)
                .WithTag("TC (Terminal Count)", 18, 1)
                .WithReservedBits(19, 13);

            Registers.Channel0SourceBaseAddress.Define(this)
                .WithTag("SRC_BASE_ADDR (32-bit Source Base Address)", 0, 32);

            Registers.Channel0DestinationBaseAddress.Define(this)
                .WithTag("DST_BASE_ADDR19-0 (20-bit Destination Base Address)", 0, 20)
                .WithTag("DST_BASE_ADDR31-20 (32-bit Destination Base Address)", 20, 12);

            Registers.Channel0TransferCount.Define(this)
                .WithTag("TFR_CNT (13-bit Transfer Count)", 0, 13)
                .WithReservedBits(13, 19);

            Registers.Channel0CurrentDestination.Define(this)
                .WithTag("CURRENT_DST_ADDR (32-bit Current Destination Address)", 0, 32);

            Registers.Channel0CurrentTransferCount.Define(this)
                .WithTag("CURENT_TFR_CNT (13-bit Current Transfer Count)", 0, 13)
                .WithReservedBits(13, 19);
        }

        private void DefineChannel1Registers()
        {
            Registers.Channel1Control.Define(this)
                .WithTag("MDMAEN (MDMA Enable)", 0, 1)
                .WithTag("MPD (MDMA Power-Down)", 1, 1)
                .WithReservedBits(2, 6)
                .WithTag("SIEN (Stop Interrupt Enable)", 8, 1)
                .WithReservedBits(9, 5)
                .WithTag("MPS (MDMA Power Save)", 14, 1)
                .WithReservedBits(15, 3)
                .WithTag("TC (Terminal Count)", 18, 1)
                .WithReservedBits(19, 13);

            Registers.Channel1SourceBaseAddress.Define(this)
                .WithTag("SRC_BASE_ADDR19-0 (20-bit Source Base Address)", 0, 20)
                .WithTag("SRC_BASE_ADDR31-20 (12-bit Source Base Address)", 20, 12);

            Registers.Channel1DestinationBaseAddress.Define(this)
                .WithTag("DST_BASE_ADDR (32-bit Destination Base Address)", 0, 32);

            Registers.Channel1TransferCount.Define(this)
                .WithTag("TFR_CNT (13-bit Transfer Count)", 0, 13)
                .WithReservedBits(13, 19);

            Registers.Channel1CurrentSource.Define(this)
                .WithTag("CURRENT_SRC_ADDR (32-bit Current Source Address)", 0, 32);

            Registers.Channel1CurrentTransferCount.Define(this)
                .WithTag("CURENT_TFR_CNT (13-bit Current Transfer Count)", 0, 13)
                .WithReservedBits(13, 19);
        }

        private enum Registers
        {
            Channel0Control = 0x0,
            Channel0SourceBaseAddress = 0x4,
            Channel0DestinationBaseAddress = 0x8,
            Channel0TransferCount = 0xC,
            Channel0CurrentDestination = 0x14,
            Channel0CurrentTransferCount = 0x18,
            Channel1Control = 0x20,
            Channel1SourceBaseAddress = 0x24,
            Channel1DestinationBaseAddress = 0x28,
            Channel1TransferCount = 0x2C,
            Channel1CurrentSource = 0x30,
            Channel1CurrentTransferCount = 0x38
        }
    }
}
