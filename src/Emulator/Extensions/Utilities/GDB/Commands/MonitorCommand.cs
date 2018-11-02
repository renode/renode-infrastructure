//
// Copyright (c) 2010-2018 Antmicro
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
                monitor.Parse(arg, eater);
                result = eater.HasError
                    ? eater.GetError()
                    : eater.GetContents();
            }

            return (string.IsNullOrEmpty(result)) ? PacketData.Success : new PacketData(string.Join(string.Empty, Encoding.UTF8.GetBytes(result).Select(x => x.ToString("X2"))));
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
                    //this workaround allows to start debugging after first connection
                    if(!manager.ShouldAutoStart)
                    {
                        manager.Machine.Pause();
                    }
                    else
                    {
                        EmulationManager.Instance.CurrentEmulation.StartAll();
                        manager.ShouldAutoStart = false;
                    }
                    break;
                case "reg":
                    var inputBuilder = new StringBuilder("=====\n");
                    foreach(var i in manager.Cpu.GetRegisters().Where(x => x.IsGeneral).Select(x => x.Index))
                    {
                        inputBuilder.AppendFormat("({0}) r{0} (/32): 0x", i);
                        var value = manager.Cpu.GetRegisterUnsafe(i);
                        foreach(var b in value.GetBytes())
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

