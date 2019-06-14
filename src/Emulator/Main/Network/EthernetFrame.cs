//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Antmicro.Renode.Core.Structure;
using PacketDotNet;
using PacketDotNet.Utils;

namespace Antmicro.Renode.Network
{
    public class EthernetFrame
    {
        public static bool TryCreateEthernetFrame(byte[] data, bool addCrc, out EthernetFrame frame)
        {
            frame = null;
            if(data.Length >= MinFrameSizeWithoutCRC + (addCrc ? 0 : CRCLength))
            {
                frame = addCrc ? new EthernetFrame(data, ComputeCRC(data).ToArray()) : new EthernetFrame(data);
                return true;
            }
            return false;
        }

        public static bool CheckCRC(byte[] data)
        {
            return CompareCRC(GetCRCFromPacket(data), CalculateCRCFromPayload(data));
        }

        public void FillWithChecksums(EtherType[] supportedEtherTypes, IPProtocolType[] supportedIpProtocolTypes)
        {
            var packetNetIpProtocols = supportedIpProtocolTypes.Select(x => (PacketDotNet.IPProtocolType)x).ToArray();
            var packetNetEtherTypes = supportedEtherTypes.Select(x => (EthernetPacketType)x).ToArray();
            packet.RecursivelyUpdateCalculatedValues(packetNetEtherTypes, packetNetIpProtocols);
        }

        public EthernetFrame Clone()
        {
            return new EthernetFrame(packet.Bytes.ToArray(), crc?.ToArray());
        }

        public override string ToString()
        {
            return packet.ToString();
        }

        public byte[] Bytes
        {
            get
            {
                    return (crc != null) ? packet.Bytes.Concat(crc).ToArray() : packet.Bytes.ToArray();
            }
        }

        public int Length
        {
            get
            {
                return packet.BytesHighPerformance.Length;
            }
        }

        public MACAddress? SourceMAC
        {
            get
            {
                var ether = (EthernetPacket)packet.Extract(typeof(EthernetPacket));
                return ether != null ? (MACAddress?)ether.SourceHwAddress : null;
            }
        }

        public MACAddress? DestinationMAC
        {
            get
            {
                var ether = (EthernetPacket)packet.Extract(typeof(EthernetPacket));
                return ether != null ? (MACAddress?)ether.DestinationHwAddress : null;
            }
        }

        public IPAddress SourceIP
        {
            get
            {
                var ip = (IpPacket)packet.Extract(typeof(IpPacket));
                return ip != null ? ip.SourceAddress : null;
            }
        }

        public IPAddress DestinationIP
        {
            get
            {
                var ip = (IpPacket)packet.Extract(typeof(IpPacket));
                return ip != null ? ip.DestinationAddress : null;
            }
        }

        // note: the length 18 covers only:
        // * mac destination (6)
        // * mac source (6)
        // * 802.1Q tag (4)
        // * ether type or length (2)
        // and is chosen so that Packet .NET doesn't crash
        // when parsing the packet;
        // according to the ethernet specs the packet must
        // be at least 64 bits long, but since not all
        // ethernet models in Renode support automatic
        // padding the selected value is a compromise
        public static int MinFrameSizeWithoutCRC = 18;
        public static int CRCLength = 4;

        private EthernetFrame(byte[] data, byte[] crc = null)
        {
            this.crc = crc;
            packet = Packet.ParsePacket(LinkLayers.Ethernet, data);
        }

        private static IEnumerable<byte> ComputeCRC(byte[] data, int? lenght = null)
        {
            var computedCRC = lenght.HasValue? Crc32.Compute(data, 0, lenght.Value) : Crc32.Compute(data);
            var result = BitConverter.GetBytes(computedCRC);
            return result.Reverse();
        }

        private static IEnumerable<byte> CalculateCRCFromPayload(byte[] data)
        {
            return ComputeCRC(data, data.Length - CRCLength);
        }

        private static IEnumerable<byte> GetCRCFromPacket(byte[] data)
        {
            return data.Skip(data.Length - CRCLength);
        }

        private static bool CompareCRC(IEnumerable<byte> receivedCrc, IEnumerable<byte> computedCrc)
        {
            return receivedCrc.SequenceEqual(computedCrc);
        }

        private readonly Packet packet;
        private byte[] crc;
    }
}
