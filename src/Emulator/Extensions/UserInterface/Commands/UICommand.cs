//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands;

public class UICommand(Monitor monitor) : Command(monitor, "ui", "controls ui")
{
    [Runnable]
    public void Run(ICommandInteraction writer, LiteralToken action)
    {
        Run(writer, action, Array.Empty<Token>());
    }

    [Runnable]
    public void Run(ICommandInteraction writer, LiteralToken action, params Token[] arguments)
    {
        if(!TryParseAction(writer, action, arguments, out var parsedAction))
        {
            return;
        }

        monitor.HandleUICommand(parsedAction.Name, parsedAction.Arguments);
    }

    private bool TryParseAction(ICommandInteraction writer, LiteralToken action, Token[] arguments, out UIAction parsedAction)
    {
        parsedAction = default;

        if(arguments.Length % 3 != 0)
        {
            writer.WriteError("Invalid arguments");
            return false;
        }

        var parsedArguments = new Dictionary<string, object>();
        foreach(var chunk in arguments.Chunk(3))
        {
            var keyToken = chunk[0];
            var eqToken = chunk[1];
            var valueToken = chunk[2];

            if(keyToken is not LiteralToken key)
            {
                writer.WriteError($"'{keyToken.OriginalValue}' is invalid argument name");
                return false;
            }

            if(eqToken is not EqualityToken)
            {
                writer.WriteError("Invalid command syntax");
                return false;
            }

            parsedArguments[key.Value] = valueToken.GetObjectValue();
        }

        parsedAction = new UIAction(action.Value, parsedArguments);
        return true;
    }

    private class UIAction(string name, IDictionary<string, object> arguments)
    {
        public string Name => name;

        public IDictionary<string, object> Arguments => arguments;
    }
}
