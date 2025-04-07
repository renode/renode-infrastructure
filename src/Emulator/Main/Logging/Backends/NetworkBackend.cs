//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    public class NetworkBackend : FormattedTextBackend
    {
        public NetworkBackend(int port, bool plainMode = true)
        {
            PlainMode = plainMode;
            socketServerProvider = new SocketServerProvider();
            socketServerProvider.Start(port);
        }

        public override void Dispose()
        {
            lock(sync)
            {
                socketServerProvider?.Stop();
                socketServerProvider = null;
            }
        }

        protected override void SetColor(ConsoleColor color)
        {
            socketServerProvider?.Send(GetColorControlSequence(color));
        }

        protected override void ResetColor()
        {
            socketServerProvider?.Send(setDefaultsControlSequence);
        }

        protected override void WriteLine(string line)
        {
            socketServerProvider?.Send(Encoding.ASCII.GetBytes(line));
            socketServerProvider?.Send(newLineSequence);
        }

        private static byte[] GetColorControlSequence(ConsoleColor color)
        {
            var index = (int)color;
            if(index < 0 || index >= ANSIColorControlSequence.Length)
            {
                throw new ArgumentException($"Color must be a {nameof(ConsoleColor)} value", nameof(color));
            }

            return ANSIColorControlSequence[index];
        }

        private static readonly byte[] setDefaultsControlSequence = Encoding.ASCII.GetBytes("\x1b[39;49m");
        private static readonly byte[] newLineSequence = Encoding.ASCII.GetBytes("\r\n");

        private void WriteChar(byte value)
        {
            socketServerProvider?.SendByte(value);
        }

        private SocketServerProvider socketServerProvider;
    }
}