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
            if(index < 0 || index >= colorControlSequence.Length)
            {
                throw new ArgumentException($"Color must be a {nameof(ConsoleColor)} value", nameof(color));
            }

            return colorControlSequence[index];
        }

        private static readonly byte[][] colorControlSequence = {
            Encoding.ASCII.GetBytes("\x1b[30m"), // ConsoleColor.Black
            Encoding.ASCII.GetBytes("\x1b[34m"), // ConsoleColor.DarkBlue
            Encoding.ASCII.GetBytes("\x1b[32m"), // ConsoleColor.DarkGreen
            Encoding.ASCII.GetBytes("\x1b[36m"), // ConsoleColor.DarkCyan
            Encoding.ASCII.GetBytes("\x1b[31m"), // ConsoleColor.DarkRed
            Encoding.ASCII.GetBytes("\x1b[35m"), // ConsoleColor.DarkMagenta
            Encoding.ASCII.GetBytes("\x1b[33m"), // ConsoleColor.DarkYellow
            Encoding.ASCII.GetBytes("\x1b[37m"), // ConsoleColor.Gray
            Encoding.ASCII.GetBytes("\x1b[90m"), // ConsoleColor.DarkGray
            Encoding.ASCII.GetBytes("\x1b[94m"), // ConsoleColor.Blue
            Encoding.ASCII.GetBytes("\x1b[92m"), // ConsoleColor.Green
            Encoding.ASCII.GetBytes("\x1b[96m"), // ConsoleColor.Cyan
            Encoding.ASCII.GetBytes("\x1b[91m"), // ConsoleColor.Red
            Encoding.ASCII.GetBytes("\x1b[95m"), // ConsoleColor.Magenta
            Encoding.ASCII.GetBytes("\x1b[93m"), // ConsoleColor.Yellow
            Encoding.ASCII.GetBytes("\x1b[97m"), // ConsoleColor.White
        };

        private static readonly byte[] setDefaultsControlSequence = Encoding.ASCII.GetBytes("\x1b[39;49m");
        private static readonly byte[] newLineSequence = Encoding.ASCII.GetBytes("\r\n");

        private void WriteChar(byte value)
        {
            socketServerProvider?.SendByte(value);
        }

        private SocketServerProvider socketServerProvider;
    }
}
