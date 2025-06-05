//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Sockets;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public class SocketServerProvider : IDisposable
    {
        public SocketServerProvider(bool telnetMode = true, bool flushOnConnect = false, string serverName = "")
        {
            queue = new ConcurrentQueue<byte[]>();
            enqueuedEvent = new AutoResetEvent(false);
            this.telnetMode = telnetMode;
            this.flushOnConnect = flushOnConnect;
            this.serverName = serverName;
        }

        public void Start(int port)
        {
            server = SocketsManager.Instance.AcquireSocket(null ,AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, new IPEndPoint(IPAddress.Any, port), listeningBacklog: 1, nameAppendix: this.serverName);

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
                if(!SocketsManager.Instance.TryDropSocket(server))
                {
                    Logger.LogAs(this, LogLevel.Debug, "Failed to drop socket from the manager");
                }
            }
            socket?.Close();
            stopRequested = true;
            cancellationToken?.Cancel();

            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
            if(readerThread?.ManagedThreadId != currentThreadId)
            {
                listenerThread?.Join();
                listenerThread = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void SendByte(byte b)
        {
            queue.Enqueue(new byte[] { b });
            enqueuedEvent.Set();
        }

        public void Send(byte[] bytes)
        {
            queue.Enqueue(bytes);
            enqueuedEvent.Set();
        }

        public void Send(IEnumerable<byte> bytes)
        {
            Send(bytes.ToArray());
        }

        public int BufferSize { get; set; } = 1;

        public bool IsAnythingReceiving => DataReceived != null && DataBlockReceived != null;

        public int? Port => (server?.LocalEndPoint as IPEndPoint)?.Port;

        public event Action ConnectionClosed;
        public event Action<Stream> ConnectionAccepted;
        public event Action<int> DataReceived;
        public event Action<byte[]> DataBlockReceived;

        private void WriterThreadBody(Stream stream)
        {
            try
            {
                var iacEscapePosition = -1;
                // This thread will poll for bytes constantly for `MaxReadThreadPoolingTimeMs` to assert we have the lowest possible latency while transmiting packet.
                var watch = new Stopwatch();
                while(!cancellationToken.IsCancellationRequested)
                {
                    watch.Start();
                    while(watch.ElapsedMilliseconds < MaxReadThreadPoolingTimeMs)
                    {
                        while(queue.TryDequeue(out var dequeued))
                        {
                            if(!telnetMode || (iacEscapePosition = Array.FindIndex(dequeued, x => x == IACEscape)) == -1)
                            {
                                stream.Write(dequeued, 0, dequeued.Length);
                            }
                            else
                            {
                                // If we're in telnetMode and we discover the IACEscape byte, we should double it
                                stream.Write(dequeued, 0, iacEscapePosition + 1);
                                stream.Write(new byte[]{ IACEscape }, 0, 1);
                                var lengthOfRest = dequeued.Length - (iacEscapePosition + 1);
                                if(lengthOfRest > 0)
                                {
                                    stream.Write(dequeued, iacEscapePosition + 1, lengthOfRest);
                                }
                            }
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
            cancellationToken.Cancel();
        }

        private void ReaderThreadBody(Stream stream)
        {
            var size = BufferSize;
            var buffer = new byte[size];

            while(!cancellationToken.IsCancellationRequested)
            {
                if(size != BufferSize)
                {
                    size = BufferSize;
                    buffer = new byte[size];
                }
                try
                {
                    var count = stream.Read(buffer, 0, size);

                    if(count == 0)
                    {
                        break;
                    }

                    if(telnetMode && buffer[0] == IACEscape)
                    {
                        if(iacEscapeSpotted)
                        {
                            // Previous Read ended in IAC
                            // IAC followed by IAC effectively sends 255 as input
                            bytesToIgnore = 0;
                            iacEscapeSpotted = false;
                        }
                        else
                        {
                            // Ignore IAC commands, 3 bytes each (unless followed by another IAC)
                            // TODO: look for iac in the middle of the sequence?
                            // In practice it doesn't seem to happen at all
                            // We explicitly do NOT handle subcommand negotiation, a possible todo for the future
                            bytesToIgnore = IACCommandBytes;

                            if(count == 1)
                            {
                                // We see the IAC code and must ensure the next Read does not yield another one
                                iacEscapeSpotted = true;
                            }
                            else if(buffer[1] == IACEscape)
                            {
                                // We receive a longer data batch starting with two IAC bytes: just skip one byte, leave the other one as is
                                bytesToIgnore = 1;
                            }
                        }
                    }
                    else
                    {
                        // Clear IAC escape mode - we encountered a different character in the package
                        iacEscapeSpotted = false;
                    }
                    var skip = 0;
                    if(bytesToIgnore > 0)
                    {
                        skip = bytesToIgnore > count ? count : bytesToIgnore;
                        bytesToIgnore -= skip;
                        count -= skip;
                    }

                    DataBlockReceived?.Invoke(buffer.Skip(skip).Take(count).ToArray());

                    var dataReceived = DataReceived;
                    if(dataReceived != null)
                    {
                        foreach(var b in buffer.Skip(skip).Take(count))
                        {
                            dataReceived((int)b);
                        }
                    }
                }
                catch(IOException)
                {
                    break;
                }
            }

            Logger.LogAs(this, LogLevel.Debug, "Client disconnected, stream closed.");
            cancellationToken.Cancel();
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
                    if(telnetMode)
                    {
                        var initBytes = new byte[] {
                            255, 253,   0, // IAC DO    BINARY
                            255, 251,   1, // IAC WILL  ECHO
                            255, 251,   3, // IAC WILL  SUPPRESS_GO_AHEAD
                            255, 252,  34, // IAC WONT  LINEMODE
                        };
                        stream.Write(initBytes, 0, initBytes.Length);
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
                    queue = new ConcurrentQueue<byte[]>();
                }

                cancellationToken = new CancellationTokenSource();
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

                var connectionClosed = ConnectionClosed;
                if(connectionClosed != null)
                {
                    connectionClosed();
                }
            }
            listenerThread = null;
        }

        private ConcurrentQueue<byte[]> queue;

        private CancellationTokenSource cancellationToken;
        private AutoResetEvent enqueuedEvent;
        private bool telnetMode;
        private bool flushOnConnect;
        private readonly string serverName;
        private volatile bool stopRequested;
        private Thread listenerThread;
        private Thread readerThread;
        private Thread writerThread;
        private Socket server;
        private Socket socket;
        private int bytesToIgnore;
        private bool iacEscapeSpotted;

        private const int IACEscape = 255;
        private const int IACCommandBytes = 3;
        private const int MaxReadThreadPoolingTimeMs = 60;
    }
}
