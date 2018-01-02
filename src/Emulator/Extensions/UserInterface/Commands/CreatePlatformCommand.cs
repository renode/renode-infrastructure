//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Core;
using System;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class CreatePlatformCommand : Command, ISuggestionProvider
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine("\nOptions:");
            writer.WriteLine("===========================");
            foreach(var item in PlatformsProvider.GetAvailablePlatforms().OrderBy(x=>x.Name))
            {
                writer.WriteLine(item.Name);
            }
        }

        #region ISuggestionProvider implementation

        public string[] ProvideSuggestions(string prefix)
        {
            return PlatformsProvider.GetAvailablePlatforms().Where(p => p.Name.StartsWith(prefix)).Select(p => p.Name).ToArray();
        }

        #endregion

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken type)
        {
            Execute(writer, type.Value, null);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken type, StringToken name)
        {
            Execute(writer, type.Value, name.Value);
        }

        private void Execute(ICommandInteraction writer, string type, string name)
        {
            var platform = PlatformsProvider.GetPlatformByName(type);
            if (platform == null)
            {
                writer.WriteError("Invalid platform type: " + type);
                return;
            }

            var mach = new Machine() { Platform = platform };
            EmulationManager.Instance.CurrentEmulation.AddMachine(mach, name);
            changeCurrentMachine(mach);
            monitor.TryExecuteScript(platform.ScriptPath, writer);
        }

        public CreatePlatformCommand(Monitor monitor, Action<Machine> changeCurrentMachine) : base(monitor, "createPlatform", "creates a platform.", "c")
        {
            this.changeCurrentMachine = changeCurrentMachine;
        }

        private readonly Action<Machine> changeCurrentMachine;
    }
}

