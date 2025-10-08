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
    public class MultilineStringToken : StringToken
    {
        public MultilineStringToken(string value) : base(value)
        {
            if(value.StartsWith(@"""""""", StringComparison.Ordinal))
            {
                Value = value.Substring(3, value.Length - 6);
            }
            else
            {
                Value = value;
            }
        }

        public override string ToString()
        {
            return string.Format("[MultilineString: Value={0}]", Value);
        }
    }
}