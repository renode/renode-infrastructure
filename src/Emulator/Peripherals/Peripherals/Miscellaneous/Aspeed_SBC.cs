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
    // Aspeed AST2600 Secure Boot Controller (SBC) / OTP Controller
    // Reference: QEMU hw/misc/aspeed_sbc.c
    //
    // Stub peripheral: reports "not secured" (normal boot).
    // OTP commands accepted but return idle status immediately.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_SBC : IDoubleWordPeripheral, IKnownSize
    {
        public Aspeed_SBC()
        {
            storage = new uint[RegisterSpaceSize / 4];
            Reset();
        }

        public long Size => RegisterSpaceSize;

        public void Reset()
        {
            Array.Clear(storage, 0, storage.Length);
            // OTP idle, not secured, not in UART boot mode
            storage[R_STATUS] = OTP_IDLE | OTP_MEM_IDLE;
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
                case R_STATUS:
                case R_QSR:
                    // Read-only registers
                    return;

                case R_CMD:
                    // Accept OTP commands but just return to idle immediately
                    storage[R_STATUS] |= OTP_IDLE | OTP_MEM_IDLE;
                    storage[reg] = value;
                    return;

                default:
                    storage[reg] = value;
                    return;
            }
        }

        private readonly uint[] storage;

        // Register offsets (word index)
        private const uint R_PROT   = 0x000 / 4;
        private const uint R_CMD    = 0x004 / 4;
        private const uint R_ADDR   = 0x010 / 4;
        private const uint R_STATUS = 0x014 / 4;
        private const uint R_CAMP1  = 0x020 / 4;
        private const uint R_CAMP2  = 0x024 / 4;
        private const uint R_QSR    = 0x040 / 4;

        // Status bits
        private const uint OTP_MEM_IDLE = 1u << 1;
        private const uint OTP_IDLE     = 1u << 2;

        private const int RegisterSpaceSize = 0x1000;
    }
}
