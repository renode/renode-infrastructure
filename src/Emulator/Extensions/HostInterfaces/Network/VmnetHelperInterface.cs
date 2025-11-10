#if PLATFORM_OSX && NET
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Antmicro.Renode.Config;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.HostInterfaces.Network
{
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
                catch (Exception ex)
                {
                    throw new RecoverableException("Failed to decode UTF-8 data.", ex);
                }

                object interfaceDescriptionJson;
                try
                {
                    interfaceDescriptionJson = SimpleJson.DeserializeObject(interfaceDescriptionString);
                }
                catch(SerializationException se)
                {
                    throw new RecoverableException("Invalid interface configuration: failed to parse JSON.", se);
                }

                if(!(interfaceDescriptionJson is IDictionary<string, object> dict))
                {
                    throw new RecoverableException("Invalid interface configuration: unexpected response format.");
                }

                var macString = dict[MACKey];

                if(!(macString is string))
                {
                    throw new RecoverableException("Invalid interface configuration: MAC value is not a string.");
                }
                if(!MACAddress.TryParse((string)macString, out var mac))
                {
                    throw new RecoverableException("Invalid interface configuration: failed to parse MAC address.");
                }
                socketInterface.MAC = mac;
                socketInterface.InfoLog("MAC address set to {0}", mac);
            }
        }

        private const string MACKey = "vmnet_mac_address";

        private const int JsonLength = 1000;
    }
}
#endif