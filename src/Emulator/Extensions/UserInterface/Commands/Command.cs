//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public abstract class AutoLoadCommand : Command, IAutoLoadType
    {
        protected AutoLoadCommand(Monitor monitor, string name, string description, params string[] alternativeNames) : base(monitor, name, description, alternativeNames)
        {
        }
    }

    public abstract class Command : ICommandDescription
    {
        public virtual void PrintHelp(ICommandInteraction writer)
        {
            writer.WriteLine(this.GetHelp());
        }

        public string[] AlternativeNames { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        protected Command(Monitor monitor, string name, string description, params string[] alternativeNames)
        {
            this.monitor = monitor;
            Description = description;
            Name = name;
            AlternativeNames = alternativeNames;
        }

        protected readonly Monitor monitor;
    }

    public class CommandComparer : IEqualityComparer<Command>
    {
        public bool Equals(Command x, Command y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(Command obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}