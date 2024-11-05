//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals
{
    public interface IMemory : IPeripheral, IKnownSize, IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IMultibyteWritePeripheral, IQuadWordPeripheral
    {
    }
}

