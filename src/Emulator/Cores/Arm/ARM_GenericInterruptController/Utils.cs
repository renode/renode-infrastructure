//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.IRQControllers;

namespace Antmicro.Renode.Peripherals.IRQControllers.ARM_GenericInterruptControllerModel
{
    internal class Utils
    {
        internal static void AddRegistersAtOffset<T>(Dictionary<long, T> registersMap, long offset, IEnumerable<T> registers)
        where T : PeripheralRegister
        {
            foreach(var register in registers)
            {
                if(registersMap.ContainsKey(offset))
                {
                    throw new ConstructionException($"The register map already contains register at 0x{offset:x} offset.");
                }
                registersMap[offset] = register;
                offset += BitHelper.CalculateBytesCount(register.RegisterWidth);
            }
        }

        internal static bool TryWriteByteToDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, uint value, ARM_GenericInterruptController gic)
        {
            AlignRegisterOffset(offset, DoubleWordRegister.DoubleWordWidth, out var alignedOffset, out var byteOffset);
            var registerExists = registers.TryRead(alignedOffset, out var currentValue);
            if(registerExists)
            {
                BitHelper.UpdateWithShifted(ref currentValue, value, byteOffset * BitHelper.BitsPerByte, BitHelper.BitsPerByte);
                registerExists &= registers.TryWrite(alignedOffset, currentValue);
            }
            return registerExists;
        }

        internal static bool TryReadByteFromDoubleWordCollection(DoubleWordRegisterCollection registers, long offset, out byte value, ARM_GenericInterruptController gic)
        {
            AlignRegisterOffset(offset, DoubleWordRegister.DoubleWordWidth, out var alignedOffset, out var byteOffset);
            var registerExists = registers.TryRead(alignedOffset, out var registerValue);
            value = (byte)(registerValue >> (byteOffset * BitHelper.BitsPerByte));
            return registerExists;
        }

        internal static bool TryWriteDoubleWordToQuadWordCollection(QuadWordRegisterCollection registers, long offset, uint value, ARM_GenericInterruptController gic)
        {
            AlignRegisterOffset(offset, QuadWordRegister.QuadWordWidth, out var alignedOffset, out var byteOffset);
            if(byteOffset % BitHelper.CalculateBytesCount(DoubleWordRegister.DoubleWordWidth) != 0)
            {
                // Unaligned access is forbidden.
                gic.Log(LogLevel.Warning, "Unaligned register write");
                return false;
            }
            var registerExists = registers.TryRead(alignedOffset, out var currentValue);
            if(registerExists)
            {
                BitHelper.UpdateWithShifted(ref currentValue, value, byteOffset * BitHelper.BitsPerByte, BitHelper.BitsPerByte);
                registerExists &= registers.TryWrite(alignedOffset, currentValue);
            }
            return registerExists;
        }

        internal static bool TryReadDoubleWordFromQuadWordCollection(QuadWordRegisterCollection registers, long offset, out uint value, ARM_GenericInterruptController gic)
        {
            AlignRegisterOffset(offset, QuadWordRegister.QuadWordWidth, out var alignedOffset, out var byteOffset);
            if(byteOffset % BitHelper.CalculateBytesCount(DoubleWordRegister.DoubleWordWidth) != 0)
            {
                // Unaligned access is forbidden.
                gic.Log(LogLevel.Warning, "Unaligned register read");
                value = 0;
                return false;
            }
            var registerExists = registers.TryRead(alignedOffset, out var registerValue);
            value = (uint)(registerValue >> (byteOffset * BitHelper.BitsPerByte));
            return registerExists;
        }

        private static void AlignRegisterOffset(long offset, int bitsPerRegister, out long alignedOffset, out int byteOffset)
        {
            var bytesPerRegister = BitHelper.CalculateBytesCount(bitsPerRegister);
            byteOffset = (int)(offset % bytesPerRegister);
            alignedOffset = offset - byteOffset;
        }
    }
}
