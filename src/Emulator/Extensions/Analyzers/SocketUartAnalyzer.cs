//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AntShell.Terminal;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;

namespace Antmicro.Renode.Analyzers
{
    [Transient]
    public class SocketUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal, IDisposable
    {
        public override void AttachTo(UARTBackend backend)
        {
            var ioProvider = new IOProvider();
            ioSource = new SimpleActiveIOSource();
            ioProvider.Backend = ioSource;
            base.AttachTo(backend);
            (Backend as UARTBackend).BindAnalyzer(ioProvider);
            StartServer();
        }

        public override void Show()
        {
        }

        public override void Hide()
        {
        }

        public void Dispose()
        {
            server?.Stop();
        }

        public int? Port => server?.Port;

        public IUART UART => (Backend as UARTBackend)?.UART;

        private void StartServer()
        {
            server = new SocketServerProvider(true, false, serverName: "UartSocketTerminalServer");
            server.DataReceived += WriteToUart;
            ioSource.ByteWritten += WriteToClient;

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

        private SimpleActiveIOSource ioSource;
        private SocketServerProvider server;

        private class SimpleActiveIOSource : IActiveIOSource
        {
            public void Flush()
            {}

            public void Write(byte b)
            {
                ByteWritten?.Invoke(b);
            }

            public void InvokeByteRead(int b)
            {
                ByteRead?.Invoke(b);
            }

            public void Pause()
            {}

            public void Resume()
            {}

            public bool IsAnythingAttached => true;

            public event Action<int> ByteRead;

            public event Action<byte> ByteWritten;
        }
    }
}
