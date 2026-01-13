//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Antmicro.Renode.Logging;
using Antmicro.Renode.WebSockets;

namespace Antmicro.Renode.Utilities
{
    public class WebSocketServerProvider : IWebSocketServerProvider, IDisposable
    {
        public WebSocketServerProvider(string endpoint = "/", bool allowMultipleConnections = false)
        {
            connections = new List<WebSocketConnection>();
            connectionsSharedData = new WebSocketConnectionSharedData
            {
                Endpoint = endpoint
            };

            this.isDisposed = false;
            this.isRunning = false;
            this.Disconnected += DisconnectedEventHandler;
            this.allowMultipleConnections = allowMultipleConnections;
        }

        public bool Start()
        {
            if(isDisposed || isRunning)
            {
                return false;
            }

            if(!WebSocketsManager.Instance.RegisterEndpoint(connectionsSharedData.Endpoint, this))
            {
                return false;
            }

            isRunning = true;
            return true;
        }

        public void Dispose()
        {
            if(isDisposed || !isRunning)
            {
                return;
            }

            isDisposed = true;
            WebSocketsManager.Instance.UnregisterEndpoint(connectionsSharedData.Endpoint);
            var connectionsCopy = new List<WebSocketConnection>(connections);

            foreach(var connection in connectionsCopy)
            {
                connection.Dispose();
            }
        }

        public void BroadcastByte(byte b)
        {
            foreach(var connection in connections)
            {
                connection.SendByte(b);
            }
        }

        public void Broadcast(byte[] bytes)
        {
            foreach(var connection in connections)
            {
                connection.Send(bytes);
            }
        }

        public void Broadcast(IEnumerable<byte> bytes)
        {
            var bytesAsArray = bytes.ToArray();
            foreach(var connection in connections)
            {
                connection.Send(bytesAsArray);
            }
        }

        public void NewConnectionEventHandler(HttpListenerContext listenerContext, WebSocket webSocket, List<string> extraSegments)
        {
            if(!allowMultipleConnections && connections.Count != 0)
            {
                var currentConnection = connections.First();
                currentConnection.Dispose();
            }

            var newConnection = new WebSocketConnection(listenerContext, webSocket, connectionsSharedData);
            connections.Add(newConnection);
            NewConnection?.Invoke(newConnection, extraSegments);
        }

        public int ConnectionsCount => connections.Count;

        public bool IsAnythingReceiving => connectionsSharedData.DataReceived != null && connectionsSharedData.DataBlockReceived != null;

        public IReadOnlyList<WebSocketConnection> Connections => connections;

        public event Action<WebSocketConnection, List<string>> NewConnection;

        public event Action<WebSocketConnection, int> DataReceived
        {
            add => connectionsSharedData.DataReceived += value;
            remove => connectionsSharedData.DataReceived -= value;
        }

        public event Action<WebSocketConnection, byte[]> DataBlockReceived
        {
            add => connectionsSharedData.DataBlockReceived += value;
            remove => connectionsSharedData.DataBlockReceived -= value;
        }

        public event Action<WebSocketConnection> Disconnected
        {
            add => connectionsSharedData.Disconnected += value;
            remove => connectionsSharedData.Disconnected -= value;
        }

        protected readonly bool allowMultipleConnections;
        protected readonly List<WebSocketConnection> connections;
        protected readonly WebSocketConnectionSharedData connectionsSharedData;

        private void DisconnectedEventHandler(WebSocketConnection sender)
        {
            connections.Remove(sender);
        }

        private bool isDisposed;
        private bool isRunning;
    }

    public class WebSocketSingleConnectionServer : WebSocketServerProvider
    {
        public WebSocketSingleConnectionServer(string endpoint = "/", bool bufferMessages = false) : base(endpoint, false)
        {
            this.bufferMessages = bufferMessages;
            this.NewConnection += NewConnectionEventHandler;

            if(bufferMessages)
            {
                bufferQueue = new ConcurrentQueue<byte[]>();
            }
        }

        public void SendByte(byte b)
        {
            if(IsConnected)
            {
                CurrentConnection.SendByte(b);
            }
            else if(bufferMessages)
            {
                bufferQueue.Enqueue(new[] { b });
            }
        }

        public void Send(byte[] bytes)
        {
            if(IsConnected)
            {
                CurrentConnection.Send(bytes);
            }
            else if(bufferMessages)
            {
                bufferQueue.Enqueue(bytes);
            }
        }

        public bool IsConnected => connections.Count != 0;

        public WebSocketConnection CurrentConnection => connections.FirstOrDefault();

        private void NewConnectionEventHandler(WebSocketConnection sender, List<string> extraSegments)
        {
            if(bufferMessages)
            {
                while(bufferQueue.TryDequeue(out var bytes))
                {
                    CurrentConnection.Send(bytes);
                }
            }
        }

        private readonly bool bufferMessages;
        private readonly ConcurrentQueue<byte[]> bufferQueue;
    }

    public class WebSocketConnectionSharedData
    {
        public Action<WebSocketConnection, int> DataReceived;
        public Action<WebSocketConnection, byte[]> DataBlockReceived;
        public Action<WebSocketConnection> Disconnected;
        public string Endpoint;
    }

    public class WebSocketConnection : IDisposable
    {
        public WebSocketConnection(HttpListenerContext listenerContext, WebSocket socket, WebSocketConnectionSharedData sharedData)
        {
            this.listenerContext = listenerContext;
            this.webSocket = socket;
            this.sharedData = sharedData;
            this.DataBlockReceived += (data) => sharedData.DataBlockReceived?.Invoke(this, data);
            this.DataReceived += (data) => sharedData.DataReceived?.Invoke(this, data);
            BufferSize = 4096;
            cancellationToken = new CancellationTokenSource();
            cancellationToken.Token.Register(() => this.sharedData.Disconnected?.Invoke(this));
            queue = new ConcurrentQueue<byte[]>();
            enqueuedEvent = new AutoResetEvent(false);
            readerTask = Task.Run(AsyncReader);
            writerTask = Task.Run(AsyncWriter);
        }

        public void Dispose()
        {
            try
            {
                if(webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken.Token).GetAwaiter().GetResult();
                    CloseSocket();
                }
            }
            catch(Exception)
            {
                // Intentionally left empty
            }

            if(!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.Cancel();
            }

            readerTask.Wait();
            writerTask.Wait();
        }

        public void SendByte(byte b)
        {
            queue.Enqueue(new[] { b });
            enqueuedEvent.Set();
        }

        public void Send(byte[] bytes)
        {
            queue.Enqueue(bytes);
            enqueuedEvent.Set();
        }

        public int BufferSize { get; private set; }

        public event Action<byte[]> DataBlockReceived;

        public event Action<int> DataReceived;

        private async Task AsyncReader()
        {
            var size = BufferSize;
            var buffer = new byte[size];

            Logger.Log(LogLevel.Debug, $"WebSocket: Begin of reader task for endpoint {sharedData.Endpoint}");
            while(!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = null;
                int totalBytes = 0;
                try
                {
                    totalBytes = 0;
                    while(true)
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, totalBytes, BufferSize - totalBytes), cancellationToken.Token);
                        totalBytes += result.Count;

                        if(result.EndOfMessage)
                        {
                            break;
                        }
                        else if(totalBytes == BufferSize)
                        {
                            BufferSize += 4096;
                            buffer = buffer.CopyAndResize(BufferSize);
                        }
                    }

                    if(result.MessageType == WebSocketMessageType.Close)
                    {
                        Logger.Log(LogLevel.Debug, $"Received WebSocket close request for endpoint {sharedData.Endpoint}");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken.Token);
                        break;
                    }
                }
                catch(Exception)
                {
                    break;
                }

                if(result.Count == 0)
                {
                    break;
                }

                var fixedBuffer = buffer.Take(totalBytes).ToArray();
                DataBlockReceived.Invoke(fixedBuffer);

                foreach(var b in fixedBuffer)
                {
                    DataReceived((int)b);
                }

            }

            if(!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.Cancel();
            }

            CloseSocket();

            Logger.Log(LogLevel.Debug, $"WebSocket: End of reader task for endpoint {sharedData.Endpoint}");
        }

        private async Task AsyncWriter()
        {
            Logger.Log(LogLevel.Debug, $"WebSocket: Begin of writer task for endpoint {sharedData.Endpoint}");
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = WaitHandle.WaitAny(new[] { enqueuedEvent, cancellationToken.Token.WaitHandle });

                    if(result == 1)
                    {
                        break;
                    }

                    while(queue.TryDequeue(out var dequeued))
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(dequeued, 0, dequeued.Length), WebSocketMessageType.Text, true, cancellationToken.Token);
                    }
                }
                catch(Exception)
                {
                    break;
                }
            }

            if(!cancellationToken.IsCancellationRequested)
            {
                cancellationToken.Cancel();
            }

            Logger.Log(LogLevel.Debug, $"WebSocket: End of writer task for endpoint {sharedData.Endpoint}");
        }

        private void CloseSocket()
        {
            // HACK - calling .CloseAsync() on websocket closes only websocket (Application layer) and leaves
            // socket (Transport layer) in half open state. To fix that we need to .Close() the httpListenerContext
            // and completly close the connection
            listenerContext.Response.Close();
        }

        private readonly CancellationTokenSource cancellationToken;
        private readonly WebSocket webSocket;
        private readonly HttpListenerContext listenerContext;
        private readonly Task readerTask;
        private readonly Task writerTask;

        private readonly ConcurrentQueue<byte[]> queue;
        private readonly AutoResetEvent enqueuedEvent;

        private readonly WebSocketConnectionSharedData sharedData;
    }
}