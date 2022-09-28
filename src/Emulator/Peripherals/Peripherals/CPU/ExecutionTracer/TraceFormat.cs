//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging.Profiling;

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
    }

    public abstract class AdditionalData
    {
        public AdditionalData(ulong pc, AdditionalDataType type)
        {
            this.PC = pc;
            this.Type = type;
        }
        public ulong PC {get;}
        public AdditionalDataType Type {get;}

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
}
