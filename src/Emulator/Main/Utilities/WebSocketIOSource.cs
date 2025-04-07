//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Terminal;

namespace Antmicro.Renode.Utilities
{
    public class WebSocketIOSource : IActiveIOSource
    {
        public WebSocketIOSource(string endpoint)
        {
            server = new WebSocketSingleConnectionServer(endpoint, true);
            server.DataReceived += (sender, b) =>
            {
                ByteRead(b);
            };

            server.Start();
        }

        public void Dispose()
        {
            server.Dispose();
        }

        public void Flush()
        {
        }

        public void Pause()
        {
            // Required by IActiveIOSource interface
        }

        public void Resume()
        {
            // Required by IActiveIOSource interface
        }

        public void Write(byte b)
        {
            server.SendByte(b);
        }

        public bool IsAnythingAttached { get { return server.IsAnythingReceiving; } }

        public event System.Action<int> ByteRead;

        private readonly WebSocketSingleConnectionServer server;
    }
}