//
// Copyright (c) 2010-2018 Antmicro
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
#if PLATFORM_WINDOWS
            throw new RecoverableException("TAP is not available on Windows");
#else
            ITapInterface result;

#if PLATFORM_OSX
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
#endif
        }

        public static void CreateTap(this Emulation emulation, string hostInterfaceName, string name, bool persistent = false)
        {
            CreateAndGetTap(emulation, hostInterfaceName, name, persistent);
        }
    }
}

