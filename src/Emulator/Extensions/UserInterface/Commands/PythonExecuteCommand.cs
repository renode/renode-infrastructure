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

namespace Antmicro.Renode.UserInterface.Commands
{
    public class PythonExecuteCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Provide a command or variable to execute.");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, VariableToken variable)
        {
            var value = GetVariable(variable);

            if(value is StringToken stringValue)
            {
                Run(writer, stringValue);
            }
            else
            {
                writer.WriteError("Variable type has to be a string.");
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken command)
        {
            Execute(command.Value, writer);
        }

        private readonly Func<VariableToken, Token> GetVariable;
        private readonly Action<string, ICommandInteraction> Execute;

        public PythonExecuteCommand(Monitor monitor, Func<VariableToken, Token> getVariable, Action<String, ICommandInteraction> execute) 
            :base(monitor, "python", "executes the provided python command.", "py") 
        {
            GetVariable = getVariable;
            Execute = execute;
        }
    }
}

