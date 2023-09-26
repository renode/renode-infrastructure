//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Storage.SCSI.Commands
{
    public enum ModePagePolicy : byte
    {
        Shared = 0b00,
        PerTargetPort = 0b01,
        Obsolete = 0b10,
        PerInitiatorTargetNexus = 0b11
    }

#pragma warning disable 649, 169
    public struct StandardInquiryResponse
    {
        [PacketField, Offset(bytes: 0, bits: 0), Width(5)]
        public byte PeripheralDeviceType;
        [PacketField, Offset(bytes: 0, bits: 5), Width(3)]
        public byte PeripheralQualifier;
        [PacketField, Offset(bytes: 1, bits: 7), Width(1)]
        public bool RemovableMedium;
        [PacketField, Offset(bytes: 2)]
        public byte Version;
        [PacketField, Offset(bytes: 3, bits: 0), Width(4)]
        public byte ResponseDataFormat;
        [PacketField, Offset(bytes: 3, bits: 4), Width(1)]
        public bool HierarchicalSupport;
        [PacketField, Offset(bytes: 3, bits: 5), Width(1)]
        public byte NormalACASupported;
        [PacketField, Offset(bytes: 4)]
        public byte AdditionalLength;
        [PacketField, Offset(bytes: 5, bits: 0), Width(1)]
        public bool Protect;
        [PacketField, Offset(bytes: 5, bits: 3), Width(1)]
        public bool ThirdPartyCopy;
        [PacketField, Offset(bytes: 5, bits: 4), Width(2)]
        public byte TargetPortGroupSupport;
        [PacketField, Offset(bytes: 5, bits: 6), Width(1)]
        public bool AccessControlsCoordinator;
        [PacketField, Offset(bytes: 5, bits: 7), Width(1)]
        public bool StorageControllerComponentSupported;
        [PacketField, Offset(bytes: 6, bits: 0), Width(1)]
        public bool ADDR16;
        [PacketField, Offset(bytes: 6, bits: 4), Width(1)]
        public bool MultiPort;
        [PacketField, Offset(bytes: 6, bits: 5), Width(1)]
        public bool VendorSpecific0;
        [PacketField, Offset(bytes: 6, bits: 6), Width(1)]
        public bool EnclosureServices;
        [PacketField, Offset(bytes: 7, bits: 0), Width(1)]
        public bool VendorSpecific1;
        [PacketField, Offset(bytes: 7, bits: 1), Width(1)]
        public bool CommandQueue;
        [PacketField, Offset(bytes: 7, bits: 4), Width(1)]
        public bool Sync;
        [PacketField, Offset(bytes: 7, bits: 5), Width(1)]
        public bool WBUS16;
        [PacketField, Offset(bytes: 8), Width(8)]
        public byte[] VendorIdentification;
        [PacketField, Offset(bytes: 16), Width(16)]
        public byte[] ProductIdentification;
        [PacketField, Offset(bytes: 32), Width(4)]
        public byte[] ProductRevisionLevel;
    }

    public struct VitalProductDataPageHeader
    {
        [PacketField, Offset(bytes: 0, bits: 0), Width(5)]
        public byte PeripheralDeviceType;
        [PacketField, Offset(bytes: 0, bits: 5), Width(3)]
        public byte PeripheralQualifier;
        [PacketField, Offset(bytes: 1)]
        public VitalProductDataPageCode PageCode;
        [PacketField, Offset(bytes: 2)]
        public ushort PageLength;
    }

    public struct ModePagePolicyDescriptor
    {
        [PacketField, Offset(bytes: 0, bits: 0), Width(6)]
        public byte PolicyPageCode;
        [PacketField, Offset(bytes: 1)]
        public byte PolicySubpageCode;
        [PacketField, Offset(bytes: 2, bits: 0), Width(2)]
        public ModePagePolicy ModePagePolicy;
        [PacketField, Offset(bytes: 2, bits: 7), Width(1)]
        public bool MultipleLogicalUnitsShare;
        [PacketField, Offset(bytes: 4)]
        private byte Reserved;
    }

    public struct ReadCapcity10Result
    {
        [PacketField]
        public uint ReturnedLogicalBlockAddress;
        [PacketField]
        public uint BlockLengthInBytes;
    }

    public struct ReadCapacity16ParameterData
    {
        [PacketField, Offset(bytes: 0)]
        public ulong ReturnedLogicalBlockAddress;
        [PacketField, Offset(bytes: 8)]
        public uint LogicalBlockLengthInBytes;
        [PacketField, Offset(bytes: 12, bits: 0), Width(1)]
        public bool ProtectionEnable;
        [PacketField, Offset(bytes: 12, bits: 1), Width(3)]
        public byte ProtectionType;
        [PacketField, Offset(bytes: 13, bits: 0), Width(4)]
        public byte LogicalBlocksPerPhysicalBlockExponent;
        [PacketField, Offset(bytes: 13, bits: 4), Width(4)]
        public byte ProtectionInformationIntervalsExponent;
        [PacketField, Offset(bytes: 14, bits: 0), Width(6)]
        public byte LowestAlignedLogicalBlockAddressMSB;
        [PacketField, Offset(bytes: 14, bits: 6), Width(1)]
        public bool ThinProvisioningReadZero;
        [PacketField, Offset(bytes: 14, bits: 7), Width(1)]
        public bool ThinProvisioningEnable;
        [PacketField, Offset(bytes: 15)]
        public byte LowestAlignedLogicalBlockAddressHighLSB;
        [PacketField, Offset(bytes: 16), Width(16)]
        private byte[] Reserved;
    }
#pragma warning restore 649, 169
}