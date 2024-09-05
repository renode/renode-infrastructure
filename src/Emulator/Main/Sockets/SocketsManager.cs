//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Sockets
{
    public class SocketsManager : IDisposable
    {
        static SocketsManager()
        {
            Instance = new SocketsManager();
            sockets = new List<SocketInstance>();
        }

        public void Dispose()
        {
            foreach(var socketInstance in sockets)
            {
                socketInstance.Dispose();
            }
        }

        public void CleanUp()
        {
            sockets = new List<SocketInstance>();
        }

        public string[,] List()
        {
            var table = new Table().AddRow("Owner", "Type", "EndPoint", "Bound", "Connected");
            table.AddRows(sockets, x => x.Owner.ToString(), x => x.Type.ToString(), x => x.EndPoint, x => x.IsBound.ToString(), x => x.IsConnected.ToString());
            return table.ToArray();
        }

        public Socket AcquireSocket(IEmulationElement owner, AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint endpoint, string nameAppendix = "", int? listeningBacklog = null, int connectingTimeout = 0, int receiveTimeout = 0, int sendTimeout = 0, bool asClient = false, bool noDelay = true)
        {
            var s = new SocketInstance(owner, addressFamily, socketType, protocolType, endpoint, nameAppendix: nameAppendix, asClient: asClient, noDelay: noDelay, listeningBacklog: listeningBacklog);
            sockets.Add(s);
            return s.socket;
        }

        public bool TryDropSocket(Socket socket)
        {
            SocketInstance socketInstance = sockets.Where(x => x.socket == socket).FirstOrDefault();
            if(socketInstance != null)
            {
                sockets.Remove(socketInstance);
                socketInstance.Dispose();
                return true;
            }
            return false;
        }

        [Transient]
        private static List<SocketInstance> sockets;

        public static SocketsManager Instance { get; private set; }

        class SocketInstance : IDisposable
        {
            public SocketInstance(IEmulationElement owner, AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint endpoint, string nameAppendix = "", int? listeningBacklog = null, bool asClient = false, bool noDelay = true)
            {
                string name;
                if(!EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName((object)owner, out name))
                {
                    name = owner?.ToString() ?? ""; // If name is not obtainable, use the peripheral name
                }
                ownerName = String.Format("{0}{1}{2}", name, name.Length == 0 ? "" : ":", nameAppendix);
                socket = new Socket(addressFamily, socketType, protocolType);
                if(protocolType == ProtocolType.Tcp)
                {
                    socket.NoDelay = noDelay;
                }
                this.endpoint = endpoint.ToString();
                try
                {
                    if(asClient)
                    {
                        socket.Connect(endpoint);
                    }
                    else
                    {
                        socket.Bind(endpoint);
                        socket.Listen(listeningBacklog ?? 0);
                    }
                }
                catch(SocketException e)
                {
                    throw new RecoverableException($"Unable to create '{this.endpoint}' socket:[{e.SocketErrorCode}] {e.Message}");
                }
            }

            public void Dispose()
            {
                try
                {
                    if(socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                }
                finally
                {
                    socket.Close();
                }

                socket.Dispose();
            }

            public string EndPoint => endpoint;
            public bool IsConnected => socket.Connected;
            public bool IsBound => socket.IsBound;
            public SocketType Type => socket.SocketType;
            public string Owner => ownerName;

            private string ownerName;
            private string endpoint;
            public Socket socket;
        }
    }
}
