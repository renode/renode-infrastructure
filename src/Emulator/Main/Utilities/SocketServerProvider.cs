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

namespace Antmicro.Renode.Utilities
{
    public class SocketServerProvider : IDisposable
    {
        public SocketServerProvider()
        {
            queue = new BlockingCollection<byte>();
            queueCancellationToken = new CancellationTokenSource();        
        }

        public void Start(int port)
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Any, port));
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
            if(socket != null)
            {
                socket.Close();
            }
            queueCancellationToken.Cancel();
            listenerThreadStopped = true;
            server.Dispose();
            if(Thread.CurrentThread != readerThread && Thread.CurrentThread != writerThread) 
            {
                listenerThread.Join();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void SendByte(byte b)
        {
            queue.Add(b);
        }

        public bool IsAnythingReceiving { get { return DataReceived != null; } }

        public event Action ConnectionClosed;
        public event Action<Stream> ConnectionAccepted;
        public event Action<int> DataReceived;

        private void WriterThreadBody(Stream stream)
        {
            while(true)
            {
                try
                {
                    stream.WriteByte(queue.Take(queueCancellationToken.Token));
                }
                catch(OperationCanceledException)
                {
                    break;
                }
                catch(IOException)
                {
                    break;
                }
                catch(ObjectDisposedException)
                {
                    break;
                }
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
                    break;
                }
            }
        }

        private void ListenerThreadBody()
        {
            NetworkStream stream;
            listenerThreadStopped = false;
            while(!listenerThreadStopped)
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

                writerThread = new Thread(() => WriterThreadBody(stream)) {
                    Name = GetType().Name + "_WriterThread",
                    IsBackground = true
                };

                readerThread = new Thread(() => ReaderThreadBody(stream)) {
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
        }

        private readonly CancellationTokenSource queueCancellationToken;
        private readonly BlockingCollection<byte> queue;

        private bool listenerThreadStopped;
        private Thread listenerThread;
        private Thread readerThread;
        private Thread writerThread;
        private Socket server;
        private Socket socket;
    }
}
