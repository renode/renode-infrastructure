//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Backends.Terminals
{
    public static class ServerSocketTerminalExtensions
    {
        public static void CreateServerSocketTerminal(this Emulation emulation, int port, string name, bool emitConfig = true)
        {
            emulation.ExternalsManager.AddExternal(new ServerSocketTerminal(port, emitConfig), name);
        }
    }

    public class ServerSocketTerminal : BackendTerminal, IDisposable
    {
        public ServerSocketTerminal(int port, bool emitConfigBytes = true)
        {
            server = new SocketServerProvider(emitConfigBytes);
            server.DataReceived += b => CallCharReceived((byte)b);

            server.Start(port);
        }

        public override void WriteChar(byte value)
        {
            server.SendByte(value);
        }

        public void Dispose()
        {
            server.Stop();
        }

        private readonly SocketServerProvider server;
    }
}

