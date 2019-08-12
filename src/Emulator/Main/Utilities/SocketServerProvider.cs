//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Net.Sockets;
using Antmicro.Renode.Logging;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using Antmicro.Renode.Exceptions;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public class SocketServerProvider : IDisposable
    {
        public SocketServerProvider(bool emitConfigBytes = true)
        {
            queue = new BlockingCollection<byte>();
            this.emitConfigBytes = emitConfigBytes;
        }

        public void Start(int port)
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                server.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
            server.Listen(1);

            listenerThread = new Thread(ListenerThreadBody)
            {
                IsBackground = true,
                Name = GetType().Name
            };
            listenerThread.Start();
        }

        public void Stop()
        {
            if(server != null)
            {
                server.Close();
                server.Dispose();
            }
            socket?.Close();
            queueCancellationToken?.Cancel();
            listenerThread?.Join();
        }

        public void Dispose()
        {
            Stop();
        }

        public void SendByte(byte b)
        {
            queue.Add(b);
        }

        public void Send(IEnumerable<byte> bytes)
        {
            foreach(var b in bytes)
            {
                SendByte(b);
            }
        }

        public bool IsAnythingReceiving { get { return DataReceived != null; } }

        public event Action ConnectionClosed;
        public event Action<Stream> ConnectionAccepted;
        public event Action<int> DataReceived;

        private void WriterThreadBody(Stream stream)
        {
            try
            {
                if(emitConfigBytes)
                {
                    var initBytes = new byte[] {
                    255, 253, 000, // IAC DO    BINARY
                    255, 251, 001, // IAC WILL  ECHO
                    255, 251, 003, // IAC WILL  SUPPRESS_GO_AHEAD
                    255, 252, 034, // IAC WONT  LINEMODE
                };
                    stream.Write(initBytes, 0, initBytes.Length);
                }

                while(!queueCancellationToken.IsCancellationRequested)
                {
                    stream.WriteByte(queue.Take(queueCancellationToken.Token));
                }
            }
            catch(OperationCanceledException)
            {
            }
            catch(IOException)
            {
            }
            catch(ObjectDisposedException)
            {
            }
        }

        private void ReaderThreadBody(Stream stream)
        {
            while(true)
            {
                int value;
                try
                {
                    value = stream.ReadByte();
                }
                catch(IOException)
                {
                    value = -1;
                }

                var dataReceived = DataReceived;
                if(dataReceived != null)
                {
                    dataReceived(value);
                }

                if(value == -1)
                {
                    Logger.LogAs(this, LogLevel.Debug, "Client disconnected, stream closed.");
                    queueCancellationToken.Cancel();
                    break;
                }
            }
        }

        private void ListenerThreadBody()
        {
            NetworkStream stream;
            while(queueCancellationToken == null || !queueCancellationToken.IsCancellationRequested)
            {
                try
                {
                    socket = server.Accept();
                    stream = new NetworkStream(socket);
                }
                catch(SocketException)
                {
                    break;
                }
                catch(ObjectDisposedException)
                {
                    break;
                }

                var connectionAccepted = ConnectionAccepted;
                if(connectionAccepted != null)
                {
                    connectionAccepted(stream);
                }

                queueCancellationToken = new CancellationTokenSource();
                writerThread = new Thread(() => WriterThreadBody(stream))
                {
                    Name = GetType().Name + "_WriterThread",
                    IsBackground = true
                };

                readerThread = new Thread(() => ReaderThreadBody(stream))
                {
                    Name = GetType().Name + "_ReaderThread",
                    IsBackground = true
                };

                writerThread.Start();
                readerThread.Start();

                writerThread.Join();
                readerThread.Join();

                writerThread = null;
                readerThread = null;
                listenerThread = null;

                var connectionClosed = ConnectionClosed;
                if(connectionClosed != null)
                {
                    connectionClosed();
                }
            }
        }

        private readonly BlockingCollection<byte> queue;

        private CancellationTokenSource queueCancellationToken;
        private bool emitConfigBytes;
        private Thread listenerThread;
        private Thread readerThread;
        private Thread writerThread;
        private Socket server;
        private Socket socket;
    }
}
