//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class TimeIntervalToken : Token
    {
        public static implicit operator TimeIntervalToken(FloatToken token)
        {
            return new TimeIntervalToken(token.OriginalValue);
        }

        public static implicit operator TimeIntervalToken(DecimalIntegerToken token)
        {
            return new TimeIntervalToken(token.OriginalValue);
        }

        public TimeIntervalToken(string value) : base(value)
        {
            Value = (TimeInterval)value;
        }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return $"[TimeIntervalToken: Value={Value}]";
        }

        public TimeInterval Value { get; set; }
    }
}
