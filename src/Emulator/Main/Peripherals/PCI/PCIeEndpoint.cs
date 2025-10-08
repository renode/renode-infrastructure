//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.PCI
{
    public abstract class PCIeEndpoint : PCIeBasePeripheral
    {
        //todo: add registers enum
        public PCIeEndpoint(IPCIeRouter parent) : base(parent, HeaderType.Endpoint)
        {
        }
    }
}