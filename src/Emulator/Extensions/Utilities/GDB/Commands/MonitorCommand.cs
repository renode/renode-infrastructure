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

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class MonitorCommand : Command
    {
        public MonitorCommand(CommandsManager m) : base(m)
        {
        }

        [Execute("qRcmd,")]
        public PacketData Run([Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexString)]string arg)
        {
            string result;
            var monitor = ObjectCreator.Instance.GetSurrogate<Monitor>();
            var eater = new CommandInteractionEater();
            if(!monitor.Parse(arg, eater))
            {
                result = eater.GetError();
            }
            else
            {
                result = eater.GetContents();
                if(string.IsNullOrEmpty(result))
                {
                    return PacketData.Success;
                }
            }
            return new PacketData(string.Join(string.Empty, Encoding.UTF8.GetBytes(result).Select(x => x.ToString("X2"))));
        }
    }
}

