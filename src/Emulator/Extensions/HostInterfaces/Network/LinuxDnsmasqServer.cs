//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.TAPHelper;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public class LinuxDnsmasqServer : IHostMachineElement, ILinuxHostConfig
    {
        public LinuxDnsmasqServer(string bridgeName, string iP, string dhcpRangeStart, string dhcpRangeEnd)
        {
            this.IsEnabled = TapIPHelper.IsValidIPv4(dhcpRangeStart) &&
                        TapIPHelper.IsValidIPv4(dhcpRangeEnd);

            if(!this.IsEnabled)
            {
                this.Log(LogLevel.Info, "No valid DHCP range specified. DHCP is disabled");
            }

            if (!TapIPHelper.IsValidIPv4(iP))
            {
                this.Log(LogLevel.Info, "No valid IP for the DHCP interface. DHCP is disabled.");
                this.IsEnabled = false;
            }

            this.bridgeName = bridgeName;
            this.iP = iP;
            this.dhcpRangeStart = dhcpRangeStart;
            this.dhcpRangeEnd = dhcpRangeEnd;
            this.pidFile = TemporaryFilesManager.Instance.GetTemporaryFile();

            if (this.IsEnabled)
            {
                this.Log(LogLevel.Info, "DHCP Server enabled");
            }
        }

        public void Apply(StreamWriter shellFile)
        {
            if(!this.IsEnabled) return;

            var arguments = string.Format("dnsmasq --log-queries --no-hosts --no-resolv --leasefile-ro --interface={0} -p0 --log-dhcp --dhcp-range={1},{2} -x {3}",
                                                                    this.bridgeName,
                                                                    this.dhcpRangeStart,
                                                                    this.dhcpRangeEnd,
                                                                    this.pidFile);

            shellFile.WriteLine(arguments);
        }

        public void Rewoke(StreamWriter shellFile)
        {
            if(!this.IsEnabled) return;
            this.IsEnabled = false;

            string killString = string.Format("pkill -F {0}", this.pidFile);
            shellFile.WriteLine(killString);
        }

        public bool IsEnabled { get; private set; }

        private string bridgeName;
        private string iP;
        private string dhcpRangeStart;
        private string dhcpRangeEnd;
        private string pidFile;
    }
}