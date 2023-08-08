//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Core;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class MachCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            var currentMachine = GetCurrentMachine();
            if(currentMachine == null)
            {
                writer.WriteError("No machine selected.");
            }
            else
            {
                writer.WriteLine(string.Format("Current machine: {0}", EmulationManager.Instance.CurrentEmulation[currentMachine]));
            }
            if(EmulationManager.Instance.CurrentEmulation.MachinesCount > 0)
            {
                writer.WriteLine("Available machines:");
                var emu = EmulationManager.Instance.CurrentEmulation;
                var longestNameLength = emu.Machines.Max(m => emu[m].Length);
                var i = 0;
                foreach(var machine in emu.Machines)
                {
                    writer.WriteLine(string.Format("\t{2}: {0,-" + longestNameLength + "} {1}", emu[machine], machine.Platform != null ? string.Format("[{0}]", machine.Platform.Name) : string.Empty, i++));
                }
            }
            writer.WriteLine();
            writer.WriteLine("You can use the following commands:");
            writer.WriteLine("'mach set [\"name\"|number]'\tto enable the given machine");
            writer.WriteLine("'mach add \"name\"'\tto create a new machine with the specified name");
            writer.WriteLine("'mach rem \"name\"'\tto remove a machine");
            writer.WriteLine("'mach create'\tto create a new machine with generic name and switch to it");
            writer.WriteLine("'mach clear'\tto clear the current selection");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("set")] LiteralToken action, DecimalIntegerToken number)
        {
            var machines = EmulationManager.Instance.CurrentEmulation.Machines.ToArray();
            if(machines.Length > number.Value && number.Value >= 0)
            {
                SetCurrentMachine(machines[number.Value]);
            }
            else
            {
                writer.WriteError("Wrong machine number. Type {0} to show a list of available machines.".FormatWith(Name));
            }

        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("set", "add", "rem", "create")] LiteralToken action, StringToken name)
        {
            IMachine machine;
            switch(action.Value)
            {
            case "add":
                machine = new Machine();
                EmulationManager.Instance.CurrentEmulation.AddMachine(machine, name.Value);
                if(GetCurrentMachine() == null)
                {
                    SetCurrentMachine(machine);
                }
                break;
            case "set":
                if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(name.Value, out var machineToSet))
                {
                    writer.WriteError(string.Format("Machine {0} not found.", name.Value));
                    break;
                }
                SetCurrentMachine(machineToSet);
                break;
            case "rem":
                if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineByName(name.Value, out var machineToRemove))
                {
                    writer.WriteError(string.Format("Machine {0} not found.", name.Value));
                    break;
                }
                EmulationManager.Instance.CurrentEmulation.RemoveMachine(name.Value);
                if(GetCurrentMachine() == machineToRemove)
                {
                    SetCurrentMachine(null);
                }
                break;
            case "create":
                machine = new Machine();
                EmulationManager.Instance.CurrentEmulation.AddMachine(machine, name.Value);
                SetCurrentMachine(machine);
                break;
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("create", "clear")] LiteralToken action)
        {
            switch(action.Value)
            {
            case "clear":
                SetCurrentMachine(null);
                break;
            case "create":
                var machine = new Machine();
                EmulationManager.Instance.CurrentEmulation.AddMachine(machine);
                SetCurrentMachine(machine);
                break;
            }
        }

        private readonly Func<IMachine> GetCurrentMachine;
        private readonly Action<IMachine> SetCurrentMachine;

        public MachCommand(Monitor monitor, Func<IMachine> getCurrentMachine, Action<IMachine> setCurrentMachine)
            : base(monitor, "mach", "list and manipulate machines available in the environment.")
        {
            GetCurrentMachine = getCurrentMachine;
            SetCurrentMachine = setCurrentMachine;
        }
    }
}

