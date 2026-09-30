//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.HostInterfaces.Network
{
    // MacOS-only
    public static class VmnetHelperInterface
    {
        public static async Task ConfigureInterface(SocketInterface socketInterface, bool autoConf)
        {
            // Dummy write to establish connection with vmnet-helper. First packet <64 bytes is ignored.
            var txBuffer = new byte[1];
            await socketInterface.DuplicatedSocket.SendAsync(txBuffer);

            // Configure the socket interface and set MAC address
            if(autoConf)
            {
                var rxBuffer = new byte[JsonLength];
                var bytesRead = await socketInterface.DuplicatedSocket.ReceiveAsync(rxBuffer);
                var interfaceDescriptionString = Encoding.UTF8.GetString(rxBuffer, 0, bytesRead);
                try
                {
                    interfaceDescriptionString = Encoding.UTF8.GetString(rxBuffer, 0, bytesRead);
                }
                catch(Exception ex)
                {
                    throw new RecoverableException("Failed to decode UTF-8 data.", ex);
                }

                AutoConfInterfaceDescription interfaceDescriptionJson;
                try
                {
                    interfaceDescriptionJson = JsonSerializer.Deserialize<AutoConfInterfaceDescription>(interfaceDescriptionString, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });
                }
                catch(SerializationException se)
                {
                    throw new RecoverableException("Invalid interface configuration: failed to parse JSON.", se);
                }

                if(!MACAddress.TryParse(interfaceDescriptionJson.VmnetMacAddress, out var mac))
                {
                    throw new RecoverableException("Invalid interface configuration: failed to parse MAC address.");
                }
                socketInterface.MAC = mac;
                socketInterface.InfoLog("MAC address set to {0}", mac);
            }
        }

        private const int JsonLength = 1000;

        private class AutoConfInterfaceDescription
        {
            [JsonRequired]
            public string VmnetMacAddress { get; set; }
        }
    }
}
