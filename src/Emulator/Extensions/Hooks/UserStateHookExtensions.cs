//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Hooks
{
    public static class UserStateHookExtensions
    {
        public static void AddUserStateHook(this IMachine machine, string stateName, string pythonScript)
        {
            var engine = new UserStatePythonEngine(machine, pythonScript);
            machine.AddUserStateHook(x => x == stateName, engine.Hook);
        }
    }
}

