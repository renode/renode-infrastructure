//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI.Capabilities
{
    public class PCIeCapability : Capability
    {
        public PCIeCapability(PCIeBasePeripheral parent) : base(parent, 0x10, 0x3C)
        {
            Registers.Add(new DoubleWordRegister(parent)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => Id, name: "PCI Express Cap ID")
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => NextCapability, name: "Next Cap Pointer")
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0x2 /* current specification */, name: "Capability Version")
                .WithEnumField(20, 4, FieldMode.Read, valueProviderCallback: (DeviceType _) => parent.HeaderType.HasFlag(HeaderType.Bridge) ? DeviceType.RootComplexIntegratedEndpoint : DeviceType.PCIExpressEndpoint, name: "Device/Port Type") // this is a guess and an approximation to the two types of devices we have
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => false, name: "Slot Implemented")
                .WithTag("Interrupt Message Number", 25, 5)
                .WithReservedBits(30, 2)
                );
        }

        private enum DeviceType
        {
            PCIExpressEndpoint = 0x0,
            LegacyPCIExpressEndpoint = 0x1,
            RootPortOfPCIExpressRootComplex = 0x4,
            UpstreamPortOfPCIExpressSwitch = 0x5,
            DownstreamPortOfPCIExpressSwitch = 0x6,
            PCIExpressToPCIBridge = 0x7,
            PCIToPCIExpressBridge = 0x8,
            RootComplexIntegratedEndpoint = 0x9,
            RootComplexEventCollector = 0xA,
        }
    }
}