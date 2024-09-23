//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Core.USB
{
    public static class USBConnectorExtensions
    {
        public static void CreateUSBConnector(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new USBConnector(), name);
        }
    }

    public class USBConnector : IExternal, IConnectable<IUSBDevice>, IUSBDevice
    {
        public void RegisterInController(SimpleContainerBase<IUSBDevice> controller, int address = 1)
        {
            controller.Register(dev, new NumberRegistrationPoint<int>(address));
        }

        public void AttachTo(IUSBDevice obj)
        {
            dev = obj;
        }

        public void DetachFrom(IUSBDevice obj)
        {
            dev = null;
        }

        public void Reset()
        {
        }

        public USBDeviceCore USBCore => dev?.USBCore;

        private IUSBDevice dev;
    }
}
