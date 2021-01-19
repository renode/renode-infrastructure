//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public class LinuxIPv4RoutingPolicy: IEmulationElement, ILinuxHostConfig
    {
        public LinuxIPv4RoutingPolicy(string bridgeName, string forwardMode, string forwardDevice)
        {
            ForwardMode t_mode;

            if(Enum.TryParse(forwardMode, true, out t_mode))
            {
                this.Mode = t_mode;
            }
            else
            {
                this.Log(LogLevel.Error, "Invalid Forward Mode. Forwarding disabled.");
                this.Mode = ForwardMode.Manual;
            }

            this.IsEnabled = !(ForwardMode.Manual == this.Mode);
            this.TargetDevice = forwardDevice;
            this.Bridge = bridgeName;
        }

        public void Apply(StreamWriter shellFile)
        {
            if(!this.IsEnabled) return;

            shellFile.WriteLine(string.Format("ufw allow in on {0}", this.Bridge));
            shellFile.WriteLine(string.Format("ufw allow out on {0}", this.Bridge));
        }

        public void Rewoke(StreamWriter shellFile)
        {
            if(!this.IsEnabled) return;

            shellFile.WriteLine(string.Format("ufw delete allow in on {0}", this.Bridge));
            shellFile.WriteLine(string.Format("ufw delete allow out on {0}", this.Bridge));
        }

        public enum ForwardMode
        {
            Private = 0,
            NAT = 1,
            PassThrough = 2,
            Manual = 3
        }

        public bool IsEnabled { get; private set; }
        public ForwardMode Mode { get; private set; }
        public string TargetDevice { get; private set; }
        public string Bridge { get; private set; }
    }
}
