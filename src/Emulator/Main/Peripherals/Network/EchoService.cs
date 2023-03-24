//
// Copyright (c) 2010 - 2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Network
{
    public class EchoService : IEmulatedNetworkService
    {
        public EchoService(string host, ushort port, string args)
        {
            Host = host;
            Port = port;
        }

        public void Dispose()
        {
            // intentionally left blank
        }

        public void Disconnect()
        {
            responseBuffer.Clear();
        }

        public byte[] Receive(int bytes)
        {
            var data = responseBuffer.Take(bytes).ToArray();
            responseBuffer.RemoveRange(0, Math.Min(bytes, responseBuffer.Count));
            Logger.Log(LogLevel.Info, "Echo: Sending {0} bytes: '{1}' to modem, {2} bytes left after this",
                bytes, Encoding.ASCII.GetString(data), BytesAvailable);
            return data.ToArray();
        }

        public bool Send(byte[] data)
        {
            Logger.Log(LogLevel.Info, "Echo: Received packet of size {0}: '{1}'",
                data.Length, Encoding.ASCII.GetString(data));
            // Add the received data to the response buffer and notify the modem about the received bytes
            // being available for reading
            responseBuffer.AddRange(data);
            BytesReceived?.Invoke(data.Length);
            return true;
        }

        public int BytesAvailable => responseBuffer.Count;

        public string Host { get; }
        public ushort Port { get; }

        public event Action<int> BytesReceived;

        private readonly List<byte> responseBuffer = new List<byte>();
    }
}
