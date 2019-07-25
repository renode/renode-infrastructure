//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Text;

namespace Antmicro.Renode.Utilities.GDB
{
    public class Packet
    {
        public static bool TryCreate(PacketData data, byte checksum, out Packet p)
        {
            p = new Packet(data);
            return p.CalculateChecksum() == checksum;
        }

        public Packet(PacketData data)
        {
            Data = data;
        }

        public byte[] GetCompletePacket()
        {
            return Encoding.ASCII.GetBytes(string.Format("${0}#{1:x2}", Data.DataAsString, CalculateChecksum()));
        }

        public byte CalculateChecksum()
        {
            uint result = 0;
            foreach(var b in Data.RawDataAsBinary)
            {
                result += b;
            }
            return (byte)(result % 256);
        }

        public PacketData Data { get; private set; }
    }
}

