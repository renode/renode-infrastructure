//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if NET
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Sockets;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public class SocketInterface : IHostNetworkInterface
    {
        public SocketInterface(string socketPath, Func<SocketInterface, Task> configureInterfaceCallback = null)
        {
            OriginalSocketPath = socketPath;
            cts = new CancellationTokenSource();
            if(configureInterfaceCallback != null)
            {
                ConfigureInterfaceCallback = configureInterfaceCallback;
            }
            else
            {
                ConfigureInterfaceCallback = async (_) => { };
            }
            Init();
        }

        public void Start()
        {
            Resume();
        }

        public void Pause()
        {
            IsPaused = true;
            UpdateInterfaceState();
        }

        public void Resume()
        {
            IsPaused = false;
            UpdateInterfaceState();
        }

        public void CloseSockets()
        {
            DuplicatedSocket.Close();
            SocketsManager.Instance.TryDropSocket(OriginalSocket);
        }

        public void CancelRecv()
        {
            if(connectionEstablished)
            {
                cts.Cancel();
                cts = new CancellationTokenSource();
                DuplicatedSocket.Close();
                DuplicatedSocket = DuplicateSocket(OriginalSocket);
            }
        }

        public void Dispose()
        {
            connectionEstablished = false;
            UpdateInterfaceState();
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(running)
            {
                Task.Run(async () => await ReceiveFrameTask(frame));
            }
            else
            {
                this.NoisyLog("Packet has been dropped because the interface has not been configured yet or is not started.");
            }
        }

        [PostDeserialization]
        public void Init()
        {
            OriginalSocket = SocketsManager.Instance.AcquireSocket(this, AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified, new UnixDomainSocketEndPoint(OriginalSocketPath), asClient: true, clientBind: true);
            DuplicatedSocket = DuplicateSocket(OriginalSocket);
            var configureInterfaceTask  = Task.Run(async () => await ConfigureInterfaceCallback(this));

            configureInterfaceTask.ContinueWith(t =>
            {
                connectionEstablished = true;
                UpdateInterfaceState();
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            configureInterfaceTask.ContinueWith(t =>
            {
                this.ErrorLog("Couldn't establish a socket connection.");
                CloseSockets();
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        [DllImport("libc")]
        static extern int dup(int fd);

        public MACAddress MAC { get; set; }

        public bool IsPaused { get; private set; } = true;

        public event Action<EthernetFrame> FrameReady;

        // The only way to cancel a blocking read on a socket is to close it.
        // Therefore, we use a duplicated socket to allow canceling the read operation.
        // Invariant: DuplicatedSocket must remain accessible while OriginalSocket is open.
        [Transient]
        public Socket DuplicatedSocket;

        public const int Mtu = 1522;

        private Socket DuplicateSocket(Socket socket)
        {
            var socketFd = socket.SafeHandle.DangerousGetHandle();
            var duplicatedFd = dup((int)socketFd);
            if(duplicatedFd == -1)
            {
                throw new RecoverableException("Cannot duplicate a socket");
            }
            var handle = new SafeSocketHandle(duplicatedFd, true);
            return new Socket(handle);
        }

        private void UpdateInterfaceState()
        {
            // UpdateIntefaceState is responsible for logging when connection is established,
            // But logs about connection lost should be manged by others (there are different reasons for connection lost)
            if(!_connectionEstablished && connectionEstablished)
            {
                this.InfoLog("Connection established with foreign host");
            }

            if(_connectionEstablished && !connectionEstablished)
            {
                var bytes = new byte[1];
                try
                {
                    // Send termination packet to close remote host if its still up.
                    this.DuplicatedSocket.Send(bytes, 0, SocketFlags.None);
                    this.InfoLog("Closing connection to remote host.");
                }
                catch
                {
                    // Ignore any exceptions, socket may already be closed.
                }
                CloseSockets();
            }

            _connectionEstablished = connectionEstablished;

            if(running)
            {
                if(IsPaused)
                {
                    CancelRecv();
                    running = false;
                }
                if(!connectionEstablished)
                {
                    running = false;
                }
            }
            else
            {
                if(!IsPaused && connectionEstablished)
                {
                    readerTask = Task.Run(async () => await ReadPacketAsync(cts.Token));
                    readerTask.ContinueWith(_ =>
                    {
                        this.ErrorLog("Connection closed by foreign host");
                        connectionEstablished = false;
                        UpdateInterfaceState();
                    }, TaskContinuationOptions.OnlyOnFaulted);
                    running = true;
                }
            }
        }

        private async Task ReadPacketAsync(CancellationToken ct)
        {
            var buffer = new byte[Mtu];
            while(!ct.IsCancellationRequested)
            {
                var size = await DuplicatedSocket.ReceiveAsync(buffer, ct);
                if(ct.IsCancellationRequested)
                {
                    return;
                }
                if(Misc.TryCreateFrameOrLogWarning(this, buffer, out var frame, addCrc: true))
                {
                    FrameReady?.Invoke(frame);
                }
                else
                {
                    this.ErrorLog("Provided buffer couldn't be converted to a packet.");
                }
            }
        }

        private async Task ReceiveFrameTask(EthernetFrame frame)
        {
            var bytes = frame.Bytes;
            try
            {
                await DuplicatedSocket.SendAsync(bytes);
            }
            catch(Exception)
            {
                connectionEstablished = false;
                this.ErrorLog("Connection closed by remote host.");
                UpdateInterfaceState();
            }
            this.NoisyLog("Frame of length {0} sent to host.", frame.Length);
        }

        private string OriginalSocketPath { get; }

        private Func<SocketInterface, Task> ConfigureInterfaceCallback { get; }

        private bool running = false;
        private bool connectionEstablished = false;
        private bool _connectionEstablished = false;

        [Transient]
        private Task readerTask;

        [Transient]
        private CancellationTokenSource cts;

        [Transient]
        private Socket OriginalSocket;

        private readonly object lockObject = new object();
    }
}
#endif
