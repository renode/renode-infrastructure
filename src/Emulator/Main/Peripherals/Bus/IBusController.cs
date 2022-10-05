//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IBusController
    {
        byte ReadByte(ulong address, ICPU context = null);
        void WriteByte(ulong address, byte value, ICPU context = null);

        ushort ReadWord(ulong address, ICPU context = null);
        void WriteWord(ulong address, ushort value, ICPU context = null);

        uint ReadDoubleWord(ulong address, ICPU context = null);
        void WriteDoubleWord(ulong address, uint value, ICPU context = null);

        void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, ICPU context = null);
        byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, ICPU context = null);

        void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, ICPU context = null);
        void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, ICPU context = null);
        void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, ICPU context = null);

        IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, ICPU context = null);
        IPeripheral WhatPeripheralIsAt(ulong address, ICPU context = null);
    }
}
