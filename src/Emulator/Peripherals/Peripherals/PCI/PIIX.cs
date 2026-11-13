//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.PCI.BAR;

namespace Antmicro.Renode.Peripherals.PCI
{
    //This is a  implementation of `Intel 82371SB`
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class PIIX : PCIeEndpoint
    {
        public PIIX(PCIHost_Bridge parent) : base()
        {
            VendorId = 0x8086;
            DeviceId = 0x7010;
            BaseClassCode = PCIBaseClass.Storage;
            SubClassCode = PCISubClass.StorageIDE;

            for(var i = 0; i < HeaderType.MaxNumberOfBARs(); ++i)
            {
                AddBaseAddressRegister(parent, (uint)i, new IOBaseAddressRegister(BARSizes[i]));
            }
        }

        private readonly uint[] BARSizes = {0, 0, 0, 0, 0, 0x10};
    }
}