//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC660K : QuectelModem
    {
        public Quectel_BC660K(Machine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
        }

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        protected override Response Qcfg(string function, int value)
        {
            switch(function)
            {
                case "dsevent":
                    deepSleepEventEnabled = value != 0;
                    break;
                case "DataInactTimer": // inactivity timer
                case "EPCO": // extended protocol configuration options
                case "faultaction": // action performed by the UE after an error occurs
                case "GPIO": // GPIO status
                case "logbaudrate": // baud rate
                case "MacRAI": // enable or disable RAI in MAC layer
                case "NBcategory": // UE category
                case "NcellMeas": // NcellMeas
                case "OOSScheme": // network searching mechanism in OOS
                case "relversion": // protocol release version
                case "SimBip": // enable or disable SIMBIP
                case "slplocktimes": // sleep duration
                case "statisr": // report interval of the statistics URC
                case "wakeupRXD": // whether the UE can be woken up by RXD
                    this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, not implemented", function, value);
                    break;
                default:
                    return base.Qcfg(function, value);
            }
            return Ok;
        }

        // QCGDEFCONT - Set Default PSD Connection Settings
        [AtCommand("AT+QCGDEFCONT", CommandType.Write)]
        protected virtual Response Qcgdefcont(PdpType pdpType, string apn = "", string username = "",
            string password = "", AuthenticationType authenticationType = AuthenticationType.None)
        {
            pdpContextApn = apn;
            return Ok; // stub
        }

        // QICFG - Configure Optional TCP/IP Parameters
        [AtCommand("AT+QICFG", CommandType.Write)]
        protected override Response Qicfg(string parameter, params int[] args)
        {
            if(args.Length < 1)
            {
                return Error;
            }

            switch(parameter)
            {
                case "dataformat":
                    if(args.Length < 2)
                    {
                        return Error;
                    }
                    // sendDataFormat only applies to sending in non-data mode, which is not
                    // currently implemented
                    sendDataFormat = args[0] != 0 ? DataFormat.Hex : DataFormat.Text;
                    receiveDataFormat = args[1] != 0 ? DataFormat.Hex : DataFormat.Text;
                    break;
                case "showRA": // display the address of the remote end while displaying received data
                    this.Log(LogLevel.Warning, "TCP/IP config value '{0}' set to {1}, not implemented", parameter, args.Stringify());
                    break;
                default:
                    return base.Qicfg(parameter, args);
            }
            return Ok;
        }

        // QIRD - Retrieve the Received TCP/IP Data
        // Not supported
        protected override Response Qird(int connectionId, int readLength) => Error;

        // QNBIOTRAI - NB-IoT Release Assistance Indication
        [AtCommand("AT+QNBIOTRAI", CommandType.Write)]
        protected override Response Qnbiotrai(int raiMode = 0)
        {
            if(raiMode > 1)
            {
                return Error;
            }

            return base.Qnbiotrai(raiMode);
        }

        protected override bool IsValidContextId(int id)
        {
            return id == 0;
        }

        protected override string Vendor => "Quectel_Ltd";
        protected override string ModelName => "Quectel_BC660K-GL";
        protected override string Revision => "Revision: QCX212";
        protected override string ManufacturerRevision => "BC660KGLAAR01A03";
        protected override string SoftwareRevision => "01.002.01.002";

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
    }
}
