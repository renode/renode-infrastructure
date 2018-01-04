//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Globalization;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class FloatToken : Token
    {
        public FloatToken(string value) : base(value)
        {
            Value = float.Parse(value, CultureInfo.InvariantCulture);
        }

        public float Value { get; private set; }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[FloatToken: Value={0}]", Value);
        }
    }
}

