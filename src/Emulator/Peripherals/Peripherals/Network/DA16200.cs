//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public static class DA16200NetworkExtension
    {
        public static void CreateDA16200Network(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new BasicNetwork<byte[], DA16200.NetworkAddress>(name), name);
        }
    }

    public class DA16200 : AtCommandModem, IBasicNetworkNode<byte[], DA16200.NetworkAddress>
    {
        public DA16200(IMachine machine) : base(machine)
        {
            sync = new object();
            address = new NetworkAddress(GenerateRandomAddress());
            dataModeState = new DataModeState(this);
        }

        public override void PassthroughWriteChar(byte value)
        {
            if(!dataModeState.TryConsumeCharacter(value))
            {
                dataModeState.Reset();
                PassthroughMode = false;
            }
        }

        public override void Reset()
        {
            base.Reset();
            udpSendAddress = null;
            udpSendPort = null;
            dataModeState?.Reset();

            for(var i = 0; i < connections.Length; i++)
            {
                connections[i] = null;
            }
        }

        public override void WriteChar(byte value)
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Warning, "Modem is not enabled, ignoring incoming byte 0x{0:x2}", value);
                return;
            }

            if(value == Escape && !PassthroughMode)
            {
                PassthroughMode = true;
                return;
            }

            base.WriteChar(value);
        }

        public void ReceiveData(byte[] data, NetworkAddress source, NetworkAddress destination)
        {
            if(!TryGetConnectionByPort(destination.Port, out var connection))
            {
                this.ErrorLog("No connection for local port {0}", destination.Port);
                return;
            }

            var commandName = connection.Type == ConnectionType.UDP ? "TRDUS" : "TRDTC";
            var dataPrefix = Encoding.ASCII.GetBytes($"+{commandName}:{connection.ID},{source.Address},{source.Port},{data.Length},");
            var response = dataPrefix.Concat(data).ToArray();
            ExecuteWithDelay(() => SendBytes(response), DataResponseDelayMilliseconds);
        }

        public NetworkAddress NodeAddress => address;

        public event BasicNetworkSendDataDelegate<byte[], NetworkAddress> TrySendData;

        public ulong DataResponseDelayMilliseconds { get; set; } = 50;

        public string IpAddress
        {
            get => address.Address;
            set => address = new NetworkAddress(value);
        }

        private void SendData(int connectionId, NetworkAddress destination, byte[] data)
        {
            if(!TryGetConnection(connectionId, out var connection))
            {
                this.ErrorLog("Invalid connection ID: {0}", connection);
                SendResponse(Error);
                return;
            }

            if(TrySendData == null)
            {
                this.WarningLog("Attempted to send data from a device not connected to a network");
                SendResponse(Error);
                return;
            }

            switch(connection.Type)
            {
            case ConnectionType.UDP:
                if(destination.Address == "0")
                {
                    if(udpSendAddress == "")
                    {
                        this.ErrorLog("UPD send address was not specified. Data will not be send");
                        SendResponse(Error);
                        return;
                    }
                    destination = destination.WithAddress(udpSendAddress);
                }
                if(destination.Port == 0)
                {
                    if(!udpSendPort.HasValue)
                    {
                        this.ErrorLog("UPD send port was not specified. Data will not be send");
                        SendResponse(Error);
                        return;
                    }
                    destination = destination.WithPort(udpSendPort.Value);
                }

                var source = address.WithPort(connection.LocalPort);
                if(!TrySendData(data, source, destination))
                {
                    SendResponse(Error);
                    return;
                }

                SendResponse(Ok);
                break;
            default:
                this.WarningLog("Connection type {0} is not supported", connection.Type);
                break;
            }
        }

        private bool TryGetNewConnectionNumber(out int connectionId, params int[] reservedNumbers)
        {
            lock(sync)
            {
                for(var i = 0; i < connections.Length; i++)
                {
                    if(connections[i] == null && !reservedNumbers.Any(reserved => reserved == i))
                    {
                        connectionId = i;
                        return true;
                    }
                }

                connectionId = -1;
                return false;
            }
        }

        private bool IsPortFree(ushort port)
        {
            return !TryGetConnectionByPort(port, out _);
        }

        private bool TryGetConnection(int connectionId, out Connection connection)
        {
            lock(sync)
            {
                if(connectionId < 0 || connectionId >= connections.Length)
                {
                    connection = null;
                    return false;
                }

                connection = connections[connectionId];
                return connection != null;
            }
        }

        private bool TryGetConnectionByPort(ushort localPort, out Connection connection)
        {
            lock(sync)
            {
                foreach(var conn in connections)
                {
                    if(conn == null)
                    {
                        continue;
                    }

                    if(conn.LocalPort == localPort)
                    {
                        connection = conn;
                        return true;
                    }
                }

                connection = null;
                return false;
            }
        }

        private string GenerateRandomAddress()
        {
            var rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            var components = new byte[4]{ 192, 168, 0, 0 };

            for(var i = 2; i < components.Length; i++)
            {
                components[i] = (byte)rng.Next(1, byte.MaxValue);
            }
            return string.Join(".", components);
        }

        // Commands

        [AtCommand("ATZ")]
        private Response Atz()
        {
            return Ok.WithParameters(
                "Display result off",
                "Echo {0}".FormatWith(EchoEnabled ? "on" : "off")
            );
        }

        [AtCommand("AT+WFMODE", CommandType.Write)]
        private Response WfModeWrite(WiFiMode mode)
        {
            if(mode == WiFiMode.SoftAccessPoint)
            {
                this.WarningLog("Soft Access Point mode is currently not supported");
                return Error;
            }

            this.DebugLog("AT+WFMODE: WiFi Mode = {0}", mode);
            return Ok;
        }

        [AtCommand("AT+RESTART")]
        private Response Restart()
        {
            ExecuteWithDelay(() => SendString("+INIT:DONE,0"));
            return Ok;
        }

        [AtCommand("AT+WFCC", CommandType.Write)]
        private Response WfccWrite(string countryCode)
        {
            this.DebugLog("AT+WFCC: Country code set to '{0}'", countryCode);
            return Ok;
        }

        [AtCommand("AT+WFJAP", CommandType.Write)]
        private Response WfjapWrite(string ssid, SecurityProtocol securityProtocol, int keyIndex, string password)
        {
            this.DebugLog("AT+WFJAP: Connecting to '{0}' ({1}, index: {2}) with password '{3}'", ssid, securityProtocol, keyIndex, password);
            return Ok;
        }

        // Socket commands

        [AtCommand("AT+TRTALL")]
        private Response Trall()
        {
            lock(sync)
            {
                for(var i = 0; i < connections.Length; i++)
                {
                    connections[i] = null;
                }
                return Ok;
            }
        }

        [AtCommand("AT+TRTRM", CommandType.Write)]
        private Response Trtrm(int connectionId, string ip = null, ushort? port = null)
        {
            lock(sync)
            {
                if(!TryGetConnection(connectionId, out var _))
                {
                    this.ErrorLog("AT+TRTRM: Invalid connection id: {0}", connectionId);
                    return Error;
                }

                connections[connectionId] = null;
                return Ok;
            }
        }

        [AtCommand("AT+TRUSE", CommandType.Write)]
        private Response Truse(ushort localPort)
        {
            lock(sync)
            {
                if(!IsPortFree(localPort))
                {
                    this.WarningLog("AT+TRUSE: Port {0} is already in use", localPort);
                    return Error;
                }

                if(!TryGetNewConnectionNumber(out var connectionNumber, DefaultTCPServerConnection, DefaultTCPClientConnection))
                {
                    return Error;
                }

                var connection = new Connection(this, connectionNumber, ConnectionType.UDP, localPort);
                connections[connectionNumber] = connection;
                
                return Ok.WithParameters($"+TRUSE:{connectionNumber}");
            }
        }

        [AtCommand("AT+TRUR", CommandType.Write)]
        private Response Trur(string address, ushort port)
        {
            udpSendAddress = address;
            udpSendPort = port;
            return Ok.WithParameters($"+TRUR:{DefaultUDPConnection}");
        }

        private readonly object sync;
        private readonly Connection[] connections = new Connection[MaxConnections];
        private readonly DataModeState dataModeState;
        private NetworkAddress address;

        private string udpSendAddress;
        private ushort? udpSendPort;

        // We assume that there is 10 maximum connections possible, because when
        // sending data there is no separator between the connection id and the length
        // of the message. When chenging this number the connection id decoding logic
        // in DataModeState:TryConsumeCharacter will also need to be updated.
        private const int MaxConnections = 10;
        private const int DefaultTCPServerConnection = 0;
        private const int DefaultTCPClientConnection = 1;
        private const int DefaultUDPConnection = 2;

        public class NetworkAddress
        {
            public NetworkAddress(string address, ushort port = 0)
            {
                Address = address;
                Port = port;
            }

            public NetworkAddress WithAddress(string address)
            {
                return new NetworkAddress(address, Port);
            }

            public NetworkAddress WithPort(ushort port)
            {
                return new NetworkAddress(Address, port);
            }

            public override bool Equals(object obj)
            {
                if(ReferenceEquals(this, obj))
                {
                    return true;
                }

                if(!(obj is NetworkAddress address))
                {
                    return false;
                }

                return address.Address == Address;
            }

            public override int GetHashCode()
            {
                return Address.GetHashCode();
            }

            public override string ToString()
            {
                return $"{Address}:{Port}";
            }

            public string Address { get; }
            public ushort Port { get; }
        }

        private class Connection
        {
            public Connection(DA16200 owner, int connectionId, ConnectionType connectionType, ushort localPort)
            {
                this.owner = owner;
                ID = connectionId;
                Type = connectionType;
                LocalPort = localPort;
            }

            public ConnectionType Type { get; }
            public int ID { get; }
            public ushort LocalPort { get; }

            private readonly DA16200 owner;
        }

        private class DataModeState
        {
            public DataModeState(DA16200 owner)
            {
                this.owner = owner;
                dataBuffer = new List<byte>();
            }

            public bool TryConsumeCharacter(byte value)
            {
                if(bytesToSkip > 0)
                {
                    bytesToSkip--;
                    return true;
                }

                if(!dataMode.HasValue)
                {
                    if(Enum.IsDefined(typeof(DataMode), (DataMode)value))
                    {
                        dataMode = (DataMode)value;
                        return true;
                    }

                    owner.ErrorLog("Invalid data mode value: {0}", value);
                    return false;
                }

                if(!connectionId.HasValue)
                {
                    var cid = (int)(value - '0');
                    if(cid < 0 || cid > 9)
                    {
                        owner.ErrorLog("Invalid connection ID byte: 0x{0:X}", value);
                        return false;
                    }

                    connectionId = cid;
                    // Skip ','
                    switch(dataMode)
                    {
                    case DataMode.H:
                    case DataMode.M:
                        bytesToSkip = 1;
                        break;
                    }
                    return true;
                }

                if(!dataLength.HasValue)
                {
                    return AggregateAndTryParseInt(value, ref dataLength);
                }

                if(remoteAddress == null)
                {
                    if(value == ',')
                    {
                        remoteAddress = Encoding.ASCII.GetString(dataBuffer.ToArray());
                        dataBuffer.Clear();
                        return true;
                    }
                    else
                    {
                        dataBuffer.Add(value);
                        return true;
                    }
                }

                if(!remotePort.HasValue)
                {
                    int? portInt = null;
                    var result = AggregateAndTryParseInt(value, ref portInt);
                    if(portInt.HasValue)
                    {
                        remotePort = (ushort)portInt;
                    }
                    return result;
                }

                if(dataLength == 0)
                {
                    if(value == '\r' || value == '\n')
                    {
                        dataToSend = dataBuffer.ToArray();
                        dataBuffer.Clear();
                    }
                    else
                    {
                        dataBuffer.Add(value);
                        return true;
                    }
                }
                else
                {
                    dataBuffer.Add(value);
                    if(dataBuffer.Count != dataLength)
                    {
                        return true;
                    }
                    dataToSend = dataBuffer.ToArray();
                    dataBuffer.Clear();
                }

                owner.SendData((int)connectionId, new NetworkAddress(remoteAddress, remotePort.Value), dataToSend);
                return false;
            }

            public void Reset()
            {
                dataMode = null;
                connectionId = null;
                dataLength = null;
                remoteAddress = null;
                remotePort = null;
                dataToSend = null;
                
                dataBuffer.Clear();
            }

            private bool AggregateAndTryParseInt(byte value, ref int? number)
            {
                if(value == ',')
                {
                    var str = Encoding.ASCII.GetString(dataBuffer.ToArray());
                    dataBuffer.Clear();

                    if(!int.TryParse(str, out var parsed))
                    {
                        owner.ErrorLog("String '{0}' is not a valid number", str);
                        return false;
                    }

                    number = parsed;
                    return true;
                }
                else
                {
                    dataBuffer.Add(value);
                    return true;
                }
            }

            private int bytesToSkip;
            private DataMode? dataMode;
            private int? connectionId;
            private int? dataLength;
            private string remoteAddress;
            private ushort? remotePort;
            private byte[] dataToSend;

            private readonly List<byte> dataBuffer;

            private readonly DA16200 owner;

            private enum DataMode : byte
            {
                S = (byte)'S',
                M = (byte)'M',
                H = (byte)'H',
            }
        }

        private enum WiFiMode
        {
            Station = 0,
            SoftAccessPoint = 1,
        }

        private enum SecurityProtocol
        {
            Open = 0,
            WEP = 1,
            WPA = 2,
            WPA2 = 3,
            WPA_WPA2 = 4,
            WPA3_OWE = 5,
            WPA3_SAE = 6,
            WPA2_RSN_WPA3_SAE = 7,
        }

        private enum ConnectionType
        {
            TCPServer,
            TCPClient,
            UDP,
        }
    }
}
