//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public enum MessageRecipient
    {
        Device = 0,
        Interface = 1,
        Endpoint = 2,
        Other = 3
    }

    public struct USBPacket
    {
        public byte Ep;
        public byte [] Data;
        public long BytesToTransfer;
    }
}