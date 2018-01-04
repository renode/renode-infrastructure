//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using System;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class ExecuteCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Provide a command or {0} to execute.".FormatWith(noun));
            writer.WriteLine();
            writer.WriteLine("Available {0}s:".FormatWith(noun));
            foreach(var variable in GetVariables())
            {
                writer.WriteLine("\t{0}".FormatWith(variable));
            }
        }

        [Runnable]
        public virtual void Run(ICommandInteraction writer, params Token[] tokens)
        {
            if(tokens.Length == 1 && tokens[0] is VariableToken)
            {
                var macroLines = GetVariable(tokens[0] as VariableToken).GetObjectValue().ToString().Split('\n');
                foreach(var line in macroLines)
                {
                    if(!monitor.Parse(line, writer))
                    {
                        throw new RecoverableException(string.Format("Parsing line '{0}' failed.", line));
                    }
                }
            }
            else
            {
                if(!monitor.ParseTokens(tokens, writer))
                {
                    throw new RecoverableException("Parsing failed.");
                }
            }
        }

        public ExecuteCommand(Monitor monitor, string name, string noun, Func<VariableToken, Token> getVariable, Func<IEnumerable<string>> getVariables):base(monitor, name, "executes a command or the content of a {0}.".FormatWith(noun))
        {
            GetVariable = getVariable;
            GetVariables = getVariables;
            this.noun = noun;
        }

        private readonly string noun;
        private readonly Func<VariableToken, Token> GetVariable;
        private readonly Func<IEnumerable<string>> GetVariables;
    }
}

