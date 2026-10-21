//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Andes_AndeStarV5Extension
    {
        private void ConvertVectorBFloat16(ulong opcode, bool narrow)
        {
            var vector = GetVectorState();
            if(vector == null || !RequireFloatingPoint() || (narrow && !RequireRoundingMode()))
            {
                return;
            }

            var vd = BaseRiscV.GetRd(opcode);
            var vs = BaseRiscV.GetRs2(opcode);
            var sourceWidth = narrow ? 32 : 16;
            var destinationWidth = narrow ? 16 : 32;
            var sourceMultiplier = vector.LogMultiplier + (narrow ? 1 : 0);
            var destinationMultiplier = vector.LogMultiplier + (narrow ? 0 : 1);
            if(!ValidateVector(vector.Width == 16 && cpu.VectorElementMaxWidth >= 32
                && vector.GroupValid(vd, destinationMultiplier) && vector.GroupValid(vs, sourceMultiplier)
                && vector.OverlapValid(vd, destinationMultiplier, vs, sourceMultiplier, destinationWidth, sourceWidth)))
            {
                return;
            }

            var flags = FloatingPointFlags.None;
            for(var i = vector.Start; i < vector.Length; ++i)
            {
                var value = vector.Read(vs, i, sourceWidth);
                FloatingPointFlags elementFlags;
                var result = narrow ? NarrowBFloat16((uint)value, out elementFlags) : WidenBFloat16((ushort)value, out elementFlags);
                flags |= elementFlags;
                vector.Write(vd, i, destinationWidth, result);
            }
            AccrueFloatingPointFlags(flags);
            vector.Complete();
        }

        private VectorState GetVectorState()
        {
            if(!cpu.IsInstructionSetEnabled(BaseRiscV.InstructionSet.V) || (ReadStatus() & VectorStateMask) == 0)
            {
                IllegalInstruction();
                return null;
            }

            var vector = new VectorState(cpu);
            if(!ValidateVector((vector.Type >> (IsRV64 ? 63 : 31)) == 0 && vector.Width <= cpu.VectorElementMaxWidth))
            {
                return null;
            }

            return vector;
        }

        private bool ValidateVector(bool valid)
        {
            if(!valid)
            {
                IllegalInstruction();
            }
            return valid;
        }

        private const ulong VectorStateMask = 0x600;
        private const byte VectorMaskBit = 25;

        private sealed class VectorState
        {
            public VectorState(BaseRiscV cpu)
            {
                this.cpu = cpu;
                Type = cpu.GetRegister((int)RiscV32Registers.VTYPE).RawValue;
                Width = 8 << (int)((Type >> 3) & 7);
                LogMultiplier = (int)(Type & 7);
                if(LogMultiplier >= 4)
                {
                    LogMultiplier -= 8;
                }
                Length = cpu.GetRegister((int)RiscV32Registers.VL).RawValue;
                Start = cpu.GetRegister((int)RiscV32Registers.VSTART).RawValue;
                registerBytes = (int)(cpu.VLEN.RawValue / 8);
            }

            public bool GroupValid(int register, int logMultiplier)
            {
                if(logMultiplier < -3 || logMultiplier > 3)
                {
                    return false;
                }
                var count = logMultiplier < 0 ? 1 : 1 << logMultiplier;
                return (register & (count - 1)) == 0 && register + count <= RiscVRegisterDescription.NumberOfVRegisters;
            }

            public bool OverlapValid(int destination, int destinationMultiplier, int source, int sourceMultiplier,
                int destinationWidth, int sourceWidth)
            {
                var dStart = destination * registerBytes;
                var sStart = source * registerBytes;
                var dEnd = dStart + GroupBytes(destinationMultiplier);
                var sEnd = sStart + GroupBytes(sourceMultiplier);
                if(dStart >= sEnd || sStart >= dEnd || destinationWidth == sourceWidth)
                {
                    return true;
                }
                return destinationWidth > sourceWidth ? sourceMultiplier >= 0 && sEnd == dEnd : dStart == sStart;
            }

            public bool Active(ulong opcode, ulong element)
            {
                return BitHelper.IsBitSet(opcode, VectorMaskBit) || ((Read(0, element / 8, 8) >> (int)(element % 8)) & 1) != 0;
            }

            public bool MaskDestinationValid(ulong opcode, int destination) => BitHelper.IsBitSet(opcode, VectorMaskBit) || destination != 0;

            public ulong Read(int register, ulong element, int width)
            {
                if(width == 4)
                {
                    return (Read(register, element / 2, 8) >> (int)(4 * (element % 2))) & 15;
                }

                var offset = checked((int)element * (width / 8));
                var data = GetRegisterBytes(register + offset / registerBytes);
                offset %= registerBytes;
                var result = 0ul;
                for(var i = 0; i < width / 8; ++i)
                {
                    result |= (ulong)data[offset + i] << (8 * i);
                }
                return result;
            }

            public void Write(int register, ulong element, int width, ulong value)
            {
                var offset = checked((int)element * (width / 8));
                register += offset / registerBytes;
                offset %= registerBytes;
                var data = GetRegisterBytes(register);
                for(var i = 0; i < width / 8; ++i)
                {
                    data[offset + i] = (byte)(value >> (8 * i));
                }
                dirty.Add(register);
            }

            public void Complete()
            {
                foreach(var register in dirty)
                {
                    cpu.SetRegister((int)RiscVRegisterDescription.StartOfVRegisters + register, registers[register]);
                }
                cpu.SetRegister((int)RiscV32Registers.VSTART, 0u);
                MarkStateDirty(cpu, VectorStateMask);
            }

            public readonly ulong Type;
            public readonly ulong Start;
            public readonly ulong Length;
            public readonly int Width;
            public readonly int LogMultiplier;

            private int GroupBytes(int multiplier) => multiplier < 0 ? registerBytes >> -multiplier : registerBytes << multiplier;

            private byte[] GetRegisterBytes(int register)
            {
                if(!registers.TryGetValue(register, out var bytes))
                {
                    bytes = cpu.GetRegister((int)RiscVRegisterDescription.StartOfVRegisters + register).ByteArrayValue;
                    registers.Add(register, bytes);
                }
                return bytes;
            }

            private readonly BaseRiscV cpu;
            private readonly int registerBytes;
            private readonly Dictionary<int, byte[]> registers = new Dictionary<int, byte[]>();
            private readonly HashSet<int> dirty = new HashSet<int>();
        }
    }
}
