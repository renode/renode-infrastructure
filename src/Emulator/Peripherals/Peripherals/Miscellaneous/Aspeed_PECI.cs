//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 PECI (Platform Environment Control Interface) Controller
    // Reference: QEMU hw/misc/aspeed_peci.c
    //
    // Stub peripheral: command FIRE bit auto-completes with success code.
    // No actual PECI bus transactions.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_PECI : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_PECI()
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
                case R_CMD:
                    storage[reg] = value;
                    if((value & CMD_FIRE) != 0)
                    {
                        // Auto-complete: clear FIRE, set CMD_DONE in INT_STS
                        storage[R_CMD] = value & ~CMD_FIRE;
                        storage[R_INT_STS] |= INT_CMD_DONE;
                        // Write success code (0x40) into read data buffer 0
                        storage[R_RD_DATA0] = PECI_CC_RSP_SUCCESS;
                        UpdateIRQ();
                    }
                    return;

                case R_INT_STS:
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
            bool pending = (storage[R_INT_STS] & storage[R_INT_CTRL]) != 0;
            IRQ.Set(pending);
        }

        private readonly uint[] storage;

        private const uint R_CMD      = 0x08 / 4;
        private const uint R_INT_CTRL = 0x18 / 4;
        private const uint R_INT_STS  = 0x1C / 4;
        private const uint R_RD_DATA0 = 0x30 / 4;

        private const uint CMD_FIRE            = 1u << 0;
        private const uint INT_CMD_DONE        = 1u << 0;
        private const uint PECI_CC_RSP_SUCCESS = 0x40u;

        private const int RegisterSpaceSize = 0x1000;
    }
}
