//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
namespace Antmicro.Renode.Utilities.GDB
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExecuteAttribute : Attribute
    {
        public ExecuteAttribute(string mnemonic)
        {
            Mnemonic = mnemonic;
        }

        public string Mnemonic { get; private set; }
    }
}

