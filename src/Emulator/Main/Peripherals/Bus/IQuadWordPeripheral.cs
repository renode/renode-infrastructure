//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IQuadWordPeripheral : IBusPeripheral
    {
        ulong ReadQuadWord(long offset);

        void WriteQuadWord(long offset, ulong value);
    }
}