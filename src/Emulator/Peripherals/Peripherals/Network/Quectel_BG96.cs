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

        protected override string Vendor => "Quectel";
        protected override string ModelName => "BG96";
        protected override string Revision => "Revision: BG96MAR01A01M1G";
        protected override string ManufacturerRevision => "BG96MAR01A01M1G";
        protected override string SoftwareRevision => "01.008.01.008";

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
    }
}
