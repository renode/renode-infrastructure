//
// Copyright (c) 2010-2026 Antmicro
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
        public StringToken(string value) : base(value)
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

        public override object GetObjectValue()
        {
            return value;
        }

        public override string ToString()
        {
            return string.Format("[StringToken: Value={0}]", Value);
        }

        public string Value
        {
            get => (string)GetObjectValue();
            protected set => this.value = value;
        }

        private string value;
    }
}
