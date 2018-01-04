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
    public class BooleanToken : Token
    {
        public BooleanToken(string value):base(value)
        {
            Value = Boolean.Parse(value);
        }

        public bool Value {get;set;}
        
        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[BooleanToken: Value={0}]", Value);
        }
    }
}

