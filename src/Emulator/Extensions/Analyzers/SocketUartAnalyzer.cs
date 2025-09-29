//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;

using AntShell.Terminal;

namespace Antmicro.Renode.Analyzers
{
    [Transient]
    public class SocketUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal, IDisposable, IDisconnectableState
    {
        public SocketUartAnalyzer()
        {
            ioProvider = new IOProvider();
            ioSource = new SimpleActiveIOSource();
            ioProvider.Backend = ioSource;
            ioSource.ByteWritten += WriteToClient;
        }

        public override void AttachTo(UARTBackend backend)
        {
            base.AttachTo(backend);
            (Backend as UARTBackend).BindAnalyzer(ioProvider);
            if(server != null)
            {
                this.Log(LogLevel.Info, "Reopened socket UART terminal on port {0}", Port);
                return;
            }
            StartServer();
        }

        public override void Show()
        {
        }

        public override void Hide()
        {
        }

        public override void Clear()
        {
            server.Send(Encoding.ASCII.GetBytes("\x1b[2J\x1b[H"));
        }

        public void Dispose()
        {
            server?.Stop();
        }

        public void DisconnectState()
        {
            (Backend as UARTBackend).UnbindAnalyzer(ioProvider);
        }

        public int? Port => server?.Port;

        public IUART UART => (Backend as UARTBackend)?.UART;

        private void StartServer()
        {
            server = new SocketServerProvider(true, false, serverName: "UartSocketTerminalServer");
            server.DataReceived += WriteToUart;
            server.ConnectionAccepted += _ => Clear();

            server.Start(0);
            this.Log(LogLevel.Info, "Opened socket UART terminal on port {0}", Port);
        }

        private void WriteToClient(byte b)
        {
            server.SendByte(b);
        }

        private void WriteToUart(int c)
        {
            ioSource.InvokeByteRead(c);
        }

        private SocketServerProvider server;

        private readonly SimpleActiveIOSource ioSource;
        private readonly IOProvider ioProvider;

        private class SimpleActiveIOSource : IActiveIOSource
        {
            public void Flush()
            { }

            public void Write(byte b)
            {
                ByteWritten?.Invoke(b);
            }

            public void InvokeByteRead(int b)
            {
                ByteRead?.Invoke(b);
            }

            public void Pause()
            { }

            public void Resume()
            { }

            public bool IsAnythingAttached => true;

            public event Action<int> ByteRead;

            public event Action<byte> ByteWritten;
        }
    }
}