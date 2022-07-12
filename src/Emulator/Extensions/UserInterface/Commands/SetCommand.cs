//
// Copyright (c) 2010-2020 Antmicro
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
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class SetCommand : Command
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("You must provide the name of the {0}.".FormatWith(noun));
            writer.WriteLine();
            writer.WriteLine(String.Format("Usage:\n\r\t{0} {1} \"value\"\n\r\n\r\t{0} {1}\n\r\t\"\"\"\n\r\t[multiline value]\n\r\t\"\"\"", Name, noun));
        }

       

        private void ProcessVariable(ICommandInteraction writer, string variableName, bool initialized = false)
        {
            variableName = GetVariableName(variableName);
            EnableStringEater(variableName, initialized ? 2 : 1); //proper string eater level
            while(GetStringEaterMode() > 0)
            {
                writer.Write("> ");
                var line = writer.ReadLine();
                if(line == null)
                {
                    DisableStringEater();
                    break;
                }
                monitor.Parse(line, writer);
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken variable)
        {
            ProcessVariable(writer, variable.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, VariableToken variable)
        {
            ProcessVariable(writer, variable.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken variable, MultilineStringTerminatorToken dummy)
        {
            ProcessVariable(writer, variable.Value, true);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, VariableToken variable, MultilineStringTerminatorToken dummy)
        {
            ProcessVariable(writer, variable.Value, true);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken variable, Token value)
        {
            var varName = variable.Value;
           
            varName = GetVariableName(varName);
            SetVariable(varName, value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, VariableToken variable, Token value)
        {
            Run(writer, new LiteralToken(variable.Value), value);
        }

        private readonly Action<string, Token> SetVariable;
        private readonly Action<string, int> EnableStringEater;
        private readonly Func<string, string> GetVariableName;
        private readonly Action DisableStringEater;
        private readonly Func<int> GetStringEaterMode;
        private readonly String noun;

        public SetCommand(Monitor monitor, String name, string noun, Action<string, Token> setVariable, Action<string, int> enableStringEater, Action disableStringEater, Func<int> getStringEaterMode, 
            Func<string, string> getVariableName) : base(monitor, name, "sets {0}.".FormatWith(noun))
        {
            EnableStringEater = enableStringEater;
            DisableStringEater = disableStringEater;
            GetStringEaterMode = getStringEaterMode;
            GetVariableName = getVariableName;
            SetVariable = setVariable;
            this.noun = noun;
        }
    }
}

