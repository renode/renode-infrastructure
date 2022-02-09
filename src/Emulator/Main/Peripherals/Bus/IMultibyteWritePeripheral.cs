//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IMultibyteWritePeripheral
    {
        byte[] ReadBytes(long offset, int count, IPeripheral context = null);
        void WriteBytes(long offset, byte[] array, int startingIndex, int count, IPeripheral context = null);
    }

    public static class IMultibyteWritePeripheralExtensions
    {
        public static void FillWithRepeatingData(this IMultibyteWritePeripheral peripheral, byte[] data, long? size = null)
        {
            if(!size.HasValue && !(peripheral is IKnownSize))
            {
                throw new RecoverableException($"'{nameof(size)}' has to be provided or '{nameof(peripheral)}' has to implement '{nameof(IKnownSize)}'");
            }
            size = size ?? (peripheral as IKnownSize).Size;

            var wholeChunks = size.Value / data.Length;
            for(var i = 0L; i < wholeChunks; ++i)
            {
                peripheral.WriteBytes(i * data.Length, data, 0, data.Length);
            }

            if(wholeChunks * data.Length < size.Value)
            {
                peripheral.WriteBytes(wholeChunks * data.Length, data, 0, checked((int)(size.Value - wholeChunks * data.Length)));
            }
        }

        public static void FillWithConstantDoubleWord(this IMultibyteWritePeripheral peripheral, int value, long? size = null)
        {
            peripheral.FillWithRepeatingData(BitConverter.GetBytes(value), size);
        }

        public static void FillWithConstantWord(this IMultibyteWritePeripheral peripheral, short value, long? size = null)
        {
            peripheral.FillWithRepeatingData(BitConverter.GetBytes(value), size);
        }

        public static void FillWithConstantByte(this IMultibyteWritePeripheral peripheral, byte value, long? size = null)
        {
            peripheral.FillWithRepeatingData(new byte[] { value }, size);
        }
    }
}
