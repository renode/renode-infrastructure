//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC66 : QuectelModem
    {
        public Quectel_BC66(Machine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
        }

        protected override bool IsValidContextId(int id)
        {
            return id == 1;
        }

        protected override string Vendor => "Quectel_Ltd";
        protected override string ModelName => "Quectel_BC66";
        protected override string Revision => "Revision: MTK_2625";
        protected override string ManufacturerRevision => "BC66NBR01A01";
        protected override string SoftwareRevision => "01.002.01.002";

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
    }
}
