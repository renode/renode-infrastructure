//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class ExecutionToken : Token
    {
        public ExecutionToken(string value) : base(value)
        {
            if(value.StartsWith("`", StringComparison.Ordinal))
            {
                Value = value.Substring(1, value.Length - 2);
            }
            else
            {
                Value = value;
            }
        }
        public string Value {get;set;}

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[ExecutionToken: Value={0}]", Value);
        }
    }
}

