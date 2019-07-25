//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

namespace Antmicro.Renode.Utilities.GDB
{
    public class PacketBuilder
    {
        public PacketBuilder()
        {
            checksum = new byte[2];
        }

        public Result AppendByte(byte b)
        {
            switch(state)
            {
            case State.NoPacket:
                switch(b)
                {
                case InterruptSymbol:
                    return new Result(interrupt: true);
                case PacketStartSymbol:
                    state = State.Data;
                    packetData = new PacketData();
                    break;
                }
                break;
            case State.Data:
                if(!escape && b == PacketStopSymbol)
                {
                    state = State.Checksum1;
                    break;
                }
                escape = !packetData.AddByte(b);
                break;
            case State.Checksum1:
                checksum[0] = b;
                state = State.Checksum2;
                break;
            case State.Checksum2:
                checksum[1] = b;
                state = State.NoPacket;

                var checksumValue = (byte)Convert.ToUInt32(Encoding.ASCII.GetString(checksum), 16);
                Packet packet;
                if(!Packet.TryCreate(packetData, checksumValue, out packet))
                {
                    return new Result(packet, corruptedPacket: true);
                }
                return new Result(packet);
            }
            return null;
        }

        private bool escape;
        private State state;
        private PacketData packetData;

        private readonly byte[] checksum;

        private const byte PacketStartSymbol = (byte)'$';
        private const byte PacketStopSymbol = (byte)'#';
        private const byte InterruptSymbol = 0x03;

        private enum State
        {
            NoPacket,
            Data,
            Checksum1,
            Checksum2
        }

        public class Result
        {
            public Result(Packet packet = null, bool corruptedPacket = false, bool interrupt = false)
            {
                Packet = packet;
                Interrupt = interrupt;
                CorruptedPacket = corruptedPacket;
            }

            public Packet Packet { get; private set; }
            public bool Interrupt { get; private set; }
            public bool CorruptedPacket { get; private set; }
        }
    }
}

