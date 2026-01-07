//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.SD
{
    public class STM32HSDMMC : STM32SDMMC, IKnownSize
    {
        public STM32HSDMMC(IMachine machine) : base(machine) { }

        public long Size => 0x2000;

        protected override void InitializeRegisters()
        {
            base.InitializeRegisters();

            Clock
                .WithReservedBits(10, 2)
                .WithTaggedFlag("Power saving configuration bit (PWRSAV)", 12)
                .WithReservedBits(13, 1)
                .WithTag("Wide bus mode enable bit (WIDBUS)", 14, 2)
                .WithTaggedFlag("SDMMC_CK dephasing selection bit for data and command (NEGEDGE)", 16)
                .WithTaggedFlag("Hardware flow control enable (HWFC_EN)", 17)
                .WithTaggedFlag("Data rate signaling selection (DDR)", 18)
                .WithTaggedFlag("Bus speed for selection of SDMMC operating modes (BUSSPEED)", 19)
                .WithTag("Receive clock selection (SELCLKRX)", 20, 2)
                .WithReservedBits(22, 10);
            Cmd
                .WithFlag(6, name: "The CPSM treats the command as a data transfer command, stops the interrupt period, and signals DataEnable to the DPSM (CMDTRANS)") // hush
                .WithFlag(7, name: "The CPSM treats the command as a Stop Transmission command and signals abort to the DPSM (CMDSTOP)") // hush
                .WithTaggedFlag("Hold new data block transmission and reception in the DPSM (DTHOLD)", 13)
                .WithTaggedFlag("Select the boot mode procedure to be used (BOOTMODE)", 14)
                .WithTaggedFlag("Enable boot mode procedure (BOOTEN)", 15)
                .WithTaggedFlag("The CPSM treats the command as a Suspend or Resume command and signals interrupt period start/end (CMDSUSPEND)", 16)
                .WithReservedBits(17, 15);
            DataCtrl
                .WithTag("Data transfer mode selection (DTMODE)", 2, 2)
                .WithTaggedFlag("Enable the reception of the boot acknowledgment (BOOTACKEN)", 12)
                .WithTaggedFlag("FIFO reset, flushes any remaining data (FIFORST)", 13)
                .WithReservedBits(14, 18);

            for(long offset = 0x4; offset < 0x40; offset += 0x4)
            {
                RegistersCollection.DefineRegister((long)Registers.Fifo + offset)
                    .WithTag("Receive and transmit FIFO data (FIFODATA)", 0, 32);
            }
            Registers.IDMACtrl.Define(this)
                .WithTaggedFlag("IDMA enable (IDMAEN)", 0)
                .WithTaggedFlag("Buffer mode selection (IDMABMODE)", 1)
                .WithTaggedFlag("Double buffer mode active buffer indication (IDMABACT)", 2)
                .WithReservedBits(3, 29);
            Registers.IDMABSize.Define(this)
                .WithReservedBits(0, 5)
                .WithTag("Number of bytes per buffer (IDMABNDT)", 5, 8)
                .WithReservedBits(13, 19);
            Registers.IDMABase0.Define(this)
                .WithTag("Buffer 0 memory base address (IDMABASE0)", 0, 32);
            Registers.IDMABase1.Define(this)
                .WithTag("Buffer 1 memory base address (IDMABASE1)", 0, 32);
        }

        protected override int ClkDivWidth { get => Stm32HClkDivWidth; }

        protected override int CommandFieldsOffset { get => Stm32HCommandFieldsOffset; }

        protected override bool RxFifoEEnableable { get => false; }

        protected override bool TxFifoFEnableable { get => false; }

        private const int Stm32HClkDivWidth = 10;
        private const int Stm32HCommandFieldsOffset = 8;

        private enum Registers
        {
            IDMACtrl = 0x50,
            IDMABSize = 0x54,
            IDMABase0 = 0x58,
            IDMABase1 = 0x5C,
            // Same as in base class, need for FIFODATA tags
            Fifo = 0x80
        }
    }
}
