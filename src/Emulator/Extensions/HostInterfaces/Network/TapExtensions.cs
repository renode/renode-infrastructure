//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using System.IO;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public static class TapExtensions
    {
        public static IMACInterface CreateAndGetTap(this Emulation emulation, string hostInterfaceName, string name, bool persistent = false)
        {
            ITapInterface result;
#if PLATFORM_WINDOWS
            result = new WindowsTapInterface(hostInterfaceName);
#elif PLATFORM_OSX
            if(persistent)
            {
                throw new RecoverableException("Persitent TAP is not available on OS X.");
            }
            result = new OsXTapInterface(hostInterfaceName);
#elif PLATFORM_LINUX
            result = new LinuxTapInterface(hostInterfaceName, persistent);
#endif

            emulation.HostMachine.AddHostMachineElement(result, name);
            return result;
        }

        public static void CreateTap(this Emulation emulation, string hostInterfaceName, string name, bool persistent = false)
        {
            CreateAndGetTap(emulation, hostInterfaceName, name, persistent);
        }
    }
}

