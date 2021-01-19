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

#if PLATFORM_LINUX
        public static void CreateIPv4Network(this Emulation emulation,
                                        string ip, uint netmask = 24,
                                        string dhcpRangeStart = "", string dhcpRangeEnd = "",
                                        string forwardMode = "Private", string forwardDevice = "",
                                        uint tapId = 0)
        {
            string hostInterfaceName = string.Format("tap{0}", tapId);
            string emulationTapName = string.Format("tap{0}",tapId);
            CreateTap(emulation, hostInterfaceName, emulationTapName, false);

            string ipv4ConnName = string.Format("ipv4Conn-{0}", tapId);
            LinuxHostConfigurator lhc = new LinuxHostConfigurator();

            // create the IPv4 config
            string bridgeName = string.Format("renode-br{0}", tapId);
            LinuxHostIPv4Config lhIPv4c = new LinuxHostIPv4Config(hostInterfaceName, ip, netmask, bridgeName);
            lhc.Register(lhIPv4c);

            // create the routing policy
            LinuxIPv4RoutingPolicy routingPolicy = new LinuxIPv4RoutingPolicy(lhIPv4c.Bridge, forwardMode, forwardDevice);
            lhc.Register(routingPolicy);

            // Create the DHCP Server
            LinuxDnsmasqServer dhcpSrv = new LinuxDnsmasqServer(bridgeName, lhIPv4c.IP, dhcpRangeStart, dhcpRangeEnd);
            lhc.Register(dhcpSrv);

            // apply the changes to the host
            lhc.ApplyHostConfiguration();
            emulation.HostMachine.AddHostMachineElement(lhc, ipv4ConnName);
        }
#endif

    }
}

