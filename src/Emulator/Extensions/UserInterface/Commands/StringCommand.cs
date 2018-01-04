//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class StringCommand : AutoLoadCommand
    {
        /*[Runnable]
        public void Run(ICommandInteraction writer, params Token[] tokens)
        {
            writer.WriteLine("\"" + string.Join("", tokens.Select(x=>x.GetObjectValue().ToString())) + "\"");
        }*/

        [Runnable]
        public void Run(ICommandInteraction writer, Token[] tokens)
        {
            writer.WriteLine("\"" + string.Join(" ", tokens.Select(x=>x.GetObjectValue().ToString())) + "\"");
        }

        public StringCommand(Monitor monitor) : base(monitor, "string", "treat given arguments as a single string.", "str") 
        {
        }
    }
}

