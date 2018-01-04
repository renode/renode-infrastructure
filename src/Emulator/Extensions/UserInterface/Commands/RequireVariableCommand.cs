//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class RequireVariableCommand : AutoLoadCommand
    {
        [Runnable]
        public void Run(Token token)
        {
            // THIS METHOD IS INTENTIONALY LEFT BLANK
        }

        public RequireVariableCommand(Monitor monitor) : base(monitor, "require", "verifies the existence of a variable.")
        {
        }
    }
}

