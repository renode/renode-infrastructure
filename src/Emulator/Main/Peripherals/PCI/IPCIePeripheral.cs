//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.PCI
{
    public interface IPCIePeripheral : IPeripheral
    {
        uint ConfigurationReadDoubleWord(long offset);
        void ConfigurationWriteDoubleWord(long offset, uint value);
        uint MemoryReadDoubleWord(uint bar, long offset);
        void MemoryWriteDoubleWord(uint bar, long offset, uint value);
    }
}