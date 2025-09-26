//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;

using AntShell.Terminal;

namespace Antmicro.Renode.Analyzers
{
    [Transient]
    public class WebSocketUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal, IDisposable
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
            server.Dispose();
        }

        public int GetUartNumber()
        {
            return uartNumber;
        }

        public IUART UART => (Backend as UARTBackend)?.UART;

        private static int UartCount = 0;

        private void StartServer()
        {
            IMachine machine = UART.GetMachine();
            uartNumber = Interlocked.Increment(ref UartCount);
            server = new WebSocketSingleConnectionServer($"/telnet/{uartNumber}", true);
            server.DataReceived += WriteToUart;
            ioSource.ByteWritten += WriteToClient;
            server.Start();
        }

        private void WriteToClient(byte b)
        {
            server.SendByte(b);
        }

        private void WriteToUart(WebSocketConnection sender, int c)
        {
            ioSource.InvokeByteRead(c);
        }

        private int uartNumber;
        private SimpleActiveIOSource ioSource;
        private WebSocketSingleConnectionServer server;

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