//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU
{
    public enum PrivilegeLevel
    {
        User = 0,
        Supervisor = 1,
        //The Hypervisor level is not yet supported
        //Hypervisor = 2,
        Machine = 3
    }
}

