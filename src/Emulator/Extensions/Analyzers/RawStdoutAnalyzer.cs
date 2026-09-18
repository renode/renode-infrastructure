//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;

using AntShell.Terminal;

namespace Antmicro.Renode.Analyzers
{
    [Transient]
    public class RawStdoutAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal
    {
        public RawStdoutAnalyzer()
        {
            ioProvider = new IOProvider();
            ioSource = new SimpleActiveIOSource();
            ioProvider.Backend = ioSource;
            ioSource.ByteWritten += ComposeAndPrintLine;
        }

        public override void AttachTo(UARTBackend backend)
        {
            base.AttachTo(backend);
            (Backend as UARTBackend).BindAnalyzer(ioProvider);
        }

        public override void Show()
        {
        }

        public override void Hide()
        {
        }

        public override void Clear()
        {
        }

        public void Dispose()
        {
            (Backend as UARTBackend)?.UnbindAnalyzer(ioProvider);
            FlushBuffer();
        }

        public IUART UART => (Backend as UARTBackend)?.UART;

        private void ComposeAndPrintLine(byte b)
        {
            char c = (char)b;

            lock(lineBuffer)
            {
                lineBuffer.Append(c);
            }
            if(c == '\n')
            {
                FlushBuffer();
            }
        }

        private void FlushBuffer()
        {
            lock(lineBuffer)
            {
                if(lineBuffer.Length > 0)
                {
                    Console.Write(lineBuffer.ToString());
                    lineBuffer.Clear();
                }
            }
        }

        private readonly StringBuilder lineBuffer = new StringBuilder();
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