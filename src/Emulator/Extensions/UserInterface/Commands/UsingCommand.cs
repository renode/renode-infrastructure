//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class UsingCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Current 'using':");
            if(!GetUsings().Any())
            {
                writer.WriteLine("\t[none]");
            }
            foreach(var use in GetUsings())
            {
                writer.WriteLine ("\t"+ use.Substring(0, use.Length-1)); //to remove the trailing dot
            }
            writer.WriteLine();
            writer.WriteLine(String.Format("To clear all current usings execute \"{0} -\".", Name));
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken use)
        {
            if(use.Value == "-")
            {
                GetUsings().Clear();
            }
            else
            {
                var dotted = use.Value + (use.Value.Last() == '.' ? "" : "."); //dot suffix
                if(!GetUsings().Contains(dotted))
                {
                    GetUsings().Add(dotted);
                }
            }
        }

        private Func<List<string>> GetUsings;

        public UsingCommand(Monitor monitor, Func<List<string>> getUsings) : base(monitor, "using", "expose a prefix to avoid typing full object names.")
        {
            GetUsings = getUsings;
        }
    }
}

