//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Renode.Config;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class PeripheralsCommand : Command
    {
        public PeripheralsCommand(Monitor monitor, Func<IMachine> getCurrentMachine) : base(monitor, "peripherals", "prints or exports list of registered and named peripherals.", "peri")
        {
            GetCurrentMachine = getCurrentMachine;
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine($"{Name}               - print tree of peripherals");
            writer.WriteLine($"{Name} export [path] - export registered peripherals to file");
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            PeripheralNode root = CreateTree(writer);

            if(root != null)
            {
                writer.WriteLine("Available peripherals:");
                writer.WriteLine();

                root.ProcessTree();
                root.PrintTree(writer);
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken searchToken)
        {
            RunFiltering(writer, searchString: searchToken.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, RangeToken searchRange)
        {
            RunFiltering(writer, rangeToken: searchRange);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("export")] LiteralToken _, PathToken path)
        {
            RunExport(writer, path.Value);
        }

        private void RunExport(ICommandInteraction writer, WriteFilePath path)
        {
            PeripheralNode root = CreateTree(writer);

            if(root == null)
            {
                return;
            }

            using(var stream = new StreamWriter(File.Open(path, FileMode.Create)))
            {
                root.ExportTree(stream);
                Logger.Log(LogLevel.Info, "Exported peripheral map '{0}'", path);
            }
        }

        private void RunFiltering(ICommandInteraction writer, string searchString = null, RangeToken rangeToken = null)
        {
            var root = CreateTree(writer);

            if(root != null)
            {
                writer.WriteLine("Filtered peripherals:");
                writer.WriteLine();

                root.ProcessTree(searchString: searchString, rangeToken: rangeToken);
                root.PrintTree(writer);
            }
        }

        private PeripheralNode CreateTree(ICommandInteraction writer)
        {
            var currentMachine = GetCurrentMachine();

            if(currentMachine == null)
            {
                writer.WriteError("Select active machine first.");
                return null;
            }

            var peripheralEntries = currentMachine.GetPeripheralsWithAllRegistrationPoints();
            var sysbusEntry = peripheralEntries.First(x => x.Key.Name == Machine.SystemBusName);
            var sysbusNode = new PeripheralNode(sysbusEntry);
            var nodeQueue = new Queue<PeripheralNode>(peripheralEntries.Where(x => x.Key != sysbusEntry.Key).Select(x => new PeripheralNode(x)));

            sysbusEntry.Key.ShouldBePrinted = true;

            while(nodeQueue.Count > 0)
            {
                var node = nodeQueue.Dequeue();

                // Adding nodes to sysbusNode is successful only if the current node's parent was already added.
                // This code effectively sorts the nodes topologically.
                if(!sysbusNode.AddChild(node))
                {
                    nodeQueue.Enqueue(node);
                }
            }

            return sysbusNode;
        }

        private readonly Func<IMachine> GetCurrentMachine;

        private class PeripheralJson
        {
            public String Name { get; set; }

            public String Alias { get; set; }

            public List<Object> Bindings { get; set; } = new List<Object>();

            public List<PeripheralJson> Children { get; set; } = new List<PeripheralJson>();
        }

        private class PeripheralNode : ITreePrintNode
        {
            public PeripheralNode(KeyValuePair<PeripheralTreeEntry, IEnumerable<IRegistrationPoint>> rawNode)
            {
                PeripheralEntry = rawNode.Key;
                RegistrationPoints = rawNode.Value;
                Children = new HashSet<PeripheralNode>();
            }

            public bool AddChild(PeripheralNode newChild)
            {
                if(newChild.PeripheralEntry.Parent == PeripheralEntry.Peripheral)
                {
                    Children.Add(newChild);
                    return true;
                }
                foreach(var child in Children)
                {
                    if(child.AddChild(newChild))
                    {
                        return true;
                    }
                }
                return false;
            }

            public bool Contains(IPeripheral peripherial)
            {
                if(PeripheralEntry.Peripheral == peripherial)
                {
                    return true;
                }
                foreach(var item in Children)
                {
                    if(item.Contains(peripherial))
                    {
                        return true;
                    }
                }
                return false;
            }

            public void ExportTree(StreamWriter writer)
            {
                PeripheralJson root = SerializeJson();
                Object document = new
                {
                    Header = "Peripheral Map 1.0",
                    Root = root
                };

                writer.WriteLine(SimpleJson.PrettySerializeObject(document));
            }

            public void ProcessTree(string searchString = null, RangeToken rangeToken = null)
            {
                foreach(var child in Children)
                {
                    // case 1: no filtering
                    if((rangeToken == null) && (searchString == null))
                    {
                        child.PeripheralEntry.ShouldBePrinted = true;
                    }
                    // case 2: node name or type name
                    else if(searchString != null)
                    {
                        var name = child.PeripheralEntry.Name;
                        var typeName = child.PeripheralEntry.TypeName;
                        if(name == null || typeName == null)
                        {
                            continue;
                        }
                        child.PeripheralEntry.ShouldBePrinted = name.ToLower().Contains(searchString.ToLower())
                                                             || typeName.ToLower().Contains(searchString.ToLower());
                    }
                    // case 3: address range
                    else if(rangeToken != null)
                    {
                        foreach(var rp in child.RegistrationPoints)
                        {
                            if(child.PeripheralEntry.ShouldBePrinted)
                            {
                                break;
                            }

                            var registerPlace = rp as BusRegistration;
                            if(registerPlace == null)
                            {
                                child.PeripheralEntry.ShouldBePrinted = false;
                                continue;
                            }
                            var registerPlaceLower = registerPlace.StartingPoint;
                            var registerPlaceUpper = registerPlace.StartingPoint + registerPlace.Offset;

                            child.PeripheralEntry.ShouldBePrinted = rangeToken.Value.Contains(registerPlaceLower)
                                                                 || rangeToken.Value.Contains(registerPlaceUpper);
                        }
                    }
                    child.ProcessTree(searchString, rangeToken);
                }

                // if there is a child that should be printed then print the parent also
                if(Children.Where(x => x.PeripheralEntry.ShouldBePrinted).Any())
                {
                    PeripheralEntry.ShouldBePrinted = true;
                }
                foreach(var leftover in Children.Where(x => !x.PeripheralEntry.ShouldBePrinted))
                {
                    Children.Remove(leftover);
                }
            }

            public string Name => $"{PeripheralEntry.Name} ({PeripheralEntry.Type.Name})";

            IEnumerable<ITreePrintNode> ITreePrintNode.Children => Children.Where(p => p.PeripheralEntry.ShouldBePrinted);

            public IEnumerable<string> Notes
            {
                get
                {
                    if(PeripheralEntry.Parent == null || PeripheralEntry.RegistrationPoint is ITheOnlyPossibleRegistrationPoint)
                    {
                        return Enumerable.Empty<string>();
                    }
                    return RegistrationPoints.Select(rp => rp.PrettyString);
                }
            }

            private PeripheralJson SerializeJson()
            {
                PeripheralJson node = new PeripheralJson();
                node.Name = PeripheralEntry.Type.Name;
                node.Alias = PeripheralEntry.Name;

                foreach(var point in RegistrationPoints)
                {
                    if(point is IJsonSerializable serializable)
                    {
                        node.Bindings.Add(serializable.SerializeJson());
                    }
                }

                foreach(var child in Children)
                {
                    node.Children.Add(child.SerializeJson());
                }

                return node;
            }

            private readonly PeripheralTreeEntry PeripheralEntry;
            private readonly IEnumerable<IRegistrationPoint> RegistrationPoints;
            private readonly HashSet<PeripheralNode> Children;

            private const String DefaultPadding = "  ";
        }
    }
}
