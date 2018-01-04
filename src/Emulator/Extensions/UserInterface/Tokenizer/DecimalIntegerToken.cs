//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//


namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class DecimalIntegerToken : Token
    {
        public DecimalIntegerToken(string value) : base(value)
        {
            Value = long.Parse(value);
        }

        public long Value { get; private set; }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[NumericToken: Value={0}]", Value);
        }
    }
}

