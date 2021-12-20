//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public interface ISideloadableKey
    {
        IEnumerable<byte> SideloadKey { set; }
    }
}
