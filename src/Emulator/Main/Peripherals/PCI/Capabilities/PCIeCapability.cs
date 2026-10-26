//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.PCI.Capabilities
{
    // Note: implementors of this class shall permit 16 bit access in addition to 32 bit access
    public class PCIeCapability : Capability
    {
        public PCIeCapability(PCIeBasePeripheral parent) : base(parent, PCIeCapabilityId, PCIeCapabilitySize)
        {
            Registers.Add((long)PCIeCapabilityRegisters.PCIExpressCapabilitiesRegister, new DoubleWordRegister(parent)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => Id, name: "PCI Express Cap ID")
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => NextCapability, name: "Next Cap Pointer")
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0x2 /* current specification */, name: "Capability Version")
                .WithEnumField(20, 4, FieldMode.Read, valueProviderCallback: (DeviceType _) => parent.HeaderType.HasFlag(HeaderType.Bridge) ? DeviceType.RootPortOfPCIExpressRootComplex : DeviceType.PCIExpressEndpoint, name: "Device/Port Type") // this is a guess and an approximation to the two types of devices we have
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => false, name: "Slot Implemented")
                .WithTag("Interrupt Message Number", 25, 5)
                .WithReservedBits(30, 1)
                .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => false, name: "Flit Mode Supported ")
            );

            Registers.Add((long)PCIeCapabilityRegisters.LinkStatusAndControl, new DoubleWordRegister(parent)
                // the rest not implemented for now
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => currentLinkSpeed, name: "Current Link Speed")
                .WithValueField(20, 6, FieldMode.Read, valueProviderCallback: _ => negotiatedLinkWidth, name: "Negotiated Link Width")
            );

            Registers.Add((long)PCIeCapabilityRegisters.LinkCapabilities2, new DoubleWordRegister(parent)
                .WithReservedBits(0, 1)
                .WithValueField(1, 7, FieldMode.Read, valueProviderCallback: _ => supportedLinkSpeeds, name: "Supported Link Speeds")
            // the rest not implemented for now
            );

            Registers.Add((long)PCIeCapabilityRegisters.LinkCapabilities, new DoubleWordRegister(parent)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => maxLinkSpeed, name: "Max Link Speed")
                .WithValueField(4, 6, FieldMode.Read, valueProviderCallback: _ => maxLinkWidth, name: "Max Link Width")
            // the rest not implemented for now
            );
        }

        public void SetSupportedLinkSpeeds(PCIeLinkSpeeds speeds)
        {
            supportedLinkSpeeds = (ulong)speeds;
        }

        public void SetCurrentLinkSpeed(PCIeLinkSpeeds speeds)
        {
            if(!EnsureSpeedIsValid(speeds, out var currentSpeed))
            {
                return;
            }
            if(SpeedToCurrentSpeed((PCIeLinkSpeeds)currentSpeed) > maxLinkSpeed)
            {
                parent.ErrorLog("Speed: {0} is greater than the maximum: {1} supported in this device", currentSpeed, maxLinkSpeed);
                return;
            }
            currentLinkSpeed = SpeedToCurrentSpeed((PCIeLinkSpeeds)currentSpeed);
        }

        public void SetMaximumLinkSpeed(PCIeLinkSpeeds speeds)
        {
            if(!EnsureSpeedIsValid(speeds, out var currentSpeed))
            {
                return;
            }
            maxLinkSpeed = SpeedToCurrentSpeed((PCIeLinkSpeeds)currentSpeed);
        }

        public void SetMaximumLinkWidth(uint width)
        {
            if(!EnsureWidthIsValid(width))
            {
                return;
            }
            maxLinkWidth = (ulong)width;
        }

        public void SetNegotiatedLinkWidth(uint width)
        {
            if(!EnsureWidthIsValid(width))
            {
                return;
            }
            if((ulong)width > maxLinkWidth)
            {
                parent.ErrorLog("Width: {0} is greater than the maximum: {1} supported in this device", width, maxLinkSpeed);
                return;
            }
            negotiatedLinkWidth = (ulong)width;
        }

        private bool EnsureSpeedIsValid(PCIeLinkSpeeds speed, out ulong currentSpeed)
        {
            currentSpeed = 0;
            if(!BitHelper.IsOneHot((ulong)speed))
            {
                parent.ErrorLog("Speed {0} is not one-hot encoded", speed);
                return false;
            }
            currentSpeed = supportedLinkSpeeds & (ulong)speed;
            if(currentSpeed == 0)
            {
                parent.ErrorLog("Speed {0} is not supported in this device", speed);
                return false;
            }
            return true;
        }

        private bool EnsureWidthIsValid(uint width)
        {
            if(!BitHelper.IsOneHot(width))
            {
                parent.ErrorLog("Width {0} is not one-hot encoded", width);
                return false;
            }
            if(width > 16)
            {
                parent.ErrorLog("Link width of {0} is not supported in this device", width);
                return false;
            }
            return true;
        }

        private ulong SpeedToCurrentSpeed(PCIeLinkSpeeds speed) => speed switch
        {
            PCIeLinkSpeeds.GTs_2_5 => 1,
            PCIeLinkSpeeds.GTs_5_0 => 2,
            PCIeLinkSpeeds.GTs_8_0 => 3,
            PCIeLinkSpeeds.GTs_16_0 => 4,
            PCIeLinkSpeeds.GTs_32_0 => 5,
            PCIeLinkSpeeds.GTs_64_0 => 6,
            _ => throw new UnreachableException(),
        };

        private ulong maxLinkWidth;
        private ulong currentLinkSpeed;
        private ulong maxLinkSpeed;
        private ulong negotiatedLinkWidth;
        private ulong supportedLinkSpeeds;

        private const uint PCIeCapabilitySize = 0x3C;
        private const byte PCIeCapabilityId = 0x10;

        public enum PCIeCapabilityRegisters
        {
            PCIExpressCapabilitiesRegister = 0x0,
            DeviceCapabilities = 0x4,
            DeviceStatusAndControl = 0x8,
            LinkCapabilities = 0xC,
            LinkStatusAndControl = 0x10,
            SlotCapabilities = 0x14,
            SlotStatusAndControl = 0x18,
            RootCapabilitiesAndControl = 0x1C,
            RootStatus = 0x1C,
            DeviceCapabilities2 = 0x24,
            DeviceStatusAndControl2 = 0x28,
            LinkCapabilities2 = 0x2C,
            LinkStatusAndControl2 = 0x30,
            SlotCapabilities2 = 0x34,
            SlotStatusAndControl2 = 0x38,
        }

        [Flags]
        public enum PCIeLinkSpeeds
        {
            GTs_2_5 = (1 << 0),
            GTs_5_0 = (1 << 1),
            GTs_8_0 = (1 << 2),
            GTs_16_0 = (1 << 3),
            GTs_32_0 = (1 << 4),
            GTs_64_0 = (1 << 5),
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
