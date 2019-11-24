//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4
{
    public class PHYHeader802154
    {
        // Definitions taken from: http://www.ti.com/lit/ug/swru346b/swru346b.pdf (page 46 and page 55)
        public PHYHeader802154(byte byteA, byte byteB, PHYType type)
        {
            this.byteA = byteA;
            this.byteB = byteB;
            if(type == PHYType.Header802154)
            {
                Length = byteA;
                Address = byteB; // This is TI CC1200 specific byte, normally 802.15.4 has just Length
            }
            else
            {
                Length = byteB + ((byteA & 0x7u) << 8);
                DataWhitening = BitHelper.IsBitSet(byteA, 3);
                FCS2Byte = BitHelper.IsBitSet(byteA, 4);
                ModeSwitch = BitHelper.IsBitSet(byteA, 7);
            }
        }

        // 802.15.4 Packet [Length, Address]
        public uint Address { get; }

        // 802.15.4g Packet [PHRA, PHRB]
        public uint Length { get; }
        public bool DataWhitening { get; }
        public bool FCS2Byte { get; }
        public bool ModeSwitch { get; }

        private byte byteA;
        private byte byteB;

        // PHY Header Type
        public enum PHYType
        {
            Header802154,
            Header802154g,
        };
    }
}
