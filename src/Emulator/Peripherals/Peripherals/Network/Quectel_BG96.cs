//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BG96 : QuectelModem
    {
        public Quectel_BG96(Machine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
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
