//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4
{
    public enum AddressingMode : byte
    {
        None = 0x0,
        Reserved = 0x1,
        // 2 bytes PAN id, 2 bytes address
        ShortAddress = 0x2,
        // 2 bytes PAN, 8 bytes address
        ExtendedAddress = 0x3
    }

    public static class AddressingModeExtensions
    {
        public static int GetBytesLength(this AddressingMode mode)
        {
            switch(mode)
            {
                case AddressingMode.None:
                    return 0;
                case AddressingMode.ShortAddress:
                    return 2;
                case AddressingMode.ExtendedAddress:
                    return 8;
                default:
                    throw new ArgumentException();
            }
        }
    }
}

