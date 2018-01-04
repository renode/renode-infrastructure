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
using System.IO;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class IncludeFileCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);

            writer.WriteLine("\nTo load a script you have to provide an existing file name.");
            writer.WriteLine();
            writer.WriteLine("Supported file formats:");
            writer.WriteLine("*.cs  - plugin file");
            writer.WriteLine("*.py  - python script");
            writer.WriteLine("other - monitor script");
        }

        [Runnable]
        public bool Run(ICommandInteraction writer, PathToken path)
        {
            if(!File.Exists(path.Value))
            {
                writer.WriteError(String.Format("No such file {0}.", path.Value));
                return false;
            }

            using(var progress = EmulationManager.Instance.ProgressMonitor.Start("Including script: " + path.Value))
            {
                bool result = false;
                switch(Path.GetExtension(path.Value))
                {
                case ".py":
                    result = PythonExecutor(path.Value, writer);
                    break;
                case ".cs":
                    result = CsharpExecutor(path.Value, writer);
                    break;
                default:
                    result = ScriptExecutor(path.Value);
                    break;
                }

                return result;
            }
        }

        private readonly Func<string,bool> ScriptExecutor;
        private readonly Func<string, ICommandInteraction, bool> CsharpExecutor;
        private readonly Func<string, ICommandInteraction, bool> PythonExecutor;

        public IncludeFileCommand(Monitor monitor, Func<string, ICommandInteraction, bool> pythonExecutor, Func<string, bool> scriptExecutor, Func<string, ICommandInteraction, bool> csharpExecutor) : base(monitor, "include", "loads a monitor script, python code or a plugin class.", "i")
        {
            this.CsharpExecutor = csharpExecutor;
            this.PythonExecutor = pythonExecutor;
            this.ScriptExecutor = scriptExecutor;
        }
    }
}

