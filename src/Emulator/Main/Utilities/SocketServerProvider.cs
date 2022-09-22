//
// Copyright (c) 2010-2022 Antmicro
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
using System.Diagnostics;
using System.Collections.Concurrent;
using Antmicro.Renode.Exceptions;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public class SocketServerProvider : IDisposable
    {
        public SocketServerProvider(bool emitConfigBytes = true, bool flushOnConnect = false)
        {
            queue = new ConcurrentQueue<byte>();
            enqueuedEvent = new AutoResetEvent(false);
            this.emitConfigBytes = emitConfigBytes;
            this.flushOnConnect = flushOnConnect;
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

            stopRequested = false;
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
            stopRequested = true;
            listenerThread?.Join();
        }

        public void Dispose()
        {
            Stop();
        }

        public void SendByte(byte b)
        {
            queue.Enqueue(b);
            enqueuedEvent.Set();
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
                // This thread will poll for bytes constantly for `MaxReadThreadPoolingTimeMs` to assert we have the lowest possible latency while transmiting packet.
                var watch = new Stopwatch();
                while(!writerCancellationToken.IsCancellationRequested)
                {
                    watch.Start();
                    while(watch.ElapsedMilliseconds < MaxReadThreadPoolingTimeMs)
                    {
                        while(queue.TryDequeue(out var dequequed))
                        {
                            stream.WriteByte(dequequed);
                        }
                    }
                    watch.Reset();
                    enqueuedEvent.WaitOne();
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
            var value = 0;
            while(value != -1)
            {
                try
                {
                    value = stream.ReadByte();
                    if(value != -1)
                    {
                        DataReceived?.Invoke(value);
                    }
                }
                catch(IOException)
                {
                    value = -1;
                    break;
                }
            }

            Logger.LogAs(this, LogLevel.Debug, "Client disconnected, stream closed.");
            writerCancellationToken.Cancel();
            enqueuedEvent.Set();
        }

        private void ListenerThreadBody()
        {
            NetworkStream stream;
            while(!stopRequested)
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
                        // we expect 9 bytes as a result of sending
                        // config bytes
                        for (int i = 0; i < 9; i++)
                        {
                            stream.ReadByte();
                        }
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

                var connectionAccepted = ConnectionAccepted;
                if(connectionAccepted != null)
                {
                    connectionAccepted(stream);
                }

                if(flushOnConnect)
                {
                    // creating a new queue not to have to lock accesses to it.
                    queue = new ConcurrentQueue<byte>();
                }

                writerCancellationToken = new CancellationTokenSource();
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

        private ConcurrentQueue<byte> queue;

        private CancellationTokenSource writerCancellationToken;
        private AutoResetEvent enqueuedEvent;
        private bool emitConfigBytes;
        private bool flushOnConnect;
        private volatile bool stopRequested;
        private Thread listenerThread;
        private Thread readerThread;
        private Thread writerThread;
        private Socket server;
        private Socket socket;

        private const int MaxReadThreadPoolingTimeMs = 60;
    }
}
