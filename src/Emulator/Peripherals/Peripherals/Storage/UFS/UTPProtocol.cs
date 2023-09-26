//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
    public enum DataDirection : uint
    {
        NoTransfer = 0b00,
        Out = 0b01,
        In = 0b10,
    }

    public enum CommandType : uint
    {
        UFSStorage = 1,
    }

    public enum UTPTransferStatus : uint
    {
        Success = 0,
        InvalidCommandTableAttributes = 1,
        InvalidPRDTAttributes = 2,
        MismatchDataBufferSize = 3,
        MismatchResponseUPIUSize = 4,
        CommunicationFailure = 5,
        Aborted = 6,
        HostFatalError = 7,
        DeviceFatalError = 8,
        InvalidCryptoConfiguration = 9,
        GeneralCryptoError = 10,
        InvalidOCSValue = 15,
    }

    public enum UTPTaskManagementStatus : uint
    {
        Success = 0,
        InvalidTaskManagementFunctionAttributes = 1,
        MismatchTaskManagementRequestSize = 2,
        MismatchTaskManagementResponseSize = 3,
        PeerCommunicationFailure = 4,
        Aborted = 5,
        HostFatalError = 6,
        DeviceFatalError = 7,
        InvalidOCSValue = 15,
    }

#pragma warning disable 649, 169
    [LeastSignificantByteFirst]
    public struct UTPTransferRequest
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(8)]
        public byte CryptoConfigurationIndex;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 0, bits: 23), Width(1)]
        public bool CryptoEnable;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(1)]
        public bool Interrupt;
        [PacketField, Offset(doubleWords: 0, bits: 25), Width(2)]
        public DataDirection DataDirection;
        [PacketField, Offset(doubleWords: 0, bits: 28), Width(4)]
        public CommandType CommandType;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)]
        public uint DataUnitNumberLower;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public UTPTransferStatus OverallCommandStatus;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public uint CommonDataSize;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort LastDataByteCount;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint DataUnitNumberUpper;
        [PacketField, Offset(doubleWords: 4, bits: 7), Width(25)]
        public uint UTPCommandDescriptorBaseAddressLower;
        [PacketField, Offset(doubleWords: 5, bits: 0), Width(32)]
        public uint UTPCommandDescriptorBaseAddressUpper;
        [PacketField, Offset(doubleWords: 6, bits: 0), Width(16)]
        public ushort ResponseUPIULength;
        [PacketField, Offset(doubleWords: 6, bits: 16), Width(16)]
        public ushort ResponseUPIUOffset;
        [PacketField, Offset(doubleWords: 7, bits: 0), Width(16)]
        public ushort PRDTLength;
        [PacketField, Offset(doubleWords: 7, bits: 16), Width(16)]
        public ushort PRDTOffset;
    }

    [LeastSignificantByteFirst]
    public struct UTPTaskManagementRequestHeader
    {
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(1)]
        public bool Interrupt;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public UTPTaskManagementStatus OverallCommandStatus;
    }

    [LeastSignificantByteFirst]
    public struct PRDT4DW
    {
        [PacketField, Offset(doubleWords: 0, bits: 2), Width(30)]
        public uint DataBaseAddress;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)]
        public uint DataBaseAddressUpper;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(18)]
        public uint DataByteCount;
    }

    [LeastSignificantByteFirst]
    public struct PRDT2DW
    {
        [PacketField, Offset(doubleWords: 0, bits: 2), Width(30)]
        public uint DataBaseAddress;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)]
        public uint DataBaseAddressUpper;
    }
#pragma warning restore 649, 169
}