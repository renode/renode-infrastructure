//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Hooks;
using Antmicro.Renode.Peripherals.Wireless;

namespace Antmicro.Renode.Extensions.Hooks
{
    public static class PacketInterceptionExtensions
    {
        public static void SetPacketHookFromScript(this WirelessMedium medium, IRadio radio, string script)
        {
            if(string.IsNullOrEmpty(script))
            {
                throw new RecoverableException("Cannot initialize packet interception hook because no script was provided");
            }
            var runner = new PacketInterceptionPythonEngine(radio, script: script);
            medium.AttachHookToRadio(radio, runner.Hook);
        }

        public static void SetPacketHookFromFile(this WirelessMedium medium, IRadio radio, string filename)
        {
            if(string.IsNullOrEmpty(filename))
            {
                throw new RecoverableException("Cannot initialize packet interception hook because no file was provided");
            }
            if(!File.Exists(filename))
            {
                throw new RecoverableException($"Cannot create the hook because file {filename} does not exist");
            }
            var runner = new PacketInterceptionPythonEngine(radio, filename: filename);
            medium.AttachHookToRadio(radio, runner.Hook);
        }
    }
}