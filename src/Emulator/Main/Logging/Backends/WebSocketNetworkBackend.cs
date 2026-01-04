//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    public class WebSocketNetworkBackend : FormattedTextBackend
    {
        public WebSocketNetworkBackend(string endpoint, bool plainMode = true)
        {
            PlainMode = plainMode;
            webSocketServer = new WebSocketSingleConnectionServer(endpoint, true);
            webSocketServer.Start();
        }

        public override void Dispose()
        {
            lock(sync)
            {
                webSocketServer.Dispose();
                webSocketServer = null;
            }
        }

        protected override void SetColor(ConsoleColor color)
        {
            webSocketServer?.Send(GetColorControlSequence(color));
        }

        protected override void ResetColor()
        {
            webSocketServer?.Send(setDefaultsControlSequence);
        }

        protected override void WriteLine(string line)
        {
            webSocketServer?.Send(Encoding.ASCII.GetBytes(line));
            webSocketServer?.Send(newLineSequence);
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
            webSocketServer?.SendByte(value);
        }

        private WebSocketSingleConnectionServer webSocketServer;
    }
}