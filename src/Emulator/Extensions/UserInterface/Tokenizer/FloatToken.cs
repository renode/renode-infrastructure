//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Globalization;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class FloatToken : Token
    {
        public FloatToken(string value) : base(value)
        {
            Value = float.Parse(value, CultureInfo.InvariantCulture);
        }

        public override object GetObjectValue()
        {
            return Value;
        }

        // Parsing the text at the precision of the target type keeps the digits that rounding to float loses.
        public override object GetObjectValue(Type targetType)
        {
            switch(Type.GetTypeCode(Nullable.GetUnderlyingType(targetType) ?? targetType))
            {
            case TypeCode.Decimal when decimal.TryParse(OriginalValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue):
                return decimalValue;
            case TypeCode.Double when double.TryParse(OriginalValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue):
                return doubleValue;
            }
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[FloatToken: Value={0}]", Value);
        }

        public float Value { get; private set; }
    }
}