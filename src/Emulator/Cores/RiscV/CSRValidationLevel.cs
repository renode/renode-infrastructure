//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public enum CSRValidationLevel : uint
    {
        Full = 2,
        PrivilegeLevel = 1,
        None = 0
    }
}
