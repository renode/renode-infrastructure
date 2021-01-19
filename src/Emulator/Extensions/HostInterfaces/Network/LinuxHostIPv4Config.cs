//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.TAPHelper;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public class LinuxHostIPv4Config : IEmulationElement, ILinuxHostConfig
    {
        public LinuxHostIPv4Config(string tapName, string ip, uint netmask = 24, string bridgeName = "")
        {
            if(!TapIPHelper.IsValidIPv4(ip))
            {
                this.Log(LogLevel.Error, "IP provided for TAP config is not a valid IPv4 address.");
                return;
            }
            this.IP = ip;

            if(32 < netmask)
            {
                this.Log(LogLevel.Error, "Invalid netmask provided for TAP config. Netmask needs to be between 0 and 32.");
                return;
            }

            this.Netmask = netmask;

            if ("" == bridgeName)
            {
                this.Bridge = string.Format("renode-br{0}", EmulationManager.Instance.CurrentEmulation.RandomGenerator.Next(1000,9999));
            }
            else
            {
                this.Bridge = bridgeName;
            }
            this.Log(LogLevel.Info, "Creating bridge {0}", this.Bridge);

            this.TapName = tapName;
        }

        public void Apply(StreamWriter shellFile)
        {
            shellFile.WriteLine(string.Format("ip link add name {0} type bridge", this.Bridge));
            shellFile.WriteLine(string.Format("ip addr add {0}/{1} brd + dev {2}", this.IP, this.Netmask, this.Bridge));
            shellFile.WriteLine(string.Format("ip link set {0} up", this.Bridge));
            shellFile.WriteLine(string.Format("ip link set {0} master {1}", this.TapName, this.Bridge));
            shellFile.WriteLine(string.Format("ip link set {0} up", this.TapName));
        }

        public void Rewoke(StreamWriter shellFile)
        {
            shellFile.WriteLine(string.Format("ip link delete {0}", this.Bridge));
            shellFile.WriteLine(string.Format("ip link set {0} down", this.TapName));
        }

        public string IP { get; private set; }
        public uint Netmask { get; private set; }
        public string Bridge { get; private set; }
        public string TapName{ get; private set; }

        private uint MinID => 1000;
        private uint MaxID => 9999;
    }
}
