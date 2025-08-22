//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
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

            writer.WriteLine("\nTo load script(s) you have to provide one (or more) existing file name(s).");
            writer.WriteLine();
            writer.WriteLine("Supported file formats:");
            writer.WriteLine("*.cs   - plugin file(s)");
            writer.WriteLine("*.py   - python script");
            writer.WriteLine("*.repl - platform description file");
            writer.WriteLine("other  - monitor script");
        }

        [Runnable]
        public bool Run(ICommandInteraction writer, params StringToken[] paths)
        {
            var filePaths = paths.Select(n => new ReadFilePath(n.Value)).ToArray();
            return Run(writer, filePaths);
        }

        private bool Run(ICommandInteraction writer, ReadFilePath[] paths)
        {
            using(var progress = EmulationManager.Instance.ProgressMonitor.Start("Including script(s): " + String.Join(" ", paths.Select(nameof => nameof.ToString()))))
            {
                bool result = false;

                var path = paths[0];
                var ext = Path.GetExtension(path);

                // Validate all extensions are the same as the first one
                for(var i = 1; i < paths.Length; i++)
                {
                    var checkExt = Path.GetExtension(paths[i]);
                    if(checkExt != ext)
                    {
                        writer.WriteError($"All file extensions must match (saw {ext} and {checkExt})!");
                        return false;
                    }
                }

                // Only allow multiple include files for *.cs
                if(ext != ".cs")
                {
                    if(paths.Length > 1)
                    {
                        writer.WriteError($"Inclusion of muliple files in currently only supported for *.cs!");
                        return false;
                    }
                }

                switch(ext)
                {
                case ".py":
                    result = PythonExecutor(path, writer);
                    break;
                case ".cs":
                    var pathsStrArray = paths.Select(n => n.ToString()).ToArray();
                    result = CsharpExecutor(pathsStrArray, writer);
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
        private readonly Func<string[], ICommandInteraction, bool> CsharpExecutor;
        private readonly Func<string, ICommandInteraction, bool> PythonExecutor;
        private readonly Func<string, ICommandInteraction, bool> ReplExecutor;

        public IncludeFileCommand(Monitor monitor, Func<string, ICommandInteraction, bool> pythonExecutor, Func<string, bool> scriptExecutor, Func<string [], ICommandInteraction, bool> csharpExecutor, Func<string, ICommandInteraction, bool> replExecutor) : base(monitor, "include", "loads a Monitor script, Python code, platform file or a plugin class.", "i")
        {
            this.CsharpExecutor = csharpExecutor;
            this.PythonExecutor = pythonExecutor;
            this.ScriptExecutor = scriptExecutor;
            this.ReplExecutor = replExecutor;
        }
    }
}

