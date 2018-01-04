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
    
    public class StringToken : Token
    {
        public StringToken(string value):base(value)
        {
            var trim = false;
            if(value.StartsWith("\"", StringComparison.Ordinal))
            {
                trim = true;
                value = value.Replace("\\\"", "\"");
            }
            else if(value.StartsWith("'", StringComparison.Ordinal))
            {
                trim = true;
                value = value.Replace("\\\'", "\'");
            }
            if(trim)
            {
                Value = value.Substring(1, value.Length - 2);
            }
            else
            {
                Value = value;
            }
        }

        public string Value { get; protected set; }
        
        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[StringToken: Value={0}]", Value);
        }
    }

}

