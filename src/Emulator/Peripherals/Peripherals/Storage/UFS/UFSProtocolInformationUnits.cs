//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
    public enum UPIUTransactionCodeInitiatorToTarget : byte
    {
        NopOut = 0b000000,
        Command = 0b000001,
        DataOut = 0b000010,
        TaskManagementRequest = 0b000100,
        QueryRequest = 0b010110,
    }

    public enum UPIUTransactionCodeTargetToInitiator : byte
    {
        NopIn = 0b100000,
        Response = 0b100001,
        DataIn = 0b100010,
        TaskManagementResponse = 0b100100,
        ReadyToTransfer = 0b110001,
        QueryResponse = 0b110110,
        RejectUPIU = 0b111111,
    }

    public enum TaskAttribute
    {
        Simple = 0b00,
        Ordered = 0b01,
        HeadOfQueue = 0b10,
        ACA = 0b11
    }

    public enum QueryFunction : byte
    {
        StandardReadRequest = 0x01,
        StandardWriteRequest = 0x81,
    }

    public enum QueryFunctionOpcode : byte
    {
        Nop = 0x00,
        ReadDescriptor = 0x01,
        WriteDescriptor = 0x02,
        ReadAttribute = 0x03,
        WriteAttribute = 0x04,
        ReadFlag = 0x05,
        SetFlag = 0x06,
        ClearFlag = 0x07,
        ToggleFlag = 0x08
    }

    public enum QueryResponseCode : byte
    {
        Success = 0x0,
        ParameterNotReadable = 0xf6,
        ParameterNotWriteable = 0xf7,
        ParameterAlreadyWritten = 0xf8,
        InvalidLength = 0xf9,
        InvalidValue = 0xfa,
        InvalidSelector = 0xfb,
        InvalidIndex = 0xfc,
        InvalidIdn = 0xfd,
        InvalidOpcode = 0xfe,
        GeneralFailure = 0xff
    }

#pragma warning disable 649, 169
    public struct BasicUPIUHeader
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 4), Width(4)]
        public byte CommandSetType;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(8)]
        public byte Function;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 1, bits: 24), Width(8)]
        public byte Status;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public byte DeviceInformation;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
    }

    public struct CommandUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 4), Width(4)]
        public byte CommandSetType;
        [PacketField, Offset(doubleWords: 1, bits: 24), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint ExpectedDataTransferLength;
        [PacketField, Width(16)]
        public byte[] CommandDescriptorBlock;
    }

    public struct CommandUPIUFlags
    {
        [PacketField, Offset(bytes: 0, bits: 0), Width(2)]
        public TaskAttribute TaskAttribute;
        [PacketField, Offset(bytes: 0, bits: 2), Width(1)]
        public bool CommandPriority;
        [PacketField, Offset(bytes: 0, bits: 5), Width(1)]
        public bool Write;
        [PacketField, Offset(bytes: 0, bits: 6), Width(1)]
        public bool Read;
    }
    public struct DataInUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint DataBufferOffset;
        [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
        public uint DataTransferCount;
        [PacketField, Offset(doubleWords: 5, bits: 4), Width(4)]
        public byte HintControl;
        [PacketField, Offset(doubleWords: 5, bits: 8), Width(4)]
        public byte HintNexusInitiatorID;
        [PacketField, Offset(doubleWords: 5, bits: 12), Width(4)]
        public byte HintInitiatorID;
        [PacketField, Offset(doubleWords: 5, bits: 16), Width(8)]
        public byte HintLogicalUnitNumber;
        [PacketField, Offset(doubleWords: 5, bits: 24), Width(8)]
        public byte HintTaskTag;
        [PacketField, Offset(doubleWords: 6, bits: 0), Width(32)]
        public uint HintDataBufferOffset;
        [PacketField, Offset(doubleWords: 7, bits: 0), Width(32)]
        public uint HintDataCount;
    }

    public struct DataOutUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 24), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint DataBufferOffset;
        [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
        public uint DataTransferCount;
    }

    public struct NopInUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public byte DeviceInformation;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
    }

    public struct NopOutUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
    }

    public struct QueryRequestUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(8)]
        public QueryFunction QueryFunction;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Width(16)]
        public byte[] TransactionSpecificFields;
    }

    public struct QueryResponseUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(8)]
        public QueryFunction QueryFunction;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public QueryResponseCode QueryResponse;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public byte DeviceInformation;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Width(16)]
        public byte[] TransactionSpecficFields;
    }

    public struct ReadyToTransferUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint DataBufferOffset;
        [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
        public uint DataTransferCount;
        [PacketField, Offset(doubleWords: 5, bits: 4), Width(4)]
        public byte HintControl;
        [PacketField, Offset(doubleWords: 5, bits: 8), Width(4)]
        public byte HintNexusInitiatorID;
        [PacketField, Offset(doubleWords: 5, bits: 12), Width(4)]
        public byte HintInitiatorID;
        [PacketField, Offset(doubleWords: 5, bits: 16), Width(8)]
        public byte HintLogicalUnitNumber;
        [PacketField, Offset(doubleWords: 5, bits: 24), Width(8)]
        public byte HintTaskTag;
        [PacketField, Offset(doubleWords: 6, bits: 0), Width(32)]
        public uint HintDataBufferOffset;
        [PacketField, Offset(doubleWords: 7, bits: 0), Width(32)]
        public uint HintDataCount;
    }

    public struct RejectUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public byte DeviceInformation;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(8)]
        public byte BasicHeaderStatus;
        [PacketField, Offset(doubleWords: 3, bits: 16), Width(8)]
        public byte E2EStatus;
    }

    public struct ResponseUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 4), Width(4)]
        public byte CommandSetType;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 1, bits: 24), Width(8)]
        public byte Status;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 8), Width(8)]
        public byte DeviceInformation;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint ResidualTransferCount;
    }

    public struct ResponseUPIUFlags
    {
        [PacketField, Offset(bytes: 0, bits: 4), Width(1)]
        public bool DataOutMismatch;
        [PacketField, Offset(bytes: 0, bits: 5), Width(1)]
        public bool DataUnderflow;
        [PacketField, Offset(bytes: 0, bits: 6), Width(1)]
        public bool DataOverflow;
    }

    public struct TaskManagementRequestUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(8)]
        public byte TaskManagementFunction;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 1, bits: 24), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint InputParameter1;
        [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
        public uint InputParameter2;
        [PacketField, Offset(doubleWords: 5, bits: 0), Width(32)]
        public uint InputParameter3;
    }

    public struct TaskManagementResponseUPIU
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(6)]
        public byte TransactionCode;
        [PacketField, Offset(doubleWords: 0, bits: 6), Width(1)]
        public bool DataSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 7), Width(1)]
        public bool HeaderSegmentsCRC;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte Flags;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte LogicalUnitNumber;
        [PacketField, Offset(doubleWords: 0, bits: 24), Width(8)]
        public byte TaskTag;
        [PacketField, Offset(doubleWords: 1, bits: 0), Width(4)]
        public byte InitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)]
        public byte NexusInitiatorID;
        [PacketField, Offset(doubleWords: 1, bits: 16), Width(8)]
        public byte Response;
        [PacketField, Offset(doubleWords: 2, bits: 0), Width(8)]
        public byte TotalEHSLength;
        [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)]
        public ushort DataSegmentLength;
        [PacketField, Offset(doubleWords: 3, bits: 0), Width(32)]
        public uint OutputParameter1;
        [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
        public uint OutputParameter2;
    }

    public struct EHSEntry
    {
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(8)]
        public byte Length;
        [PacketField, Offset(doubleWords: 0, bits: 8), Width(8)]
        public byte EHSType;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(16)]
        public ushort EHSSubType;
        // EHS data
    }
#pragma warning restore 649, 169
}