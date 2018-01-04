//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Wireless.CC2538
{
    internal enum InterruptRegister
    {
        IrqFlag0,
        IrqFlag1
    }

    // It turned out that irq registers are placed in memory in different ordering than mask registers
    internal static class InterruptRegisterHelper
    {
        public static InterruptRegister GetValueRegister(int index)
        {
            switch(index)
            {
            case 0:
                return InterruptRegister.IrqFlag1;
            case 1:
                return InterruptRegister.IrqFlag0;
            default:
                throw new ArgumentException("Expected index 0 or 1");
            }
        }

        public static InterruptRegister GetMaskRegister(int index)
        {
            switch(index)
            {
            case 0:
                return InterruptRegister.IrqFlag0;
            case 1:
                return InterruptRegister.IrqFlag1;
            default:
                throw new ArgumentException("Expected index 0 or 1");
            }
        }
    }
}
