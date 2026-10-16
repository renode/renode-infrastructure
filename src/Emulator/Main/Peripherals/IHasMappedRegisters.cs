//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus.Wrappers;

namespace Antmicro.Renode.Peripherals
{
    public interface IHasMappedRegisters
    {
        string OffsetToString(long offset);

        RegisterMapper GetRegisterMapperForRegion(string tag)
        {
            return null;
        }
    }
}
