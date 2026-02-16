//
// Copyright (c) 2010-2026 Antmicro
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

using AntShell.Helpers;

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
            server = SocketsManager.Instance.AcquireSocket(null, AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, new IPEndPoint(IPAddress.Any, port), listeningBacklog: 1, nameAppendix: this.serverName);

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

        public void ClearConnectionEvents()
        {
            ConnectionAccepted = null;
            ConnectionClosed = null;
            DataReceived = null;
        }

        public int BufferSize { get; set; } = 1;

        public bool IsAnythingReceiving => DataReceived != null && DataBlockReceived != null;

        public int? Port => (server?.LocalEndPoint as IPEndPoint)?.Port;

        public bool IsStarted => server?.IsBound ?? false;

        public Position TerminalSize { get; private set; }

        public event Action ConnectionClosed;

        public event Action<Stream> ConnectionAccepted;

        public event Action<int> DataReceived;

        public event Action<byte[]> DataBlockReceived;

        public event Action TerminalResized = () => {};

        private void ProcessWriteBinary(Stream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }

        private void ProcessWriteTelnet(Stream stream, byte[] data)
        {
            var offset = 0;
            while(offset < data.Length)
            {
                var iacIdx = Array.FindIndex(data, offset, data.Length - offset, item => item == IACEscape);
                if(iacIdx == -1)
                {
                    stream.Write(data, offset, data.Length - offset);
                    break;
                }
                // Move the index to cover the IAC
                iacIdx += 1;
                // Write all bytes up to and including the IAC, then redouble the IAC
                stream.Write(data, offset, iacIdx - offset);
                stream.WriteByte(IACEscape);
                offset = iacIdx;
            }
        }

        private void ProcessWrite(Stream stream, byte[] data)
        {
            if(telnetMode)
            {
                ProcessWriteTelnet(stream, data);
            }
            else
            {
                ProcessWriteBinary(stream, data);
            }
        }

        private void WriterThreadBody(Stream stream)
        {
            try
            {
                // This thread will poll for bytes constantly for `MaxReadThreadPoolingTimeMs` to assert we have the lowest possible latency while transmiting packet.
                var watch = new Stopwatch();
                while(!cancellationToken.IsCancellationRequested)
                {
                    watch.Start();
                    while(watch.ElapsedMilliseconds < MaxReadThreadPoolingTimeMs)
                    {
                        while(queue.TryDequeue(out var dequeued))
                        {
                            ProcessWrite(stream, dequeued);
                        }
                    }
                    watch.Reset();
                    enqueuedEvent.WaitOne();
                }
            }
            catch(OperationCanceledException)
            {
            }
            catch(IOException e)
            {
                Logger.LogAs(this, LogLevel.Info, $"Got exception when writing to socket: {e}");
            }
            catch(ObjectDisposedException)
            {
            }
            cancellationToken.Cancel();
        }

        private void SubmitReadBytes(byte[] buffer, int skip, int count)
        {
            DataBlockReceived?.Invoke(buffer.Skip(skip).Take(count).ToArray());

            var dataReceived = DataReceived;
            if(dataReceived == null) return;
            foreach(var b in buffer.Skip(skip).Take(count))
            {
                dataReceived((int)b);
            }
        }

        private void SubmitReadByte(byte b)
        {
            DataBlockReceived?.Invoke(new byte[] { b });
            DataReceived?.Invoke(b);
        }

        private void ProcessTelnetSubnegotiation(byte[] data)
        {
            if(data.Length != 5 || data[0] != (byte)TelnetOption.Naws) return;
            TerminalSize = new Position(
                (data[1] << 8) + data[2],
                (data[3] << 8) + data[4]
            );
            TerminalResized();
        }

        private void ProcessTelnetSpecial(byte data)
        {
            switch(telnetReadState)
            {
            case TelnetReadState.IAC:
                switch(data)
                {
                case (byte)TelnetCommand.Do:
                case (byte)TelnetCommand.Dont:
                case (byte)TelnetCommand.Will:
                case (byte)TelnetCommand.Wont:
                    telnetReadState = TelnetReadState.Negotiation;
                    break;
                case (byte)TelnetCommand.SubnegotiationBegin:
                    telnetReadState = TelnetReadState.SubnegotiationBegin;
                    break;
                case (byte)TelnetCommand.IAC:
                    SubmitReadByte(IACEscape);
                    telnetReadState = TelnetReadState.Normal;
                    break;
                default:
                    // Ignore all other commands
                    telnetReadState = TelnetReadState.Normal;
                    break;
                }
                break;
            case TelnetReadState.Negotiation:
                // We don't actually care about negotiations
                // NOTE: While RFC854 doesn't specify whether option code 255 has to be escaped, RFC861 defines option code 255 without any mention of escaping, so let's assume we don't escape
                telnetReadState = TelnetReadState.Normal;
                break;
            case TelnetReadState.SubnegotiationBegin:
                // NOTE: Like mentioned above, RFC855 doesn't specify whether option code 255 has to be escaped, but RFC861 doesn't mention any escapes

                // Only option we care about
                if(data == (byte)TelnetOption.Naws)
                {
                    subnegotiationBuffer = new List<byte>() { data };
                }
                telnetReadState = TelnetReadState.Subnegotiation;
                break;
            case TelnetReadState.Subnegotiation:
                if(data == IACEscape)
                {
                    telnetReadState = TelnetReadState.SubnegotiationIAC;
                    break;
                }
                if(subnegotiationBuffer != null)
                {
                    subnegotiationBuffer.Add(data);
                }
                break;
            case TelnetReadState.SubnegotiationIAC:
                switch(data)
                {
                case (byte)TelnetCommand.IAC:
                    if(subnegotiationBuffer != null)
                    {
                        subnegotiationBuffer.Add(IACEscape);
                    }
                    telnetReadState = TelnetReadState.Subnegotiation;
                    break;
                case (byte)TelnetCommand.SubnegotiationEnd:
                    if(subnegotiationBuffer != null)
                    {
                        ProcessTelnetSubnegotiation(subnegotiationBuffer.ToArray());
                        subnegotiationBuffer = null;
                    }
                    telnetReadState = TelnetReadState.Normal;
                    break;
                default:
                    // NOTE: RFC855 doesn't specify, but implies that IAC codes other than IAC IAC or IAC SE aren't allowed during subnegotiation, so ignore non-allowed codes
                    telnetReadState = TelnetReadState.Subnegotiation;
                    break;
                }
                break;
            }
        }

        private void ProcessReadBinary(byte[] buffer, int count)
        {
            SubmitReadBytes(buffer, 0, count);
        }

        private void ProcessReadTelnet(byte[] buffer, int count)
        {
            var offset = 0;
            while(true)
            {
                while(telnetReadState != TelnetReadState.Normal && offset < count)
                {
                    ProcessTelnetSpecial(buffer[offset]);
                    offset += 1;
                }
                var iacIdx = Array.FindIndex(buffer, offset, count - offset, b => b == IACEscape);
                if(iacIdx == -1)
                {
                    SubmitReadBytes(buffer, offset, count - offset);
                    break;
                }
                SubmitReadBytes(buffer, offset, iacIdx - offset);
                telnetReadState = TelnetReadState.IAC;
                offset = iacIdx + 1;
            }
        }

        private void ProcessRead(byte[] buffer, int count)
        {
            if(telnetMode)
            {
                ProcessReadTelnet(buffer, count);
            }
            else
            {
                ProcessReadBinary(buffer, count);
            }
        }

        private void ReaderThreadBody(Stream stream)
        {
            var buffer = new byte[BufferSize];

            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    var count = stream.Read(buffer, 0, BufferSize);

                    if(count == 0)
                    {
                        break;
                    }

                    ProcessRead(buffer, count);
                }
            }
            catch(IOException e)
            {
                Logger.LogAs(this, LogLevel.Info, $"Got exception when reading from socket: {e}");
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
                            255, 253,  31, // IAC DO    NAWS
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

        private volatile bool stopRequested;
        private Thread listenerThread;
        private Thread readerThread;
        private Thread writerThread;
        private Socket server;
        private Socket socket;

        private TelnetReadState telnetReadState;
        private List<byte> subnegotiationBuffer;

        private ConcurrentQueue<byte[]> queue;

        private CancellationTokenSource cancellationToken;
        private readonly AutoResetEvent enqueuedEvent;
        private readonly bool telnetMode;
        private readonly bool flushOnConnect;
        private readonly string serverName;

        private const int IACEscape = 255;
        private const int MaxReadThreadPoolingTimeMs = 60;

        private enum TelnetReadState
        {
            Normal,
            IAC,
            Negotiation,
            SubnegotiationBegin,
            Subnegotiation,
            SubnegotiationIAC
        }

        private enum TelnetCommand : byte
        {
            SubnegotiationEnd = 0xf0,
            // The commands in the range [0xf1; 0xf9] commands are alternative ways of sending Ctrl-C, Backspace, etc., which we don't need to support
            SubnegotiationBegin = 0xfa,
            Will = 0xfb,
            Wont = 0xfc,
            Do = 0xfd,
            Dont = 0xfe,
            IAC = 0xff
        }

        private enum TelnetOption : byte
        {
            // There may be more options that just this in the future, but currently we just have the one
            Naws = 31
        }
    }
}