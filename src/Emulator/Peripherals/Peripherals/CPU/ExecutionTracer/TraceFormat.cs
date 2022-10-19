//
// Copyright (c) 2010-2022 Antmicro
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
        public ulong PC { get; }
        public AdditionalDataType Type { get; }

        public abstract string GetStringRepresentation();
        public abstract byte[] GetBinaryRepresentation();
    }

    public class MemoryAccessAdditionalData : AdditionalData
    {
        public MemoryAccessAdditionalData(ulong pc, MemoryOperation operationType, ulong operationTarget) : base(pc, AdditionalDataType.MemoryAccess)
        {
            this.operationType = operationType;
            this.operationTarget = operationTarget;
        }

        public override string GetStringRepresentation()
        {
            return $"{operationType} with address 0x{operationTarget:X}";
        }

        public override byte[] GetBinaryRepresentation()
        {
            /* 
              [0] = [operationType]
              [1] = [operationTarget 63:56]
              [2] = [operationTarget 55:48]
              [3] = [operationTarget 47:40]
              [4] = [operationTarget 39:32]
              [5] = [operationTarget 31:24]
              [6] = [operationTarget 23:16]
              [7] = [operationTarget 15:8]
              [8] = [operationTarget 7:0]
            */
            var byteLength = sizeof(ulong) + 1;
            var output = new byte[byteLength];
            output[0] = (byte)operationType;
            BitHelper.GetBytesFromValue(output, 1, operationTarget, sizeof(ulong), true);
            return output;
        }

        private readonly ulong operationTarget;
        private readonly MemoryOperation operationType;
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
