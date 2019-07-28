//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Collections.Concurrent;
using System.Globalization;
using Antmicro.Renode.Core;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Testing
{
    public static class NetworkInterfaceTesterExtensions
    {
        public static void CreateNetworkInterfaceTester(this Emulation emulation, string name, IMACInterface iface)
        {
            emulation.ExternalsManager.AddExternal(new NetworkInterfaceTester(iface), name);
        }

        public static void CreateNetworkInterfaceTester(this Emulation emulation, string name, IRadio iface)
        {
            emulation.ExternalsManager.AddExternal(new NetworkInterfaceTester(iface), name);
        }
    }

    public class NetworkInterfaceTester : IExternal, IDisposable
    {
        public NetworkInterfaceTester(IMACInterface iface)
        {
            this.iface = iface;
            iface.FrameReady += HandleFrame;
        }

        public NetworkInterfaceTester(IRadio iface)
        {
            this.iface = iface;
            iface.FrameSent += HandleFrame;
        }

        public bool TryWaitForOutgoingPacket(int timeoutInSeconds, out NetworkInterfaceTesterResult result)
        {
            return frames.TryTake(out result, timeoutInSeconds * 1000);
        }

        public bool TryWaitForOutgoingPacketWithBytesAtIndex(string bytes, int index, int maxPackets, int singleTimeout, out NetworkInterfaceTesterResult result)
        {
            int packetsChecked = 0;

            if(bytes.Length % 2 != 0)
            {
                throw new ArgumentException("Partial bytes specified in the search pattern.");
            }

            while(packetsChecked++ < maxPackets)
            {
                if(!TryWaitForOutgoingPacket(singleTimeout, out var currentPacket))
                {
                    break;
                }

                if(index + (bytes.Length / 2) > currentPacket.bytes.Length)
                {
                    continue;
                }

                var matches = true;

                for(uint i = 0; i < bytes.Length; i += 2)
                {
                    var currentByte = currentPacket.bytes[index + (i / 2)];

                    if(!IsByteEqual(bytes, i, currentByte))
                    {
                        matches = false;
                        break;
                    }
                }

                if(matches)
                {
                    result = currentPacket;
                    return true;
                }
            }

            result = new NetworkInterfaceTesterResult();
            return false;
        }

        public void Dispose()
        {
            if(iface is IMACInterface mac)
            {
                mac.FrameReady -= HandleFrame;
            }
            if(iface is IRadio radio)
            {
                radio.FrameSent -= HandleFrame;
            }
        }

        private void HandleFrame(IRadio radio, byte[] frame)
        {
            HandleFrameInner(frame);
        }

        private void HandleFrame(EthernetFrame frame)
        {
            HandleFrameInner(frame.Bytes);
        }

        private void HandleFrameInner(byte[] bytes)
        {
            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }

            frames.Add(new NetworkInterfaceTesterResult(bytes, vts.TimeElapsed.TotalMilliseconds));
        }

        private byte HexCharToByte(char c)
        {
            c = Char.ToLower(c);

            if(c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }

            if(c >= 'a' && c <= 'f')
            {
                return (byte)(c - '0' - ('a' - '9') + 1);
            }

            throw new ArgumentException(string.Format("{0} is not a valid hex number.", c));
        }

        private bool IsNibbleEqual(string match, uint index, byte data)
        {
            if(index >= match.Length)
            {
                return false;
            }

            var c = match[(int)index];

            // Treat underscore as a wildcard
            if(c == '_')
            {
                return true;
            }

            return data == HexCharToByte(c);
        }

        private bool IsByteEqual(string input, uint index, byte data)
        {
            // check index + 1 as we need to check both nibbles
            if(index + 1 >= input.Length)
            {
                return false;
            }

            return IsNibbleEqual(input, index, (byte)((data & 0xF0) >> 4)) && IsNibbleEqual(input, index + 1, (byte)(data & 0x0F));
        }

        private readonly INetworkInterface iface;
        private readonly BlockingCollection<NetworkInterfaceTesterResult> frames = new BlockingCollection<NetworkInterfaceTesterResult>();
    }

    public struct NetworkInterfaceTesterResult
    {
        public NetworkInterfaceTesterResult(byte[] bytes, double timestamp)
        {
            this.bytes = bytes;
            this.timestamp = timestamp;
        }

        public byte[] bytes;
        public double timestamp;
    }
}
