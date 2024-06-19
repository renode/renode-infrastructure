//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IIndirectCSRPeripheral : IPeripheral
    {
        uint ReadIndirectCSR(uint iselect, uint ireg);
        void WriteIndirectCSR(uint iselect, uint ireg, uint value);
    }
}
