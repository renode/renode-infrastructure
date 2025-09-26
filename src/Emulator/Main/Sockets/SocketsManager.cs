//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Sockets
{
    public class SocketsManager : IDisposable
    {
        static SocketsManager()
        {
            Instance = new SocketsManager();
            sockets = new List<SocketInstance>();
        }

        public static SocketsManager Instance { get; private set; }

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

        public Socket AcquireSocket(IEmulationElement owner, AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint endpoint, string nameAppendix = "", int? listeningBacklog = null, bool asClient = false, bool noDelay = true)
        {
            var s = new SocketInstance(owner, addressFamily, socketType, protocolType, endpoint, nameAppendix: nameAppendix, asClient: asClient, noDelay: noDelay, listeningBacklog: listeningBacklog);
            sockets.Add(s);
            return s.Socket;
        }

        public bool TryDropSocket(Socket socket)
        {
            SocketInstance socketInstance = sockets.Where(x => x.Socket == socket).FirstOrDefault();
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
                Socket = new Socket(addressFamily, socketType, protocolType);
                if(protocolType == ProtocolType.Tcp)
                {
                    Socket.NoDelay = noDelay;
                }
                this.endpoint = endpoint.ToString();
                try
                {
                    if(asClient)
                    {
                        Socket.Connect(endpoint);
                    }
                    else
                    {
                        Socket.Bind(endpoint);
                        Socket.Listen(listeningBacklog ?? 0);
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
                    if(Socket.Connected)
                    {
                        Socket.Shutdown(SocketShutdown.Both);
                    }
                }
                finally
                {
                    Socket.Close();
                }

                Socket.Dispose();
            }

            public string EndPoint => endpoint;

            public bool IsConnected => Socket.Connected;

            public bool IsBound => Socket.IsBound;

            public SocketType Type => Socket.SocketType;

            public string Owner => ownerName;

            public Socket Socket;

            private readonly string ownerName;
            private readonly string endpoint;
        }
    }
}