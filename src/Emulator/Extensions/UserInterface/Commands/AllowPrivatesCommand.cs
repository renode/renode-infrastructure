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
using System.Reflection;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class AllowPrivatesCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            var allowed = (monitor.CurrentBindingFlags & BindingFlags.NonPublic) > 0;
            writer.WriteLine();
            writer.WriteLine(allowed ? "Private fields are available":"Private fields are not available");
            return;
        }

        [Runnable]
        public void RunnableAttribute(ICommandInteraction writer, BooleanToken allow)
        {
            if(allow.Value)
            {
                monitor.CurrentBindingFlags |= BindingFlags.NonPublic;
                monitor.ClearCache();
            }
            else
            {
                monitor.CurrentBindingFlags &= ~BindingFlags.NonPublic;
                monitor.ClearCache();
            }
        }

        public AllowPrivatesCommand(Monitor monitor):base(monitor, "allowPrivates","allow private fields and properties manipulation.", "privs")
        {
        }
    }
}

