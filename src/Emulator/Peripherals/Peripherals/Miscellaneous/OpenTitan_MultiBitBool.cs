//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // All below values come from https://github.com/lowRISC/opentitan/blob/master/sw/device/lib/base/multibits.h
    public enum MultiBitBool4
    {
        True = 0x6,
        False = 0x9,
    }

    public enum MultiBitBool8
    {
        True = 0x96,
        False = 0x69,
    }

    public enum MultiBitBool12
    {
        True = 0x696,
        False = 0x969,
    }

    public enum MultiBitBool16
    {
        True = 0x9696,
        False = 0x6969,
    }
}
