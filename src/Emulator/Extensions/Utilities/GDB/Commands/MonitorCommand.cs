//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface;
using System.Text;
using System.Linq;
using System;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class MonitorCommand : Command
    {
        public MonitorCommand(CommandsManager m) : base(m)
        {
            openOcdOverlay = new OpenOcdOverlay(m);
        }

        [Execute("qRcmd,")]
        public PacketData Run([Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexString)]string arg)
        {
            if(!openOcdOverlay.TryProcess(arg, out var result))
            {
                var monitor = ObjectCreator.Instance.GetSurrogate<Monitor>();
                var eater = new CommandInteractionEater();
                if(!monitor.Parse(arg, eater))
                {
                    return PacketData.ErrorReply(1);
                }
                result = eater.GetContents();
            }

            return (result == null) ? PacketData.Success : new PacketData(string.Join(string.Empty, Encoding.UTF8.GetBytes(result).Select(x => x.ToString("X2"))));
        }

        private readonly OpenOcdOverlay openOcdOverlay;

        private class OpenOcdOverlay
        {
            public OpenOcdOverlay(CommandsManager manager)
            {
                this.manager = manager;
            }

            public bool TryProcess(string input, out string output)
            {
                output = null;

                switch(input)
                {
                case "reset init":
                    manager.Machine.Pause();
                    manager.Machine.Reset();
                    break;
                case "halt":
                    manager.Machine.Pause();
                    break;
                case "reg":
                    var inputBuilder = new StringBuilder("=====\n");
                    foreach(var i in manager.Cpu.GetRegisters())
                    {
                        inputBuilder.AppendFormat("({0}) r{0} (/32): 0x", i);
                        var value = manager.Cpu.GetRegisterUnsafe(i);
                        foreach(var b in BitConverter.GetBytes(value))
                        {
                            inputBuilder.AppendFormat("{0:x2}", b);
                        }
                        inputBuilder.Append("\n");
                    }
                    output = inputBuilder.ToString();
                    break;
                default:
                    return false;
                }
                return true;
            }

            private readonly CommandsManager manager;
        }
    }
}

