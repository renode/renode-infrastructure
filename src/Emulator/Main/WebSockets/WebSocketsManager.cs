//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.WebSockets
{
    public delegate void NewConnectionEvent(WebSocket webSocket, List<string> extraSegments);

    public interface IWebSocketServerProvider
    {
        void NewConnectionEventHandler(HttpListenerContext listenerContext, WebSocket webSocket, List<string> extraSegments);

        IReadOnlyList<WebSocketConnection> Connections { get; }
    }

    public class WebSocketsManager : IDisposable
    {
        static WebSocketsManager()
        {
            Instance = new WebSocketsManager();
        }

        public static readonly WebSocketsManager Instance;

        public bool Start(int portToUse)
        {
            int minPort = 21234;
            int maxPort = 31234;

            // If user request specific port via option --server-mode-port - try to acquire only this port
            if(portToUse != 21234)
            {
                minPort = portToUse;
                maxPort = portToUse;
            }

            if(!TryCreateListener(minPort, maxPort, out var port, out httpListener))
            {
                return false;
            }

            Port = port;

            Logger.Log(LogLevel.Info, $"Listening on port {Port}");
            listenerTask = Task.Run(AsyncListener);

            return true;
        }

        public bool RegisterEndpoint(string endpoint, IWebSocketServerProvider provider)
        {
            if(endpoints.ContainsKey(endpoint))
            {
                return false;
            }

            Logger.Log(LogLevel.Info, $"Listening for new requests at http://localhost:{Port}{endpoint}");
            endpoints.Add(endpoint, provider);

            return true;
        }

        public bool UnregisterEndpoint(string endpoint)
        {
            if(!endpoints.ContainsKey(endpoint))
            {
                return false;
            }

            Logger.Log(LogLevel.Debug, $"Stopped listening for new requests at endpoint: http://localhost:{Port}{endpoint}");
            endpoints.Remove(endpoint);

            return true;
        }

        public IReadOnlyList<WebSocketConnection> GetConnections(string endpoint)
        {
            if(endpoints.TryGetValue(endpoint, out var provider))
            {
                return provider.Connections;
            }

            return null;
        }

        public void Dispose()
        {
            if(alreadyDisposed)
            {
                return;
            }

            alreadyDisposed = true;
            cancellationToken.Cancel();
            httpListener.Abort();
            listenerTask.Wait();
        }

        public int Port { get; private set; }

        private WebSocketsManager()
        {
            endpoints = new Dictionary<string, IWebSocketServerProvider>();
            cancellationToken = new CancellationTokenSource();
            alreadyDisposed = false;
        }

        private bool TryCreateListener(int minPort, int maxPort, out int port, out HttpListener httpListener)
        {
            for(port = minPort; port <= maxPort; port++)
            {
                httpListener = TryCreateListener($"http://*:{port}/");
                if(httpListener != null)
                {
                    return true;
                }
#if PLATFORM_WINDOWS
                // On Windows, we need admin permissions to bind to all interfaces, so we need to fall back to localhost if binding to `*` fails.
                httpListener = TryCreateListener($"http://localhost:{port}/");
                if(httpListener != null)
                {
                    return true;
                }
#endif
            }

            httpListener = null;
            port = 0;
            return false;
        }

        private HttpListener TryCreateListener(string address)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(address);
            try
            {
                listener.Start();
                return listener;
            }
            catch(Exception)
            {
            }
            return null;
        }

        private async Task AsyncListener()
        {
            try
            {
                httpListener.Start();
            }
            catch(Exception e)
            {
                Logger.Log(LogLevel.Warning, $"HTTP Listener error: {e.Message}");
                cancellationToken.Cancel();
            }

            while(!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await httpListener.GetContextAsync();
                }
                catch(Exception)
                {
                    break;
                }

                Logger.Log(LogLevel.Info, $"New connection at: {context.Request.Url.AbsolutePath} on port: {context.Request.RemoteEndPoint.Port}");

                if(!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    continue;
                }

                var path = context.Request.Url.AbsolutePath;
                var endpoint = endpoints.Where(e => path.StartsWith(e.Key)).FirstOrDefault();
                var endpointName = endpoint.Key;
                var provider = endpoint.Value;

                if(provider == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                var requestSegments = path.Trim('/').Split('/');
                var endpointSegments = endpointName.Trim('/').Split('/');
                var extraSegments = requestSegments.Skip(endpointSegments.Length).ToList();
                var webSocketContext = await context.AcceptWebSocketAsync(null);

                provider.NewConnectionEventHandler(context, webSocketContext.WebSocket, extraSegments);
            }
        }

        private HttpListener httpListener;
        private Task listenerTask;
        private bool alreadyDisposed;
        private readonly CancellationTokenSource cancellationToken;
        private readonly Dictionary<string, IWebSocketServerProvider> endpoints;
    }
}
