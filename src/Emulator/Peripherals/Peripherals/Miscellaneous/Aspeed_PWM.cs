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
    // Aspeed AST2600 PWM/Fan Tachometer Controller
    // Reference: QEMU hw/misc/aspeed_pwm.c
    //
    // Stub peripheral: simple R/W register storage.
    // No actual PWM output or tachometer input.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_PWM : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_PWM()
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
            if(offset >= 0 && offset < RegisterSpaceSize)
            {
                storage[(uint)offset / 4] = value;
            }
        }

        private readonly uint[] storage;
        private const int RegisterSpaceSize = 0x1000;
    }
}
