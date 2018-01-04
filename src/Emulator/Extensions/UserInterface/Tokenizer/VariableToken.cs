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
    public class VariableToken : Token
    {
        public VariableToken(string value):base(value)
        {
            Value = value.TrimStart('$');
        }

        public string Value { get; private set; }
        
        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[VariableToken: Value={0}]", Value);
        }
    }
}

