//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BG96 : QuectelModem
    {
        public Quectel_BG96(Machine machine, string imeiNumber = DefaultImeiNumber,
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
                case "apready": // AP_READY Pin
                case "band": // band configuration
                case "celevel": // get LTE Cat NB1 coverage enhancement level
                case "cmux/urcport": // URC output port for CMUX
                case "ims": // IMS function control
                case "iotopmode": // network category to be searched under LTE RAT
                case "ledmode": // NETLIGHT output Mode
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
                    this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, not implemented", function, value);
                    break;
                default:
                    return base.Qcfg(function, value);
            }
            return Ok;
        }

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
