//
// Copyright (c) 2010-2022 Antmicro
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

        public static PacketData ErrorReply(Error err = Error.Unknown)
        {
            return new PacketData(string.Format("E{0}", (int)err));
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
            return new PacketData(string.Format("T05{0}:{1};thread:{2:X2};", reason.GetStopReason(),
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
                Mnemonic = null;
                return true;
            }
            return false;
        }

        public string GetDataAsStringLimited()
        {
            var cs = cachedString;
            if(cs == null)
            {
                var counter = 0;
                // take only ASCII characters and truncate the size to fit nicely in the log
                return Encoding.ASCII.GetString(bytes.TakeWhile(b => b >= 0x20 && b <= 0x7e && counter++ < DataAsStringLimit).ToArray());
            }
            else
            {
                return cs.Substring(0, Math.Min(cs.Length, DataAsStringLimit));
            }
        }

        public string MatchMnemonicFromList(List<string> mnemonicList)
        {
            // Start from the longest command to properly distinguish between commands that
            // have a similar start eg. qC and qCRC
            foreach(var entry in mnemonicList.OrderByDescending(x => x.Length))
            {
                if(DataAsString.StartsWith(entry))
                {
                    Mnemonic = entry;
                    break;
                }
            }
            return Mnemonic;
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
                    // take only intial ASCII characters and truncate at the first binary byte;
                    // it's ok, since `DataAsString` is used to parse the beginning of a command
                    // (which is always a string) and all potential binary arguments are handled separately
                    cs = Encoding.ASCII.GetString(bytes.TakeWhile(b => b >= 0x20 && b <= 0x7e).ToArray());
                    cachedString = cs;
                }
                return cs;
            }
        }

        public string Mnemonic { get; private set; }

        private const byte EscapeOffset = 0x20;
        private const byte EscapeSymbol = (byte)'}';
        private const int DataAsStringLimit = 100;

        private string cachedString;
        private bool escapeNextByte;
        private readonly List<byte> rawBytes;
        private readonly List<byte> bytes;
    }

    public enum Error : int
    {
        OperationNotPermitted = 1,
        NoSuchFileOrDirectory = 2,
        InterruptedSystemCall = 4,
        BadFileNumber         = 9,
        PermissionDenied      = 13,
        BadAddress            = 14,
        DeviceOrResourceBusy  = 16,
        FileExists            = 17,
        NoSuchDevice          = 19,
        NotADirectory         = 20,
        IsADirectory          = 21,
        InvalidArgument       = 22,
        FileTableOverflow     = 23,
        TooManyOpenFiles      = 24,
        FileTooLarge          = 27,
        NoSpaceLeftOnDevice   = 28,
        IllegalSeek           = 29,
        ReadOnlyFileSystem    = 30,
        NameTooLong           = 91,
        Unknown               = 9999
    }
}
