//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4
{
    public enum FrameType : byte
    {
        Beacon = 0x0,
        Data = 0x1,
        ACK = 0x2,
        MACControl = 0x3,
        Reserved4 = 0x4,
        Reserved5 = 0x5,
        Reserved6 = 0x6,
        Reserved7 = 0x7
    }
}

