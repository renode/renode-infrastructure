//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AntShell.Commands;

namespace Antmicro.Renode.Utilities;

public interface ITreePrintNode
{
    public string Name { get; }

    public IEnumerable<string> Notes { get; }

    public IEnumerable<ITreePrintNode> Children { get; }
}

public static class ITreePrintNodeExtensions
{
    public static void PrintTree(this ITreePrintNode node, ICommandInteraction writer)
    {
        node.PrintTree(writer, []);
    }

    private static string ToIndent(this TreePrintIndent indent) => indent switch
    {
        TreePrintIndent.Full => "├── ",
        TreePrintIndent.End => "└── ",
        TreePrintIndent.Straight => "│   ",
        TreePrintIndent.Empty => "    ",
        _ => throw new ArgumentException()
    };

    private static TreePrintIndent WithoutSpoke(this TreePrintIndent indent) => indent switch
    {
        TreePrintIndent.Full => TreePrintIndent.Straight,
        TreePrintIndent.End => TreePrintIndent.Empty,
        _ => indent
    };

    private static string ToIndents(this TreePrintIndent[] rawSignPattern)
    {
        var indentBuilder = new StringBuilder("  ");
        foreach(var tmp in rawSignPattern)
        {
            indentBuilder.Append(tmp.ToIndent());
        }
        return indentBuilder.ToString();
    }

    private static TreePrintIndent[] WithBlock(this TreePrintIndent[] oldPattern, TreePrintIndent newSign)
    {
        var newPattern = new TreePrintIndent[oldPattern.Length + 1];
        Array.Copy(oldPattern, newPattern, oldPattern.Length);
        if(newPattern.Length >= 2)
        {
            newPattern[^2] = newPattern[^2].WithoutSpoke();
        }
        newPattern[^1] = newSign;
        return newPattern;
    }

    private static void PrintTree(this ITreePrintNode node, ICommandInteraction writer, TreePrintIndent[] pattern)
    {
        writer.WriteLine($"{pattern.ToIndents()}{node.Name}");

        var newPattern = pattern.WithBlock(node.Children.Any() ? TreePrintIndent.Straight : TreePrintIndent.Empty);

        foreach(var note in node.Notes)
        {
            writer.WriteLine($"{newPattern.ToIndents()}{note}");
        }
        writer.WriteLine(newPattern.ToIndents());

        newPattern[^1] = TreePrintIndent.Full;

        var lastChild = node.Children.LastOrDefault();
        foreach(var child in node.Children)
        {
            if(child == lastChild)
            {
                newPattern[^1] = TreePrintIndent.End;
            }
            child.PrintTree(writer, newPattern);
        }
    }

    private enum TreePrintIndent { Empty, Straight, End, Full }
}
