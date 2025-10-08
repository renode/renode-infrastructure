//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public enum TraceFormat
    {
        PC,
        Opcode,
        PCAndOpcode,
        Disassembly,
        TraceBasedModel,
    }

    public enum AdditionalDataType : byte
    {
        None = 0,
        MemoryAccess = 1,
        RiscVVectorConfiguration = 2,
    }

    public abstract class AdditionalData
    {
        public AdditionalData(ulong pc, AdditionalDataType type)
        {
            this.PC = pc;
            this.Type = type;
        }

        public abstract string GetStringRepresentation();

        public abstract byte[] GetBinaryRepresentation();

        public ulong PC { get; }

        public AdditionalDataType Type { get; }
    }

    public class MemoryAccessAdditionalData : AdditionalData
    {
        public MemoryAccessAdditionalData(ulong pc, MemoryOperation operationType, ulong operationTargetVirtual, ulong operationTargetPhysical, ulong operationValue) : base(pc, AdditionalDataType.MemoryAccess)
        {
            this.OperationType = operationType;
            this.OperationTargetVirtual = operationTargetVirtual;
            this.OperationTargetPhysical = operationTargetPhysical;
            this.OperationValue = operationValue;
        }

        public override string GetStringRepresentation()
        {
            if(OperationTargetVirtual == OperationTargetPhysical)
            {
                return $"{OperationType} with address 0x{OperationTargetVirtual:X}, value 0x{OperationValue:X}";
            }
            else
            {
                return $"{OperationType} with address 0x{OperationTargetVirtual:X} => 0x{OperationTargetPhysical:X}, value 0x{OperationValue:X}";
            }
        }

        public override byte[] GetBinaryRepresentation()
        {
            /* 
              [0] = [operationType]
              [1] = [operationTargetVirtual 63:56]
              [2] = [operationTargetVirtual 55:48]
              [3] = [operationTargetVirtual 47:40]
              [4] = [operationTargetVirtual 39:32]
              [5] = [operationTargetVirtual 31:24]
              [6] = [operationTargetVirtual 23:16]
              [7] = [operationTargetVirtual 15:8]
              [8] = [operationTargetVirtual 7:0]
              [9] = [operationValue 63:56]
              [10] = [operationValue 55:48]
              [11] = [operationValue 47:40]
              [12] = [operationValue 39:32]
              [13] = [operationValue 31:24]
              [14] = [operationValue 23:16]
              [15] = [operationValue 15:8]
              [16] = [operationValue 7:0]
              [17] = [operationTargetPhysical 63:56]
              [18] = [operationTargetPhysical 55:48]
              [19] = [operationTargetPhysical 47:40]
              [20] = [operationTargetPhysical 39:32]
              [21] = [operationTargetPhysical 31:24]
              [22] = [operationTargetPhysical 23:16]
              [23] = [operationTargetPhysical 15:8]
              [24] = [operationTargetPhysical 7:0]
            */
            var byteLength = sizeof(ulong) + sizeof(ulong) + sizeof(ulong) + 1;
            var output = new byte[byteLength];
            output[0] = (byte)OperationType;
            BitHelper.GetBytesFromValue(output, 1, OperationTargetVirtual, sizeof(ulong), true);
            BitHelper.GetBytesFromValue(output, 9, OperationValue, sizeof(ulong), true);
            BitHelper.GetBytesFromValue(output, 17, OperationTargetPhysical, sizeof(ulong), true);

            return output;
        }

        public ulong OperationTargetVirtual { get; }

        public ulong OperationTargetPhysical { get; }

        public ulong OperationValue { get; }

        public MemoryOperation OperationType { get; }
    }

    public class RiscVVectorConfigurationData : AdditionalData
    {
        public RiscVVectorConfigurationData(ulong pc, ulong vl, ulong vtype) : base(pc, AdditionalDataType.RiscVVectorConfiguration)
        {
            this.VectorLength = vl;
            this.VectorType = vtype;
        }

        public override string GetStringRepresentation()
        {
            return $"Vector configured to VL: 0x{VectorLength:X}, VTYPE: 0x{VectorType:X}";
        }

        public override byte[] GetBinaryRepresentation()
        {
            /*
              [0] = [VectorLength 63:56]
              [1] = [VectorLength 55:48]
              [2] = [VectorLength 47:40]
              [3] = [VectorLength 39:32]
              [4] = [VectorLength 31:24]
              [5] = [VectorLength 23:16]
              [6] = [VectorLength 15:8]
              [7] = [VectorLength 7:0]
              [8] = [VectorType 63:56]
              [9] = [VectorType 55:48]
              [10] = [VectorType 47:40]
              [11] = [VectorType 39:32]
              [12] = [VectorType 31:24]
              [13] = [VectorType 23:16]
              [14] = [VectorType 15:8]
              [15] = [VectorType 7:0]
            */
            var byteLength = sizeof(ulong) * 2;
            var output = new byte[byteLength];

            BitHelper.GetBytesFromValue(output, 0, VectorLength, sizeof(ulong), true);
            BitHelper.GetBytesFromValue(output, sizeof(ulong), VectorType, sizeof(ulong), true);
            return output;
        }

        public ulong VectorLength { get; }

        public ulong VectorType { get; }
    }
}