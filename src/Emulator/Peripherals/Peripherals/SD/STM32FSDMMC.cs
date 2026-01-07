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
    public class STM32FSDMMC : STM32SDMMC, IKnownSize
    {
        public STM32FSDMMC(IMachine machine) : base(machine)
        {
            DMAReceive = new GPIO();
        }

        public GPIO DMAReceive { get; }

        public long Size => 0x400;

        protected override void InitializeRegisters()
        {
            base.InitializeRegisters();

            Clock
                .WithTaggedFlag("Clock enable bit (CLKEN)", 8)
                .WithTaggedFlag("Power saving configuration bit (PWRSAV)", 9)
                .WithTaggedFlag("Clock divider bypass enable bit (BYPASS)", 10)
                .WithTag("Wide bus mode enable bit (WIDBUS)", 11, 2)
                .WithTaggedFlag("SDMMC_CK dephasing selection bit (NEGEDGE)", 13)
                .WithTaggedFlag("HW Flow Control enable (HWFC_EN)", 14)
                .WithReservedBits(15, 17);
            Cmd
                .WithTaggedFlag("SD I/O suspend command (SDIOSuspend)", 11)
                .WithReservedBits(12, 20);
            DataCtrl
                .WithTaggedFlag("Data transfer mode selection 1: Stream or SDIO multibyte data transfer. (DTMODE)", 2)
                .WithFlag(3, out dmaEnabled, name: "DMA enable bit (DMAEN)")
                .WithReservedBits(12, 20);

            Registers.FifoCount.Define(this)
                .WithValueField(0, 24, FieldMode.Read, name: "Remaining number of words to be written to or read from the FIFO. (FIFOCOUNT)", valueProviderCallback: _ =>
                {
                    if(ReadDataBuffer.Count > 0) return (ulong)ReadDataBuffer.Count;
                    return WriteDataLeft;
                })
                .WithReservedBits(24, 8);
        }

        protected override void ReadCard(SDCard sdCard, uint size)
        {
            base.ReadCard(sdCard, size);
            if(!dmaEnabled.Value)
            {
                return;
            }
            /* DMA reads data from FIFO in bursts of 4 bytes when this pin blinks */
            for(int i = 0; i < ReadDataBuffer.Count / DmaReadChunk; i++)
            {
                DMAReceive.Blink();
            }
        }

        protected override int ClkDivWidth { get => Stm32FClkDivWidth; }

        protected override int CommandFieldsOffset { get => Stm32FCommandFieldsOffset; }

        private IFlagRegisterField dmaEnabled;

        private const int DmaReadChunk = 4;

        private const int Stm32FClkDivWidth = 8;
        private const int Stm32FCommandFieldsOffset = 6;

        private enum Registers
        {
            FifoCount = 0x48,
        }
    }
}
