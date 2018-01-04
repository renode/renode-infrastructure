//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Terminal;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Utilities
{
    public class SocketIOSource : IActiveIOSource
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

        public void Write(byte b)
        {
            server.SendByte(b);
        }

        public event System.Action<int> ByteRead
        {
            add { server.DataReceived += value; }
            remove { server.DataReceived -= value; }
        }

        public bool IsAnythingAttached { get { return server.IsAnythingReceiving; } }

        private readonly SocketServerProvider server;
    }
}

