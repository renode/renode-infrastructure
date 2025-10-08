//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public struct USBSetupPacket
    {
        public byte RequestType;
        public byte Request;
        public UInt16 Value;
        public UInt16 Index;
        public UInt16 Length;
    }
}