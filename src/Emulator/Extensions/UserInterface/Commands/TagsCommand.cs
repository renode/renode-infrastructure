//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

using Range = Antmicro.Renode.Core.Range;
using TagListEntry = System.Collections.Generic.KeyValuePair<Antmicro.Renode.Core.Range, Antmicro.Renode.Peripherals.Bus.SystemBus.TagEntry>;

namespace Antmicro.Renode.UserInterface.Commands;

public class TagsCommand : Command
{
    public TagsCommand(Monitor monitor, Func<IMachine> getCurrentMachine) : base(monitor, "tags", "prints a list of tags.")
    {
        this.getCurrentMachine = getCurrentMachine;
    }

    public override void PrintHelp(ICommandInteraction writer)
    {
        base.PrintHelp(writer);
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine($"{Name}                          - print all tags");
        writer.WriteLine($"{Name} abc                      - print tags containing \"abc\"");
        writer.WriteLine($"{Name} <0x10000000, 0x1FFFFFFF> - print tags in range");
    }

    [Runnable]
    public void Run(ICommandInteraction writer)
    {
        RunFiltered(writer, null);
    }

    [Runnable]
    public void Run(ICommandInteraction writer, LiteralToken name)
    {
        RunFiltered(writer, n => n.Name?.Contains(name.Value) ?? false);
    }

    [Runnable]
    public void Run(ICommandInteraction writer, RangeToken range)
    {
        RunFiltered(writer, n => n.Range.Intersects(range.Value));
    }

    private void RunFiltered(ICommandInteraction writer, Func<TagNode, bool> filter)
    {
        var machine = getCurrentMachine();
        if(machine == null || machine.SystemBus is not SystemBus sysbus)
        {
            writer.WriteError("Select active machine first.");
            return;
        }
        // `Tags` is guaranteed to be sorted by start address, the tag tree reconstruction logic depends on thsi
        var allTags = sysbus.Tags.GetEnumerator();
        var rootNode = TagNode.MakeRootNode(allTags);
        if(filter != null)
        {
            rootNode.Filter(filter);
        }
        if(!rootNode.Shown)
        {
            writer.WriteLine("No matching tags.");
            return;
        }
        rootNode.PrintTree(writer);
    }

    private readonly Func<IMachine> getCurrentMachine;

    private class TagNode : ITreePrintNode
    {
        public static TagNode MakeRootNode(IEnumerator<TagListEntry> tagsEnumerator)
        {
            var node = new TagNode();
            if(!tagsEnumerator.MoveNext())
            {
                return node;
            }
            while(true)
            {
                // This logic is simplified from `MakeSubNode` - all entries are subtags,
                // and the first part of the name is the full name
                var entry = tagsEnumerator.Current;
                var entryName = entry.Value.Name.Split(SystemBus.TagNestSymbol).First();

                var (subNode, moreTags) = MakeSubNode(entryName, tagsEnumerator);
                node.children.Add(subNode);
                if(!moreTags)
                {
                    return node;
                }
            }
        }

        public static (TagNode, bool) MakeSubNode(string fullName, IEnumerator<TagListEntry> tagsEnumerator)
        {
            // `allTags.Current` always refers to the tag entry that first referenced this tag.
            // `fullName` will be a substring of it. For example, `en.Current.Value.Name` is A->B->C,
            // `MakeSubNode` will be called with a `fullName` of A, A->B, and A->B->C
            var node = new TagNode
            {
                // The name of this specific node, so the B of A->B
                Name = fullName.Split(SystemBus.TagNestSymbol).Last(),
                // This is provisional - the range will be grown to include any subtags or following tags with the same name
                Range = tagsEnumerator.Current.Key,
                DefaultValue = tagsEnumerator.Current.Value.DefaultValue
            };

            // Move on to the next entry only if we made all parent nodes.
            // We don't want to MoveNext when `fullName` is A->B when the entry name is A->B->C,
            // because we still need to make the C subnode
            if(tagsEnumerator.Current.Value.Name == fullName)
            {
                if(!tagsEnumerator.MoveNext())
                {
                    return (node, false);
                }
            }

            while(true)
            {
                var subEntry = tagsEnumerator.Current;
                var subEntryName = subEntry.Value.Name;
                // If the next entry is neither a subtag nor a continuation of this tag, the entry should be
                // handled by the parent tag
                if(subEntryName != fullName && !subEntryName.StartsWith(fullName + SystemBus.TagNestSymbol))
                {
                    return (node, true);
                }
                node.Range = node.Range.ExpandUnchecked(subEntry.Key);
                if(subEntryName == fullName)
                {
                    // This is a continuation tag, so it has been fully handled by expanding the range
                    if(!tagsEnumerator.MoveNext())
                    {
                        return (node, false);
                    }
                    continue;
                }
                // Figure out the subtag name, if we're A and the entry name is A->B->C, we want the subtag to be A->B
                var nextNestSymbolIdx = subEntryName.IndexOf(SystemBus.TagNestSymbol, fullName.Length + SystemBus.TagNestSymbol.Length);
                if(nextNestSymbolIdx == -1)
                {
                    nextNestSymbolIdx = subEntryName.Length;
                }
                var subName = subEntry.Value.Name[..nextNestSymbolIdx];
                // `MakeSubNode` moves the enumerator until the first tag it can't handle or until it runs out of tags,
                // so we don't need to advance the enumerator after making a subnode
                var (subNode, moreTags) = MakeSubNode(subName, tagsEnumerator);
                node.children.Add(subNode);
                if(!moreTags)
                {
                    return (node, false);
                }
            }
        }

        public void Filter(Func<TagNode, bool> filter)
        {
            Shown = filter(this);
            foreach(var child in children)
            {
                child.Filter(filter);
                Shown = Shown || child.Shown;
            }
        }

        string ITreePrintNode.Name => Name ?? "(all tags)";

        public IEnumerable<string> Notes
        {
            get
            {
                if(Name == null)
                {
                    yield break;
                }
                yield return Range.ToString();
                if(DefaultValue != 0)
                {
                    yield return $"Default value: 0x{DefaultValue:X}";
                }
            }
        }

        IEnumerable<ITreePrintNode> ITreePrintNode.Children => children
            .Where(ch => ch.Shown);

        public bool Shown { get; private set; } = true;

        public Range Range { get; private set; }

        public string Name { get; private set; }

        public ulong DefaultValue { get; private set; }

        private TagNode()
        { }

        private readonly List<TagNode> children = [];
    }
}
