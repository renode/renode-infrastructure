//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Storage.SCSI.Commands
{
    public enum SCSIStatus : byte
    {
        Good = 0,
        CheckCondition = 1,
        ConditionMet = 2,
        Busy = 3,
        ReservationConflict = 4,
        TaskSetFull = 5,
        AutoContingentAllegianceActive = 6,
        TaskAborted = 7,
    }

    public enum VitalProductDataPageCode : byte
    {
        SupportedVPDPages = 0x00,
        UnitSerialNumber = 0x80,
        DeviceIdentification = 0x83,
        SoftwareInterfaceIdentification = 0x84,
        ManagementNetworkAddresses = 0x85,
        ExtendedInquiryData = 0x86,
        ModePagePolicy = 0x87,
        SCSIPorts = 0x88,
        PowerCondition = 0x8A,
        DeviceConstituents = 0x8B,
        CFAProfileInformation = 0x8C,
        PowerConsumption = 0x8D,
        BlockLimits = 0xB0,
        BlockDeviceCharacteristics = 0xB1,
        LogicalBlockProvisioning = 0xB2,
        Referrals = 0xB3,
        SupportedBlockLengthsAndProtectionTypes = 0xB4,
        BlockDeviceCharacteristicsExtension = 0xB5,
        ZonedBlockDeviceCharacteristics = 0xB6,
        BlockLimitsExtension = 0xB7,
        FirmwareNumbers = 0xC0,
        DateCode = 0xC1,
        JumperSettings = 0xC2,
        DeviceBehavior = 0xC3,
    }

#pragma warning disable 649, 169
    public struct BarrierCommand
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct FormatUnit
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(3)]
        public byte DefectListFormat;
        [PacketField, Offset(bytes: 1, bits: 3), Width(1)]
        public bool CompleteList;
        [PacketField, Offset(bytes: 1, bits: 4), Width(1)]
        public bool FormatData;
        [PacketField, Offset(bytes: 1, bits: 5), Width(1)]
        public byte LongList;
        [PacketField, Offset(bytes: 1, bits: 6), Width(2)]
        public byte FormatProtectionInformation;
        [PacketField, Offset(bytes: 2)]
        public byte VendorSpecific;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct Inquiry
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(1)]
        public bool EnableVitalProductData;
        [PacketField, Offset(bytes: 2)]
        public VitalProductDataPageCode PageCode;
        [PacketField, Offset(bytes: 3)]
        public ushort AllocationLength;
        [PacketField, Offset(bytes: 5)]
        public ushort Control;
    }

    public struct ModeSelect10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(1)]
        public bool SavePages;
        [PacketField, Offset(bytes: 1, bits: 4), Width(1)]
        public bool PageFormat;
        [PacketField, Offset(bytes: 7)]
        public ushort ParameterListLength;
        [PacketField, Offset(bytes: 9)]
        public ushort Control;
    }

    public struct ModeSense10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 3), Width(1)]
        public bool DisableBlockDescriptors;
        [PacketField, Offset(bytes: 1, bits: 4), Width(1)]
        public bool LongLBAAccepted;
        [PacketField, Offset(bytes: 2, bits: 0), Width(6)]
        public bool PageCode;
        [PacketField, Offset(bytes: 2, bits: 6), Width(2)]
        public byte PageControl;
        [PacketField, Offset(bytes: 3)]
        public byte SubpageCode;
        [PacketField, Offset(bytes: 7)]
        public ushort AllocationLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct PreFetch10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1), Width(1)]
        public bool Immediate;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort PrefetchLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct PreFetch16
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1), Width(1)]
        public bool Immediate;
        [PacketField, Offset(bytes: 2)]
        public ulong LogicalBlockAddress;
        [PacketField, Offset(bytes: 10)]
        public uint PrefetchLength;
        [PacketField, Offset(bytes: 14, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct Read6
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(5)]
        public byte LogicalBlockAddressHigh;
        [PacketField, Offset(bytes: 2)]
        public ushort LogicalBlockAddressLow;
        [PacketField, Offset(bytes: 4)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct Read10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool ForceUnitAccessNonVolatile;
        [PacketField, Offset(bytes: 1, bits: 3)]
        public bool ForceUnitAccess;
        [PacketField, Offset(bytes: 1, bits: 4)]
        public bool DisablePageOut;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public bool ReadProtect;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct Read16
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool ForceUnitAccessNonVolatile;
        [PacketField, Offset(bytes: 1, bits: 3)]
        public bool ForceUnitAccess;
        [PacketField, Offset(bytes: 1, bits: 4)]
        public bool DisablePageOut;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public bool ReadProtect;
        [PacketField, Offset(bytes: 2)]
        public ulong LogicalBlockAddress;
        [PacketField, Offset(bytes: 10)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 14, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct ReadBuffer
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(5)]
        public byte Mode;
        [PacketField, Offset(bytes: 2)]
        public byte BufferId;
        [PacketField, Offset(bytes: 3), Width(3)]
        public uint BufferOffset;
        [PacketField, Offset(bytes: 6), Width(3)]
        public uint AllocationLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct ReadCapacity10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 8, bits: 0), Width(1)]
        public bool PartialMediumIndicator;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct ReadCapacity16
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(5)]
        public bool ServiceAction;
        [PacketField, Offset(bytes: 2)]
        public ulong LogicalBlockAddress;
        [PacketField, Offset(bytes: 10)]
        public uint AllocationLength;
        [PacketField, Offset(bytes: 14, bits: 0), Width(1)]
        public bool PartialMediumIndicator;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct ReportLUNs
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 2)]
        public byte SelectReport;
        [PacketField, Offset(bytes: 6)]
        public uint AllocationLength;
        [PacketField, Offset(bytes: 11)]
        public byte Control;
    }

    public struct RequestSense
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(1)]
        public bool DescriptorFormat;
        [PacketField, Offset(bytes: 4)]
        public byte AllocationLength;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct SecurityProtocolIn
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1)]
        public byte SecurityProtocol;
        [PacketField, Offset(bytes: 2)]
        public ushort SecurityProtocolSpecific;
        [PacketField, Offset(bytes: 4, bits: 7), Width(1)]
        public bool Increment512;
        [PacketField, Offset(bytes: 6)]
        public uint AllocationLength;
        [PacketField, Offset(bytes: 11)]
        public byte Control;
    }

    public struct SecurityProtocolOut
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1)]
        public byte SecurityProtocol;
        [PacketField, Offset(bytes: 2)]
        public ushort SecurityProtocolSpecific;
        [PacketField, Offset(bytes: 4, bits: 7), Width(1)]
        public bool Increment512;
        [PacketField, Offset(bytes: 6)]
        public uint TransferLength;
        [PacketField, Offset(bytes: 11)]
        public byte Control;
    }

    public struct SendDiagnostic
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(1)]
        public bool UnitOffline;
        [PacketField, Offset(bytes: 1, bits: 1), Width(1)]
        public bool SCSITargetDeviceOffline;
        [PacketField, Offset(bytes: 1, bits: 2), Width(1)]
        public bool SelfTest;
        [PacketField, Offset(bytes: 1, bits: 4), Width(1)]
        public bool PageFormat;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public byte SelfTestCode;
        [PacketField, Offset(bytes: 3)]
        public ushort ParameterListLength;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct StartStopUnit
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(1)]
        public bool Immediate;
        [PacketField, Offset(bytes: 3, bits: 0), Width(4)]
        public byte PowerConditionModifier;
        [PacketField, Offset(bytes: 4, bits: 0), Width(1)]
        public bool Start;
        [PacketField, Offset(bytes: 4, bits: 1), Width(1)]
        public bool LoadEject;
        [PacketField, Offset(bytes: 4, bits: 2), Width(1)]
        public bool NoFlush;
        [PacketField, Offset(bytes: 4, bits: 4), Width(4)]
        public bool PowerConditions;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct SynchronizeCache10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool Immediate;
        [PacketField, Offset(bytes: 1, bits: 2)]
        public bool SyncNonVolatile;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort NumberOfLogicalBlocks;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct SynchronizeCache16
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool Immediate;
        [PacketField, Offset(bytes: 1, bits: 2)]
        public bool SyncNonVolatile;
        [PacketField, Offset(bytes: 2)]
        public ulong LogicalBlockAddress;
        [PacketField, Offset(bytes: 10)]
        public uint NumberOfLogicalBlocks;
        [PacketField, Offset(bytes: 14, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct TestUnitReady
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct Unmap
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort ParameterListLength;
        [PacketField, Offset(bytes: 9)]
        public ushort Control;
    }

    public struct Verify10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1), Width(1)]
        public bool ByteCheck;
        [PacketField, Offset(bytes: 1, bits: 4), Width(1)]
        public bool DisablePageOut;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public bool VerifyProtect;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort VerificationLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct Write6
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(5)]
        public byte LogicalBlockAddressHigh;
        [PacketField, Offset(bytes: 2)]
        public ushort LogicalBlockAddressLow;
        [PacketField, Offset(bytes: 4)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 5)]
        public byte Control;
    }

    public struct Write10
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool ForceUnitAccessNonVolatile;
        [PacketField, Offset(bytes: 1, bits: 3)]
        public bool ForceUnitAccess;
        [PacketField, Offset(bytes: 1, bits: 4)]
        public bool DisablePageOut;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public bool WriteProtect;
        [PacketField, Offset(bytes: 2)]
        public uint LogicalBlockAddress;
        [PacketField, Offset(bytes: 6, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 7)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }

    public struct Write16
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 1)]
        public bool ForceUnitAccessNonVolatile;
        [PacketField, Offset(bytes: 1, bits: 3)]
        public bool ForceUnitAccess;
        [PacketField, Offset(bytes: 1, bits: 4)]
        public bool DisablePageOut;
        [PacketField, Offset(bytes: 1, bits: 5), Width(3)]
        public bool WriteProtect;
        [PacketField, Offset(bytes: 2)]
        public ulong LogicalBlockAddress;
        [PacketField, Offset(bytes: 10)]
        public ushort TransferLength;
        [PacketField, Offset(bytes: 14, bits: 0), Width(5)]
        public byte GroupNumber;
        [PacketField, Offset(bytes: 15)]
        public byte Control;
    }

    public struct WriteBuffer
    {
        [PacketField, Offset(bytes: 0)]
        public byte OperationCode;
        [PacketField, Offset(bytes: 1, bits: 0), Width(5)]
        public byte Mode;
        [PacketField, Offset(bytes: 2)]
        public byte BufferId;
        [PacketField, Offset(bytes: 3), Width(3)]
        public uint BufferOffset;
        [PacketField, Offset(bytes: 6), Width(3)]
        public uint ParameterListLength;
        [PacketField, Offset(bytes: 9)]
        public byte Control;
    }
#pragma warning restore 649, 169
}