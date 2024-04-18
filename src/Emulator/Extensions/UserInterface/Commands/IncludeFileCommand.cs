//
// Copyright (c) 2010-2024 Antmicro
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
using Antmicro.Renode.Utilities;

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
            writer.WriteLine("*.cs   - plugin file");
            writer.WriteLine("*.py   - python script");
            writer.WriteLine("*.repl - platform description file");
            writer.WriteLine("other  - monitor script");
        }

        [Runnable]
        public bool Run(ICommandInteraction writer, StringToken path)
        {
            return Run(writer, path.Value);
        }
        
        private bool Run(ICommandInteraction writer, ReadFilePath path)
        {
            using(var progress = EmulationManager.Instance.ProgressMonitor.Start("Including script: " + path))
            {
                bool result = false;
                switch(Path.GetExtension(path))
                {
                case ".py":
                    result = PythonExecutor(path, writer);
                    break;
                case ".cs":
                    result = CsharpExecutor(path, writer);
                    break;
                case ".repl":
                    result = ReplExecutor(path, writer);
                    break;
                default:
                    result = ScriptExecutor(path);
                    break;
                }

                return result;
            }
        }

        private readonly Func<string,bool> ScriptExecutor;
        private readonly Func<string, ICommandInteraction, bool> CsharpExecutor;
        private readonly Func<string, ICommandInteraction, bool> PythonExecutor;
        private readonly Func<string, ICommandInteraction, bool> ReplExecutor;

        public IncludeFileCommand(Monitor monitor, Func<string, ICommandInteraction, bool> pythonExecutor, Func<string, bool> scriptExecutor, Func<string, ICommandInteraction, bool> csharpExecutor, Func<string, ICommandInteraction, bool> replExecutor) : base(monitor, "include", "loads a Monitor script, Python code, platform file or a plugin class.", "i")
        {
            this.CsharpExecutor = csharpExecutor;
            this.PythonExecutor = pythonExecutor;
            this.ScriptExecutor = scriptExecutor;
            this.ReplExecutor = replExecutor;
        }
    }
}

