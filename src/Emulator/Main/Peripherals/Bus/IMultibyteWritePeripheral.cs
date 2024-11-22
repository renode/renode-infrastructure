//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IMultibyteWritePeripheral
    {
        byte[] ReadBytes(long offset, int count, IPeripheral context = null);
        void WriteBytes(long offset, byte[] array, int startingIndex, int count, IPeripheral context = null);
    }
}

