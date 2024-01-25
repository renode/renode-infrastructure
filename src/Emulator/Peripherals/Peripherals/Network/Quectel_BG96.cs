//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BG96 : QuectelModem
    {
        public Quectel_BG96(IMachine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
        }

        // CEREG - EPS Network Registration Status
        [AtCommand("AT+CEREG", CommandType.Write)]
        protected override Response CeregWrite(NetworkRegistrationUrcType type)
        {
            // EMM cause information is not supported on this modem
            if(type == NetworkRegistrationUrcType.StatLocationEmmCause ||
                type == NetworkRegistrationUrcType.StatLocationEmmCausePsm)
            {
                return Error;
            }
            return base.CeregWrite(type);
        }

        // CREG - Network Registration
        [AtCommand("AT+CREG", CommandType.Write)]
        protected override Response CregWrite(NetworkRegistrationUrcType type)
        {
            if(type > NetworkRegistrationUrcType.StatLocation)
            {
                this.Log(LogLevel.Warning, "AT+CREG: Argument <n> set to {0}, not supported by this modem", (int)type);
                return Error;
            }
            return base.CregWrite(type);
        }

        // CESQ - Extended Signal Quality
        // CESQ is not supported by this modem. We override Cesq without marking it with [AtCommand]
        // in order to remove it from the command set (including the test command, AT+CESQ=?).
        protected override Response Cesq() => Error;

        // CGDCONT - Define PDP Context
        [AtCommand("AT+CGDCONT", CommandType.Read)]
        protected override Response Cgdcont() => Ok.WithParameters($"+CGDCONT: 1,\"IP\",\"{pdpContextApn}\",\"{NetworkIp}\",0,0"); // stub

        // CGMI - Request Manufacturer Identification
        [AtCommand("AT+CGMI")]
        protected override Response Cgmi() => Ok.WithParameters(Vendor);

        // CGSN - Request Product Serial Number
        // CGSN only supports reading the IMEI on this modem and is available only as an execution command
        // Also, it returns only the IMEI itself instead of "+CGSN: <imei>"
        [AtCommand("AT+CGSN")]
        protected override Response Cgsn() => Ok.WithParameters(imeiNumber);

        [AtCommand("AT+CGSN", CommandType.Write)]
        protected override Response CgsnWrite(SerialNumberType serialNumberType = SerialNumberType.Device) => Error;

        // CMEE - Report Mobile Termination Error
        [AtCommand("AT+CMEE", CommandType.Write)]
        protected override Response Cmee(MobileTerminationResultCodeMode mode = MobileTerminationResultCodeMode.Numeric)
        {
            return base.Cmee(mode);
        }

        // CSCON - Signaling Connection Status
        // Not supported
        protected override Response Cscon(int enable = 0) => Error;

        // QBAND - Get and Set Mobile Operation Band
        // Not supported
        protected override Response Qband(int numberOfBands, params int[] bands) => Error;

        // QCCID - USIM Card Identification
        // Not supported
        protected override Response Qccid() => Error;

        // CCLK - Set and Get Current Date and Time
        [AtCommand("AT+CCLK", CommandType.Read)]
        protected virtual Response CclkRead()
        {
            return Ok.WithParameters("+CCLK: " + machine.RealTimeClockDateTime.ToString("yy/MM/dd,HH:mm:sszz").SurroundWith("\""));
        }

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        protected override Response Qcfg(string function, params int[] args)
        {
            switch(function)
            {
                case "ledmode": // NETLIGHT output Mode
                {
                    if(args.Length == 1)
                    {
                        return SetNetLightMode(args[0]);
                    }
                    return base.Qcfg(function, args);
                }
                case "apready": // AP_READY Pin
                case "band": // band configuration
                case "celevel": // get LTE Cat NB1 coverage enhancement level
                case "cmux/urcport": // URC output port for CMUX
                case "ims": // IMS function control
                case "iotopmode": // network category to be searched under LTE RAT
                case "msc": // MSC release version configuration
                case "nb1/bandprior": // band scan priority under LTE Cat NB1
                case "nwscanmode": // RAT(s) to be searched
                case "nwscanseq": // RAT searching sequence
                case "pdp/duplicatechk": // establish multi PDNs with the same APN
                case "psm/enter": // trigger PSM immediately
                case "psm/urc": // enable/disable PSM entry indication
                case "risignaltype": // RI signal output carrier
                case "roamservice": // roam service configuration
                case "servicedomain": // service domain configuration
                case "sgsn": // SGSN release version configuration
                case "urc/delay": // delay URC indication
                case "urc/ri/other": // RI behavior when other URCs are presented
                case "urc/ri/ring": // RI behavior when RING URC is presented
                case "urc/ri/smsincoming": // RI behavior when incoming SMS URCs are presented
                    this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, not implemented", function, string.Join(", ", args));
                    break;
                default:
                    return base.Qcfg(function, args);
            }
            return Ok;
        }

        // QENG - Engineering Mode
        // Not supported
        protected override Response Qeng(int mode) => Error;

        // QIACT - Activate a PDP Context
        [AtCommand("AT+QIACT", CommandType.Write)]
        protected Response Qiact(int contextId)
        {
            if(!IsValidContextId(contextId))
            {
                return Error;
            }
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
                    sendDataFormat = args[0] != 0 ? DataFormat.Hex : DataFormat.Text;
                    receiveDataFormat = args[1] != 0 ? DataFormat.Hex : DataFormat.Text;
                    break;
                case "viewmode":
                    dataOutputSeparator = args[0] != 0 ? "," : CrLf;
                    break;
                case "transpktsize": // packet size for transparent mode
                case "transwaittm": // wait time for transparent mode
                case "tcp/retranscfg": //  maximum interval time and number for TCP retransmissions
                case "dns/cache": // enable the DNS cache
                case "qisend/timeout": // input data timeout
                case "passiveclosed": // passive close of TCP connection when the server is closed
                    this.Log(LogLevel.Warning, "TCP/IP config value '{0}' set to {1}, not implemented", parameter, args.Stringify());
                    break;
                default:
                    return base.Qicfg(parameter, args);
            }
            return Ok;
        }

        // QICLOSE - Close a Socket Service
        [AtCommand("AT+QICLOSE", CommandType.Write)]
        protected /* override */ Response Qiclose(int connectionId, int timeout = 10)
        {
            return base.Qiclose(connectionId);
        }

        // QICSGP - Configure Parameters of a TCP/IP Context
        [AtCommand("AT+QICSGP", CommandType.Write)]
        protected virtual Response Qicsgp(int contextId, ProtocolType contextType = ProtocolType.IpV4,
            string apn = "", string username = "", string password = "",
            AuthenticationMethod authenticationType = AuthenticationMethod.None)
        {
            if(!IsValidContextId(contextId))
            {
                return Error;
            }
            return Ok; // stub
        }

        // QIDEACT - Deactivate a PDP Context
        [AtCommand("AT+QIDEACT", CommandType.Write)]
        protected virtual Response Qideact(int contextId)
        {
            if(!IsValidContextId(contextId))
            {
                return Error;
            }
            return Ok; // stub
        }

        // QISEND - Send Hex/Text String Data
        [AtCommand("AT+QISEND", CommandType.Write)]
        protected override Response Qisend(int connectionId, int? sendLength = null, string data = null, int? raiMode = null)
        {
            // The BG96 doesn't support non-data mode in AT+QISEND
            if(data != null)
            {
                return Error;
            }

            return base.Qisend(connectionId, sendLength, data);
        }

        // QNBIOTEVENT - Enable/Disable NB-IoT Related Event Report
        // Not supported
        protected override Response Qnbiotevent(int enable = 0, int eventType = 1) => Error;

        // QNBIOTRAI - NB-IoT Release Assistance Indication
        // Not supported
        protected override Response Qnbiotrai(int raiMode = 0) => Error;

        // QRST - Module Reset
        // Not supported
        protected override Response QrstWrite(int mode = 1) => Error;

        protected override string Vendor => "Quectel";
        protected override string ModelName => "BG96";
        protected override string Revision => "Revision: BG96MAR01A01M1G";
        protected override string ManufacturerRevision => "BG96MAR01A01M1G";
        protected override string SoftwareRevision => "01.008.01.008";

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";

        protected enum ProtocolType
        {
            IpV4 = 1,
            IpV4V6,
        }

        protected enum AuthenticationMethod
        {
            None,
            Pap,
            Chap,
            PapOrChap,
        }
    }
}
