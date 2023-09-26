//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
#pragma warning disable 649, 169
    public struct ConfigurationDescriptorHeader
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte ConfDescContinue;
        [PacketField, Offset(bytes: 0x03)]
        public byte BootEnable;
        [PacketField, Offset(bytes: 0x04)]
        public byte DescrAccessEn;
        [PacketField, Offset(bytes: 0x05)]
        public byte InitPowerMode;
        [PacketField, Offset(bytes: 0x06)]
        public byte HighPriorityLUN;
        [PacketField, Offset(bytes: 0x07)]
        public byte SecureRemovalType;
        [PacketField, Offset(bytes: 0x08)]
        public byte InitActiveICCLevel;
        [PacketField, Offset(bytes: 0x09)]
        public ushort PeriodicRTCUpdate;
        [PacketField, Offset(bytes: 0x0b)]
        private byte ReservedHostPerformanceBooster ;
        [PacketField, Offset(bytes: 0x0c)]
        public byte RPMBRegionEnable;
        [PacketField, Offset(bytes: 0x0d)]
        public byte RPMBRegion1Size;
        [PacketField, Offset(bytes: 0x0e)]
        public byte RPMBRegion2Size;
        [PacketField, Offset(bytes: 0x0f)]
        public ushort RPMBRegion3Size;
        [PacketField, Offset(bytes: 0x10)]
        public byte WriteBoosterBufferPreserveUserSpaceEn;
        [PacketField, Offset(bytes: 0x11)]
        public byte WriteBoosterBufferType;
        [PacketField, Offset(bytes: 0x12)]
        public uint NumSharedWriteBoosterBufferAllocUnits;
    }

    public struct DeviceDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte Device;
        [PacketField, Offset(bytes: 0x03)]
        public byte DeviceClass;
        [PacketField, Offset(bytes: 0x04)]
        public byte DeviceSubClass;
        [PacketField, Offset(bytes: 0x05)]
        public byte Protocol;
        [PacketField, Offset(bytes: 0x06)]
        public byte NumberLU;
        [PacketField, Offset(bytes: 0x07)]
        public byte NumberWLU;
        [PacketField, Offset(bytes: 0x08)]
        public byte BootEnable;
        [PacketField, Offset(bytes: 0x09)]
        public byte DescrAccessEn;
        [PacketField, Offset(bytes: 0xa)]
        public byte InitPowerMode;
        [PacketField, Offset(bytes: 0xb)]
        public byte HighPriorityLUN;
        [PacketField, Offset(bytes: 0xc)]
        public byte SecureRemovalType;
        [PacketField, Offset(bytes: 0xd)]
        public byte SecurityLU;
        [PacketField, Offset(bytes: 0xe)]
        public byte BackgroundOpsTermLat;
        [PacketField, Offset(bytes: 0xf)]
        public byte InitActiveICCLevel;
        [PacketField, Offset(bytes: 0x10)]
        public ushort SpecVersion;
        [PacketField, Offset(bytes: 0x12)]
        public ushort ManufactureDate;
        [PacketField, Offset(bytes: 0x14)]
        public byte ManufacturerName;
        [PacketField, Offset(bytes: 0x15)]
        public byte ProductName;
        [PacketField, Offset(bytes: 0x16)]
        public byte SerialNumber;
        [PacketField, Offset(bytes: 0x17)]
        public byte OemID;
        [PacketField, Offset(bytes: 0x18)]
        public ushort ManufacturerID;
        [PacketField, Offset(bytes: 0x1a)]
        public byte UD0BaseOffset;
        [PacketField, Offset(bytes: 0x1b)]
        public byte UDConfigPLength;
        [PacketField, Offset(bytes: 0x1c)]
        public byte DeviceRTTCap;
        [PacketField, Offset(bytes: 0x1d)]
        public ushort PeriodicRTCUpdate;
        [PacketField, Offset(bytes: 0x1f)]
        public byte UFSFeaturesSupport;
        [PacketField, Offset(bytes: 0x20)]
        public byte FFUTimeout;
        [PacketField, Offset(bytes: 0x21)]
        public byte QueueDepth;
        [PacketField, Offset(bytes: 0x22)]
        public ushort DeviceVersion;
        [PacketField, Offset(bytes: 0x24)]
        public byte NumSecureWPArea;
        [PacketField, Offset(bytes: 0x25)]
        public uint PSAMaxDataSize;
        [PacketField, Offset(bytes: 0x29)]
        public byte PSAStateTimeout;
        [PacketField, Offset(bytes: 0x2a)]
        public byte ProductRevisionLevel;
        [PacketField, Offset(bytes: 0x2b), Width(5)]
        private byte[] Reserved0;
        [PacketField, Offset(bytes: 0x30), Width(16)]
        private byte[] ReservedUnifiedMemoryExtension;
        [PacketField, Offset(bytes: 0x40), Width(3)]
        private byte[] ReservedHostPerformanceBooster;
        [PacketField, Offset(bytes: 0x43), Width(12)]
        private byte[] Reserved1;
        [PacketField, Offset(bytes: 0x4f)]
        public uint ExtendedUFSFeaturesSupport;
        [PacketField, Offset(bytes: 0x53)]
        public byte WriteBoosterBufferPreserveUserSpaceEn;
        [PacketField, Offset(bytes: 0x54)]
        public byte WriteBoosterBufferType;
        [PacketField, Offset(bytes: 0x55)]
        public uint NumSharedWriteBoosterBufferAllocUnits;
    }

    public struct DeviceHealthDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte PreEOLInfo;
        [PacketField, Offset(bytes: 0x03)]
        public byte DeviceLifeTimeEstA;
        [PacketField, Offset(bytes: 0x04)]
        public byte DeviceLifeTimeEstB;
        [PacketField, Offset(bytes: 0x05), Width(32)]
        public byte[] VendorPropInfo;
        [PacketField, Offset(bytes: 0x25)]
        public uint RefreshTotalCount;
        [PacketField, Offset(bytes: 0x29)]
        public uint RefreshProgress;
    }

    public struct GeometryDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte MediaTechnology;
        [PacketField, Offset(bytes: 0x03)]
        private byte Reserved0;
        [PacketField, Offset(bytes: 0x04)]
        public ulong TotalRawDeviceCapacity;
        [PacketField, Offset(bytes: 0x0c)]
        public byte MaxNumberLU;
        [PacketField, Offset(bytes: 0x0d)]
        public uint SegmentSize;
        [PacketField, Offset(bytes: 0x11)]
        public byte AllocationUnitSize;
        [PacketField, Offset(bytes: 0x12)]
        public byte MinAddrBlockSize;
        [PacketField, Offset(bytes: 0x13)]
        public byte OptimalReadBlockSize;
        [PacketField, Offset(bytes: 0x14)]
        public byte OptimalWriteBlockSize;
        [PacketField, Offset(bytes: 0x15)]
        public byte MaxInBufferSize;
        [PacketField, Offset(bytes: 0x16)]
        public byte MaxOutBufferSize;
        [PacketField, Offset(bytes: 0x17)]
        public byte RPMBReadWriteSize;
        [PacketField, Offset(bytes: 0x18)]
        public byte DynamicCapacityResourcePolicy;
        [PacketField, Offset(bytes: 0x19)]
        public byte DataOrdering;
        [PacketField, Offset(bytes: 0x1a)]
        public byte MaxContexIDNumber;
        [PacketField, Offset(bytes: 0x1b)]
        public byte SysDataTagUnitSize;
        [PacketField, Offset(bytes: 0x1c)]
        public byte SysDataTagResSize;
        [PacketField, Offset(bytes: 0x1d)]
        public byte SupportedSecRTypes;
        [PacketField, Offset(bytes: 0x1e)]
        public ushort SupportedMemoryTypes;
        [PacketField, Offset(bytes: 0x20)]
        public uint SystemCodeMaxNAllocU;
        [PacketField, Offset(bytes: 0x24)]
        public ushort SystemCodeCapAdjFac;
        [PacketField, Offset(bytes: 0x26)]
        public uint NonPersistMaxNAllocU;
        [PacketField, Offset(bytes: 0x2a)]
        public ushort NonPersistCapAdjFac;
        [PacketField, Offset(bytes: 0x2c)]
        public uint Enhanced1MaxNAllocU;
        [PacketField, Offset(bytes: 0x30)]
        public ushort Enhanced1CapAdjFac;
        [PacketField, Offset(bytes: 0x32)]
        public uint Enhanced2MaxNAllocU;
        [PacketField, Offset(bytes: 0x36)]
        public ushort Enhanced2CapAdjFac;
        [PacketField, Offset(bytes: 0x38)]
        public uint Enhanced3MaxNAllocU;
        [PacketField, Offset(bytes: 0x3c)]
        public ushort Enhanced3CapAdjFac;
        [PacketField, Offset(bytes: 0x3e)]
        public uint Enhanced4MaxNAllocU;
        [PacketField, Offset(bytes: 0x42)]
        public ushort Enhanced4CapAdjFac;
        [PacketField, Offset(bytes: 0x44)]
        public uint OptimalLogicalBlockSize;
        [PacketField, Offset(bytes: 0x48), Width(5)]
        private byte[] ReservedHostPerformanceBooster;
        [PacketField, Offset(bytes: 0x4d)]
        private ushort Reserved1;
        [PacketField, Offset(bytes: 0x4f)]
        public uint WriteBoosterBufferMaxNAllocUnits;
        [PacketField, Offset(bytes: 0x53)]
        public byte DeviceMaxWriteBoosterLUs;
        [PacketField, Offset(bytes: 0x54)]
        public byte WriteBoosterBufferCapAdjFac;
        [PacketField, Offset(bytes: 0x55)]
        public byte SupportedWriteBoosterBufferUserSpaceReductionTypes;
        [PacketField, Offset(bytes: 0x56)]
        public byte SupportedWriteBoosterBufferTypes;
    }

    public struct InterconnectDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public uint BCDUniproVersion;
        [PacketField, Offset(bytes: 0x04)]
        public uint BCDMphyVersion;
    }

    public struct PowerParametersDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02), Width(32)]
        public byte[] ActiveICCLevelsVCC; // 16 ushort fields
        [PacketField, Offset(bytes: 0x22), Width(32)]
        public byte[] ActiveICCLevelsVCCQ; // 16 ushort fields
        [PacketField, Offset(bytes: 0x42), Width(32)]
        public byte[] ActiveICCLevelsVCCQ2; // 16 ushort fields
    }

    public struct RPMBUnitDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte UnitIndex;
        [PacketField, Offset(bytes: 0x03)]
        public byte LUEnable;
        [PacketField, Offset(bytes: 0x04)]
        public byte BootLunID;
        [PacketField, Offset(bytes: 0x05)]
        public byte LUWriteProtect;
        [PacketField, Offset(bytes: 0x06)]
        public byte LUQueueDepth;
        [PacketField, Offset(bytes: 0x07)]
        public byte PSASensitive;
        [PacketField, Offset(bytes: 0x08)]
        public byte MemoryType;
        [PacketField, Offset(bytes: 0x09)]
        public byte RPMBRegionEnable;
        [PacketField, Offset(bytes: 0x0a)]
        public byte LogicalBlockSize;
        [PacketField, Offset(bytes: 0x0b)]
        public ulong LogicalBlockCount;
        [PacketField, Offset(bytes: 0x13)]
        public byte RPMBRegion0Size;
        [PacketField, Offset(bytes: 0x14)]
        public byte RPMBRegion1Size;
        [PacketField, Offset(bytes: 0x15)]
        public byte RPMBRegion2Size;
        [PacketField, Offset(bytes: 0x16)]
        public byte RPMBRegion3Size;
        [PacketField, Offset(bytes: 0x17)]
        public byte ProvisioningType;
        [PacketField, Offset(bytes: 0x18)]
        public ulong PhyMemResourceCount;
        [PacketField, Offset(bytes: 0x20), Width(3)]
        private byte[] Reserved;
    }

    public struct UnitDescriptor
    {
        [PacketField, Offset(bytes: 0x00)]
        public byte Length;
        [PacketField, Offset(bytes: 0x01)]
        public byte DescriptorIDN;
        [PacketField, Offset(bytes: 0x02)]
        public byte UnitIndex;
        [PacketField, Offset(bytes: 0x03)]
        public byte LUEnable;
        [PacketField, Offset(bytes: 0x04)]
        public byte BootLunID;
        [PacketField, Offset(bytes: 0x05)]
        public byte LUWriteProtect;
        [PacketField, Offset(bytes: 0x06)]
        public byte LUQueueDepth;
        [PacketField, Offset(bytes: 0x07)]
        public byte PSASensitive;
        [PacketField, Offset(bytes: 0x08)]
        public byte MemoryType;
        [PacketField, Offset(bytes: 0x09)]
        public byte DataReliability;
        [PacketField, Offset(bytes: 0x0a)]
        public byte LogicalBlockSize;
        [PacketField, Offset(bytes: 0x0b)]
        public ulong LogicalBlockCount;
        [PacketField, Offset(bytes: 0x13)]
        public uint EraseBlockSize;
        [PacketField, Offset(bytes: 0x17)]
        public byte ProvisioningType;
        [PacketField, Offset(bytes: 0x18)]
        public ulong PhyMemResourceCount;
        [PacketField, Offset(bytes: 0x20)]
        public ushort ContextCapabilities;
        [PacketField, Offset(bytes: 0x22)]
        public byte LargeUnitGranularity_M1;
        [PacketField, Offset(bytes: 0x23), Width(6)]
        private byte[] ReservedHostPerformanceBooster;
        [PacketField, Offset(bytes: 0x29)]
        public uint LUNumWriteBoosterBufferAllocUnits;
    }
#pragma warning restore 649, 169
}