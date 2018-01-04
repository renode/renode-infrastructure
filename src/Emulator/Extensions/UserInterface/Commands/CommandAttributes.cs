//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;

namespace Antmicro.Renode.UserInterface.Commands
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RunnableAttribute:Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ValuesAttribute:Attribute
    {
        public IEnumerable<object> Values { get; set; }

        public ValuesAttribute(params object[] values)
        {
            Values = new List<object>(values);
        }
    }
}

