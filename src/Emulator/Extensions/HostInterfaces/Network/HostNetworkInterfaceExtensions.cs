//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
#pragma warning disable IDE0005
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

#pragma warning restore IDE0005
using Antmicro.Renode.Peripherals.Network;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public static class HostNetworkInterfaceExtensions
    {
        public static IMACInterface CreateAndGetTap(this Emulation emulation, string hostInterfaceName, string name, bool persistent = false)
        {
            ITapInterface result;
            if(RuntimeInfo.IsWindows())
            {
                result = new WindowsTapInterface(hostInterfaceName);
            }
            else if(RuntimeInfo.IsMacOS())
            {
                Logger.Warning("OsX Tap Interface is obsolete and will be removed in a future release. Please use CreateVmnetHelper instead.");
                if(persistent)
                {
                    throw new RecoverableException("Persitent TAP is not available on OS X.");
                }
                result = new OsXTapInterface(hostInterfaceName);
            }
            else if(RuntimeInfo.IsLinux())
            {
                result = new LinuxTapInterface(hostInterfaceName, persistent);
            }
            else
            {
                throw new RecoverableException("TAP isn't available on this platform");
            }

            emulation.HostMachine.AddHostMachineElement(result, name);
            return result;
        }

        public static void CreateTap(this Emulation emulation, string hostInterfaceName, string name, bool persistent = false)
        {
            CreateAndGetTap(emulation, hostInterfaceName, name, persistent);
        }

        public static void CreateVmnetHelper(this Emulation emulation, string path, string name, bool autoConf = false)
        {
            if(!RuntimeInfo.IsMacOS())
            {
                throw new RecoverableException("CreateVmnetHelper is available only on macOS with dotnet.");
            }
            var result = new SocketInterface(path,async socketInterface => await VmnetHelperInterface.ConfigureInterface(socketInterface, autoConf));
            emulation.HostMachine.AddHostMachineElement(result, name);
        }
    }
}
