//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

using AntShell.Terminal;

namespace Antmicro.Renode.Utilities
{
    public class WebSocketUartServer
    {
        public WebSocketUartServer()
        {
            server = new WebSocketServerProvider("/uart", true);
            server.NewConnection += OnNewConnection;
            server.Disconnected += OnDisconnected;
            server.Start();
            EmulationManager.Instance.EmulationChanged += () => server.Connections.ToList().ForEach(c => c.Dispose());
        }

        private void OnNewConnection(WebSocketConnection connection, List<string> segments)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            if(segments.Count != 2
                || !emulation.TryGetMachine(segments[0], out var machine)
                || !machine.TryGetByName<IUART>(segments[1], out var uart)
                || !emulation.BackendManager.TryGetBackendFor(uart, out var backend)
                || backend is not UARTBackend uartBackend)
            {
                Logger.Log(LogLevel.Warning, "WebSocket: No UART at /uart/{0}", string.Join("/", segments));
                connection.Dispose();
                return;
            }

            var io = new IOProvider { Backend = new ConnectionIOSource(connection) };
            bindings[connection] = (uartBackend, io);
            uartBackend.BindAnalyzer(io);
        }

        private void OnDisconnected(WebSocketConnection connection)
        {
            if(bindings.TryRemove(connection, out var binding))
            {
                binding.Backend.UnbindAnalyzer(binding.IO);
            }
        }

        private readonly WebSocketServerProvider server;
        private readonly ConcurrentDictionary<WebSocketConnection, (UARTBackend Backend, IOProvider IO)> bindings = new ConcurrentDictionary<WebSocketConnection, (UARTBackend Backend, IOProvider IO)>();

        private class ConnectionIOSource : IActiveIOSource
        {
            public ConnectionIOSource(WebSocketConnection connection)
            {
                this.connection = connection;
                connection.DataBlockReceived += bytes => Array.ForEach(bytes, b => ByteRead?.Invoke(b));
            }

            public void Write(byte b) => connection.SendByte(b);

            public void Flush()
            {
            }

            public void Pause()
            {
            }

            public void Resume()
            {
            }

            public bool IsAnythingAttached => true;

            public event Action<int> ByteRead;

            private readonly WebSocketConnection connection;
        }
    }
}
