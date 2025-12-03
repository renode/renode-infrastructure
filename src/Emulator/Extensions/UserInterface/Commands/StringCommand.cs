//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class StringCommand : AutoLoadCommand
    {
        public StringCommand(Monitor monitor) : base(monitor, "string", "treat given arguments as a single string.", "str")
        {
        }
        /*[Runnable]
        public void Run(ICommandInteraction writer, params Token[] tokens)
        {
            writer.WriteLine("\"" + string.Join("", tokens.Select(x=>x.GetObjectValue().ToString())) + "\"");
        }*/

        [Runnable]
        public void Run(ICommandInteraction writer, Token[] tokens)
        {
            writer.WriteLine("\"" + string.Join(" ", tokens.Select(x => x.GetObjectValue().ToString())) + "\"");
        }
    }
}