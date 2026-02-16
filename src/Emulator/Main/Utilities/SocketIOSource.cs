//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Helpers;
using AntShell.Terminal;

namespace Antmicro.Renode.Utilities
{
    public class SocketIOSource : IActiveIOSource, ISizeSource
    {
        public SocketIOSource(int port)
        {
            server = new SocketServerProvider();
            server.Start(port);
        }

        public void Dispose()
        {
            server.Stop();
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

        public Position Size => server.TerminalSize;

        public event System.Action<int> ByteRead
        {
            add { server.DataReceived += value; }
            remove { server.DataReceived -= value; }
        }

        public event System.Action Resized
        {
            add { server.TerminalResized += value; }
            remove { server.TerminalResized -= value; }
        }

        public bool IsAnythingAttached { get { return server.IsAnythingReceiving; } }

        private readonly SocketServerProvider server;
    }
}