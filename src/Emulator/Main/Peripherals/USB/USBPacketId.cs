//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Core.USB
{
    public enum USBPacketId
    {
        OutToken = 0x1,
        InToken = 0x9,
        SofToken = 0x5,
        SetupToken = 0xD,

        Data0 = 0x3,
        Data1 = 0xB,
        Data2 = 0x7,
        MData = 0xF,

        AckHandshake = 0x2,
        NakHandshake = 0xA,
        StallHandshake = 0xE,
        NoResponseYet = 0x6,

        Preamble_Error = 0xC,
        Split = 0x8,
        Ping = 0x4,
        Reserved = 0x0
    }
}

