//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class LiteralToken : Token
    {
        public LiteralToken(string value) : base(value)
        {
            Value = value;
        }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[Literal: Value={0}]", Value);
        }

        public string Value { get; private set; }
    }
}