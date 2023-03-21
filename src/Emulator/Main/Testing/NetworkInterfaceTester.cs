//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Globalization;
using Antmicro.Renode.Core;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

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
            this.iface = iface as IPeripheral;
            if(this.iface == null)
            {
                throw new ConstructionException("This tester can only be attached to an IPeripheral");
            }

            iface.FrameReady += HandleFrame;
            newFrameEvent = new AutoResetEvent(false);
        }

        public NetworkInterfaceTester(IRadio iface)
        {
            this.iface = iface as IPeripheral;
            if(this.iface == null)
            {
                throw new ConstructionException("This tester can only be attached to an IPeripheral");
            }

            iface.FrameSent += HandleFrame;
            newFrameEvent = new AutoResetEvent(false);
        }

        public bool TryWaitForOutgoingPacket(float timeout, out NetworkInterfaceTesterResult result)
        {
            var machine = iface.GetMachine();
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)(1000 * timeout));

            do
            {
                if(frames.TryTake(out result))
                {
                    return true;
                }

                WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, newFrameEvent });
            }
            while(!timeoutEvent.IsTriggered);

            result = default(NetworkInterfaceTesterResult);
            return false;
        }

        public bool TryWaitForOutgoingPacketWithBytesAtIndex(string bytes, int index, int maxPackets, float timeout, out NetworkInterfaceTesterResult result)
        {
            if(bytes.Length % 2 != 0)
            {
                throw new ArgumentException("Partial bytes specified in the search pattern.");
            }

            int packetsChecked = 0;

            var machine = iface.GetMachine();
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)(1000 * timeout));

            do
            {
                while(packetsChecked < maxPackets && frames.TryTake(out var frame))
                {
                    packetsChecked++;
                    if(IsMatch(bytes, index, frame.bytes))
                    {
                        result = frame;
                        return true;
                    }
                }

                WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, newFrameEvent });
            }
            while(!timeoutEvent.IsTriggered && packetsChecked < maxPackets);

            result = new NetworkInterfaceTesterResult();
            return false;
        }

        public void SendFrame(string bytes)
        {
            var data = HexStringToBytes(bytes);
            if(iface is IMACInterface macIface)
            {
                if(!EthernetFrame.TryCreateEthernetFrame(data, false, out var frame))
                {
                    throw new ArgumentException("Couldn't create Ethernet frame.");
                }
                var vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
                iface.GetMachine().HandleTimeDomainEvent(macIface.ReceiveFrame, frame, vts);
            }
            else if(iface is IRadio)
            {
                throw new NotImplementedException("Sending frames is not implemented for Radio interfaces.");
            }
            else
            {
                throw new NotImplementedException("Sending frames is not implemented for this peripheral.");
            }
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

        private bool IsMatch(string pattern, int index, byte[] packet)
        {
            if(index + (pattern.Length / 2) > packet.Length)
            {
                return false;
            }

            for(var i = 0; i < pattern.Length; i += 2)
            {
                var currentByte = packet[index + (i / 2)];

                if(!IsByteEqual(pattern, i, currentByte))
                {
                    return false;
                }
            }

            return true;
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
            TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts);
            frames.Add(new NetworkInterfaceTesterResult(bytes, vts.TimeElapsed.TotalMilliseconds));
            newFrameEvent.Set();
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

        private bool IsNibbleEqual(string match, int index, byte data)
        {
            if(index >= match.Length)
            {
                return false;
            }

            var c = match[index];

            // Treat underscore as a wildcard
            if(c == '_')
            {
                return true;
            }

            return data == HexCharToByte(c);
        }

        private bool IsByteEqual(string input, int index, byte data)
        {
            // check index + 1 as we need to check both nibbles
            if(index + 1 >= input.Length)
            {
                return false;
            }

            return IsNibbleEqual(input, index, (byte)((data & 0xF0) >> 4)) && IsNibbleEqual(input, index + 1, (byte)(data & 0x0F));
        }

        private byte[] HexStringToBytes(string data)
        {
            if(data.Length % 2 == 1)
            {
                data = "0" + data;
            }

            var bytes = new byte[data.Length / 2];
            for(var i = 0; i < bytes.Length; ++i)
            {
                if(!Byte.TryParse(data.Substring(i * 2, 2), NumberStyles.HexNumber, null, out bytes[i]))
                {
                    throw new ArgumentException($"Data not in hex format at index {i * 2} (\"{data.Substring(i * 2, 2)}\")");
                }
            }

            return bytes;
        }

        [Antmicro.Migrant.Constructor(false)]
        private readonly AutoResetEvent newFrameEvent;
        private readonly IPeripheral iface;
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
