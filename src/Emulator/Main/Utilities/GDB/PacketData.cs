//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Peripherals.CPU;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Antmicro.Renode.Utilities.GDB
{
    public class PacketData
    {
        static PacketData()
        {
            Success = new PacketData("OK");
            Empty = new PacketData(string.Empty);
        }

        public static PacketData ErrorReply(int errNo)
        {
            return new PacketData(string.Format("E{0:X2}", errNo));
        }

        public static PacketData AbortReply(int signal)
        {
            return new PacketData(string.Format("X{0:X2}", signal));
        }

        public static PacketData StopReply(int signal)
        {
            return new PacketData(string.Format("S{0:X2}", signal));
        }

        public static PacketData StopReply(int signal, uint id)
        {
            return new PacketData(string.Format("T{0:X2}thread:{1:X2};", signal, id));
        }

        public static PacketData StopReply(BreakpointType reason, ulong? address)
        {
            return new PacketData(string.Format("T05{0}:{1};", reason.GetStopReason(),
                                                !address.HasValue ? string.Empty : string.Format("{0:X2}", address)));
        }

        public static PacketData StopReply(BreakpointType reason, uint cpuId, ulong? address)
        {
            return new PacketData(string.Format("T05{0}:{1};thread:{2};", reason.GetStopReason(),
                                                !address.HasValue ? string.Empty : string.Format("{0:X2}", address), cpuId));
        }

        public PacketData(string data)
        {
            cachedString = data;
            rawBytes = bytes = new List<byte>(Encoding.UTF8.GetBytes(data));
            RawDataAsBinary = DataAsBinary = new ReadOnlyCollection<byte>(rawBytes);
        }

        public PacketData()
        {
            RawDataAsBinary = new ReadOnlyCollection<byte>(rawBytes = new List<byte>());
            DataAsBinary = new ReadOnlyCollection<byte>(bytes = new List<byte>());
        }

        public bool AddByte(byte b)
        {
            rawBytes.Add(b);
            if(escapeNextByte)
            {
                bytes.Add((byte)(b ^ EscapeOffset));
                escapeNextByte = false;
            }
            else if(b == EscapeSymbol)
            {
                escapeNextByte = true;
            }
            else
            {
                bytes.Add(b);
            }
            if(!escapeNextByte)
            {
                cachedString = null;
                return true;
            }
            return false;
        }

        public static PacketData Success { get; private set; }
        public static PacketData Empty { get; private set; }

        public IEnumerable<byte> RawDataAsBinary { get; private set; }
        public IEnumerable<byte> DataAsBinary { get; private set; }
        public string DataAsString
        {
            get
            {
                var cs = cachedString;
                if(cs == null)
                {
                    var counter = 0;
                    // This code effectively prevents us from printing out non-printable characters in logs, and limits the output.
                    // It is definitely a hack, but works well enough for our needs and prevents the console from crashing when
                    // loading binaries via GDB.
                    cs = Encoding.ASCII.GetString(bytes.TakeWhile(b => b >= 32 && b <= 127 && counter++ < 100).ToArray());
                    cachedString = cs;
                }
                return cs;
            }
        }

        private const byte EscapeOffset = 0x20;
        private const byte EscapeSymbol = (byte)'}';

        private string cachedString;
        private bool escapeNextByte;
        private readonly List<byte> rawBytes;
        private readonly List<byte> bytes;
    }
}

