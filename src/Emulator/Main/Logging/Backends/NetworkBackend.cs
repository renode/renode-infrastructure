//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    public class NetworkBackend : TextBackend
    {
        public NetworkBackend(int port)
        {
            sync = new object();
            socketServerProvider.Start(port);
        }

        public override void Log(LogEntry entry)
        {
            if(!ShouldBeLogged(entry))
            {
                return;
            }

            lock (sync)
            {
                if(isDisposed)
                {
                    return;
                }

                var type = entry.Type;
                var message = FormatLogEntry(entry);
                WriteString(string.Format("{0:HH:mm:ss.ffff} [{1}] {2}\r\n", CustomDateTime.Now, type, message));
            }
        }

        public void WriteString(string value)
        {
            foreach(char c in value)
            {
                WriteChar((byte)c);
            }
        }

        public void WriteChar(byte value)
        {
            socketServerProvider.SendByte(value);
        }

        public override void Dispose()
        {
            lock(sync)
            {
                if(isDisposed)
                {
                    return;
                }
                socketServerProvider.Stop();
                isDisposed = true;
            }
        }

        private readonly object sync;
        private bool isDisposed;
        private readonly SocketServerProvider socketServerProvider = new SocketServerProvider();
    }
}


