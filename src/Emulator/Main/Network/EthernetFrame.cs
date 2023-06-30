//
// Copyright (c) 2010-2023 Antmicro
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
            return TryCreateEthernetFrame(data, addCrc ? CRCMode.Add : CRCMode.NoOperation, out frame);
        }

        public static bool TryCreateEthernetFrame(byte[] data, CRCMode crcMode, out EthernetFrame frame)
        {
            frame = null;
            switch(crcMode)
            {
                case CRCMode.NoOperation:
                    if(data.Length >= MinFrameSizeWithoutCRC)
                    {
                        frame = new EthernetFrame(data);
                        return true;
                    }
                    return false;
                case CRCMode.Add:
                case CRCMode.Replace:
                case CRCMode.Keep:
                    if(data.Length >= MinFrameSizeWithoutCRC + CRCLength)
                    {
                        var noCrcData = crcMode == CRCMode.Add ? data : data.Take(data.Length - CRCLength).ToArray();
                        var crc = (crcMode == CRCMode.Keep ? data.Skip(data.Length - CRCLength) : ComputeCRC(noCrcData)).ToArray();
                        frame = new EthernetFrame(noCrcData, crc);
                        return true;
                    }
                    return false;
                default:
                    throw new ArgumentException("Illegal value", "crcMode");
            }
        }

        public static bool CheckCRC(byte[] data)
        {
            return CompareCRC(GetCRCFromPacket(data), CalculateCRCFromPayload(data));
        }

        public void FillWithChecksums(EtherType[] supportedEtherTypes, IPProtocolType[] supportedIpProtocolTypes, bool updateEthernetCrc = true)
        {
            var packetNetIpProtocols = supportedIpProtocolTypes.Select(x => (PacketDotNet.IPProtocolType)x).ToArray();
            var packetNetEtherTypes = supportedEtherTypes.Select(x => (EthernetPacketType)x).ToArray();
            UnderlyingPacket.RecursivelyUpdateCalculatedValues(packetNetEtherTypes, packetNetIpProtocols);

            if(updateEthernetCrc)
            {
                var data = UnderlyingPacket.Bytes.ToArray();
                crc = ComputeCRC(data).ToArray();
            }
        }

        public EthernetFrame Clone()
        {
            return new EthernetFrame(UnderlyingPacket.Bytes.ToArray(), crc?.ToArray());
        }

        public override string ToString()
        {
            try
            {
                return UnderlyingPacket.ToString();
            }
            catch
            {
                return "<failed to decode frame>";
            }
        }

        public EthernetPacket UnderlyingPacket { get; }

        public byte[] Bytes
        {
            get
            {
                return (crc != null) ? UnderlyingPacket.Bytes.Concat(crc).ToArray() : UnderlyingPacket.Bytes.ToArray();
            }
        }

        public int Length
        {
            get
            {
                return UnderlyingPacket.BytesHighPerformance.Length;
            }
        }

        public MACAddress SourceMAC => (MACAddress)UnderlyingPacket.SourceHwAddress;

        public MACAddress DestinationMAC => (MACAddress)UnderlyingPacket.DestinationHwAddress;

        public IPAddress SourceIP
        {
            get
            {
                var ip = (IpPacket)UnderlyingPacket.Extract(typeof(IpPacket));
                return ip != null ? ip.SourceAddress : null;
            }
        }

        public IPAddress DestinationIP
        {
            get
            {
                var ip = (IpPacket)UnderlyingPacket.Extract(typeof(IpPacket));
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
        // 1500 byte upper layer IP packet with 14 byte frame header and 4 byte frame trailer
        public static readonly int MaximumFrameSize = 1518;
        public static readonly int RuntPacketMaximumSize = 63;

        private EthernetFrame(byte[] data, byte[] crc = null)
        {
            this.crc = crc;
            this.UnderlyingPacket = (EthernetPacket)Packet.ParsePacket(LinkLayers.Ethernet, data);
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

        private byte[] crc;
    }
}
