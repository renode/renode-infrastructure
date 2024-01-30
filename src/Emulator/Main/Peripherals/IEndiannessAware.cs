//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals
{
    public interface IEndiannessAware : IBusPeripheral
    {
        Endianess Endianness { get; }
    }
}
