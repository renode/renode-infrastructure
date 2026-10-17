//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 XDMA Engine (Cross-Domain DMA)
    // Reference: QEMU hw/misc/aspeed_xdma.c
    //
    // Stub peripheral: R/W register storage with W1C interrupt status.
    // No actual DMA transfers.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_XDMA : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_XDMA()
        {
            IRQ = new GPIO();
            storage = new uint[RegisterSpaceSize / 4];
            Reset();
        }

        public long Size => RegisterSpaceSize;
        public GPIO IRQ { get; }

        public void Reset()
        {
            Array.Clear(storage, 0, storage.Length);
            // QEMU resets IRQ_STATUS to 0xF8000000
            storage[R_IRQ_STATUS] = IRQ_STATUS_RESET;
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0 && offset < RegisterSpaceSize)
            {
                return storage[(uint)offset / 4];
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return;
            }

            var reg = (uint)offset / 4;

            switch(reg)
            {
                case R_IRQ_CTRL:
                    storage[reg] = value & IRQ_CTRL_W_MASK;
                    return;

                case R_IRQ_STATUS:
                    // W1C (write-1-to-clear)
                    storage[reg] &= ~value;
                    UpdateIRQ();
                    return;

                default:
                    storage[reg] = value;
                    return;
            }
        }

        private void UpdateIRQ()
        {
            bool pending = (storage[R_IRQ_STATUS] & storage[R_IRQ_CTRL]) != 0;
            IRQ.Set(pending);
        }

        private readonly uint[] storage;

        // AST2600 register offsets (word index)
        private const uint R_BMC_CMDQ_ADDR = 0x14 / 4;
        private const uint R_BMC_CMDQ_ENDP = 0x18 / 4;
        private const uint R_BMC_CMDQ_WRP  = 0x1C / 4;
        private const uint R_BMC_CMDQ_RDP  = 0x20 / 4;
        private const uint R_IRQ_CTRL      = 0x38 / 4;
        private const uint R_IRQ_STATUS    = 0x3C / 4;

        private const uint IRQ_STATUS_RESET = 0xF8000000;
        private const uint IRQ_CTRL_W_MASK  = 0x017003FF;

        private const int RegisterSpaceSize = 0x1000;
    }
}
