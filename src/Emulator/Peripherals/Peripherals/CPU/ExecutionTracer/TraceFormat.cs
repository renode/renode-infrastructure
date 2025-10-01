//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

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
        RiscVAtomicInstruction = 3,
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

    public enum RiscVAtomicInstruction
    {
        ADD = 0x00,
        SWAP = 0x01,
        LR = 0x02,
        SC = 0x03,
        XOR = 0x04,
        CAS = 0x05,
        AND = 0x0C,
        OR = 0x08,
        MIN = 0x10,
        MAX = 0x14,
        MINU = 0x18,
        MAXU = 0x1C,
    }

    public enum RiscVAtomicInstructionWidth
    {
        Word = 0x2,
        DoubleWord = 0x3,
        QuadWord = 0x4,
    }

    public class RiscVAtomicInstructionData : AdditionalData
    {
        public RiscVAtomicInstructionData(bool isAfterExecution, ulong pc, int funct5, ulong rd, ulong rs1, ulong rs2, RiscVAtomicInstructionWidth width, ulong memoryValue) : base(pc, AdditionalDataType.RiscVAtomicInstruction)
        {
            IsAfterExecution = isAfterExecution;
            if(!Enum.IsDefined(typeof(RiscVAtomicInstruction), funct5))
            {
                throw new ArgumentOutOfRangeException(nameof(funct5), $"0x{funct5:X} is not a recognized AMO instruction");
            }
            Instruction = (RiscVAtomicInstruction)funct5;
            Rd = rd;
            Rs1 = rs1;
            Rs2 = rs2;
            Width = width;
            MemoryValue = memoryValue;
        }

        public override string GetStringRepresentation()
        {
            var prePostText = IsAfterExecution ? "after" : "before";
            return $"AMO operands {prePostText} - RD: 0x{Rd:X}, RS1: 0x{Rs1:X} (memory value: 0x{MemoryValue:X}), RS2: 0x{Rs2:X}";
        }

        public override byte[] GetBinaryRepresentation()
        {
            var register_width = 0;
            switch(Width)
            {
            case RiscVAtomicInstructionWidth.Word:
                register_width = sizeof(uint);
                break;
            case RiscVAtomicInstructionWidth.DoubleWord:
                register_width = sizeof(ulong);
                break;
            case RiscVAtomicInstructionWidth.QuadWord:
                throw new NotImplementedException("Support for 128-bit AMO execution tracing not yet implemented");
            }

            var output = new BitStream();

            output.AppendBytesFromValue((byte)(IsAfterExecution ? 1 : 0), sizeof(byte), true);
            output.AppendBytesFromValue((byte)Width, sizeof(byte), true);
            output.AppendBytesFromValue((byte)Instruction, sizeof(byte), true);
            output.AppendBytesFromValue(Rd, register_width, true);
            output.AppendBytesFromValue(Rs1, register_width, true);
            output.AppendBytesFromValue(Rs2, register_width, true);
            output.AppendBytesFromValue(MemoryValue, register_width, true);

            return output.AsByteArray();
        }

        public RiscVAtomicInstruction Instruction { get; }

        public bool IsAfterExecution { get; }

        public ulong Rd { get; }

        public ulong Rs1 { get; }

        public ulong Rs2 { get; }

        public RiscVAtomicInstructionWidth Width { get; }

        public ulong MemoryValue { get; }
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