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
    // Aspeed AST2600 Real-Time Clock
    // Reference: QEMU hw/rtc/aspeed_rtc.c
    //
    // Stub peripheral: returns fixed time (2025-01-01 00:00:00).
    // Supports enable/unlock via CONTROL register.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_RTC : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_RTC()
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
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return 0;
            }

            var reg = (uint)offset / 4;

            switch(reg)
            {
                case R_COUNTER1:
                    if((storage[R_CONTROL] & RTC_ENABLED) != 0)
                    {
                        // Return a fixed time: day=1, hour=0, min=0, sec=0
                        return (1u << 24);
                    }
                    return storage[reg];

                case R_COUNTER2:
                    if((storage[R_CONTROL] & RTC_ENABLED) != 0)
                    {
                        // Return a fixed date: century=20, year=25, month=1
                        return (20u << 16) | (25u << 8) | 1u;
                    }
                    return storage[reg];

                default:
                    return storage[reg];
            }
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
                case R_COUNTER1:
                case R_COUNTER2:
                    // Only writable when unlocked
                    if((storage[R_CONTROL] & RTC_UNLOCKED) != 0)
                    {
                        storage[reg] = value;
                    }
                    return;

                case R_ALARM_STATUS:
                    // W1C
                    storage[reg] &= ~value;
                    return;

                default:
                    storage[reg] = value;
                    return;
            }
        }

        private readonly uint[] storage;

        private const uint R_COUNTER1     = 0x00 / 4;
        private const uint R_COUNTER2     = 0x04 / 4;
        private const uint R_ALARM        = 0x08 / 4;
        private const uint R_CONTROL      = 0x10 / 4;
        private const uint R_ALARM_STATUS = 0x14 / 4;

        private const uint RTC_ENABLED  = 1u << 0;
        private const uint RTC_UNLOCKED = 1u << 1;

        private const int RegisterSpaceSize = 0x18;
    }
}
