//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class PeripheralsCommand : Command
    {
        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            var currentMachine = GetCurrentMachine();
            if(currentMachine == null)
            {
                writer.WriteError("Select active machine.");
                return;
            }
            writer.WriteLine("Available peripherals:");
            writer.WriteLine();

            var peripheralEntries = currentMachine.GetPeripheralsWithAllRegistrationPoints();
            var sysbusEntry = peripheralEntries.First(x => x.Key.Name == Machine.SystemBusName);
            var sysbusNode = new PeripheralNode(sysbusEntry);
            var nodeQueue = new Queue<PeripheralNode>(peripheralEntries.Where(x => x.Key != sysbusEntry.Key).Select(x => new PeripheralNode(x)));

            while(nodeQueue.Count > 0)
            {
                var x = nodeQueue.Dequeue();
                // Adding nodes to sysbusNode is successful only if the current node's parent was already added.
                // This code effectively sorts the nodes topologically.
                if(!sysbusNode.AddChild(x))
                {
                    nodeQueue.Enqueue(x);
                }
            }
            sysbusNode.PrintTree(writer);
        }


        public PeripheralsCommand(Monitor monitor, Func<IMachine> getCurrentMachine) : base(monitor, "peripherals", "prints list of registered and named peripherals.", "peri")
        {
            GetCurrentMachine = getCurrentMachine;
        }

        private Func<IMachine> GetCurrentMachine;

        private class PeripheralNode
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

            public void PrintTree(ICommandInteraction writer, TreeViewBlock[] pattern = null)
            {
                if(pattern == null)
                {
                    pattern = new TreeViewBlock[0];
                }
                var indent = GetIndentString(pattern);
                writer.WriteLine(String.Format("{0}{1} ({2})", indent, PeripheralEntry.Name, PeripheralEntry.Type.Name));

                if(PeripheralEntry.Parent != null)
                {
                    var newIndent = GetIndentString(UpdatePattern(pattern, Children.Count > 0 ? TreeViewBlock.Straight : TreeViewBlock.Empty));
                    if(!(PeripheralEntry.RegistrationPoint is ITheOnlyPossibleRegistrationPoint))
                    {
                        foreach(var registerPlace in RegistrationPoints)
                        {
                            writer.WriteLine(String.Format("{0}{1}", newIndent, registerPlace.PrettyString));
                        }
                    }
                    writer.WriteLine(newIndent);
                }
                else
                {
                    writer.WriteLine(GetIndentString(new TreeViewBlock[] { TreeViewBlock.Straight }));
                }

                var lastChild = Children.LastOrDefault();
                foreach(var child in Children)
                {
                    child.PrintTree(writer, UpdatePattern(pattern, child != lastChild ? TreeViewBlock.Full : TreeViewBlock.End));
                }
            }

            private PeripheralTreeEntry PeripheralEntry;
            private IEnumerable<IRegistrationPoint> RegistrationPoints;
            private HashSet<PeripheralNode> Children;

            private static String GetIndentString(TreeViewBlock[] rawSignPattern)
            {
                var indentBuilder = new StringBuilder(DefaultPadding);
                foreach(var tmp in rawSignPattern)
                {
                    indentBuilder.Append(GetSingleIndentString(tmp));
                }
                return indentBuilder.ToString();
            }

            private static String GetSingleIndentString(TreeViewBlock rawSignPattern)
            {
                switch(rawSignPattern)
                {
                case TreeViewBlock.Full:
                    return "├── ";
                case TreeViewBlock.End:
                    return "└── ";
                case TreeViewBlock.Straight:
                    return "│   ";
                case TreeViewBlock.Empty:
                    return "    ";
                default:
                    throw new ArgumentException();
                }
            }

            private static TreeViewBlock[] UpdatePattern(TreeViewBlock[] oldPattern, TreeViewBlock newSign)
            {
                FixLastSign(oldPattern);
                var newPattern = new TreeViewBlock[oldPattern.Length + 1];
                Array.Copy(oldPattern, newPattern, oldPattern.Length);
                newPattern[newPattern.Length - 1] = newSign;
                return newPattern;
            }

            private static void FixLastSign(TreeViewBlock[] pattern)
            {
                if(pattern.Length < 1)
                {
                    return;
                }
                if(pattern[pattern.Length - 1] == TreeViewBlock.Full)
                {
                    pattern[pattern.Length - 1] = TreeViewBlock.Straight;
                }
                else if(pattern[pattern.Length - 1] == TreeViewBlock.End)
                {
                    pattern[pattern.Length - 1] = TreeViewBlock.Empty;
                }
            }

            private const String DefaultPadding = "  ";

            internal enum TreeViewBlock { Empty, Straight, End, Full };
        }
    }
}
