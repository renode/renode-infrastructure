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
    public class MultilineStringTerminatorToken : StringToken
    {
        public MultilineStringTerminatorToken(string value) : base(value)
        {
        }

        public override string ToString()
        {
            return string.Format("[MultilineStringTerminatorToken]");
        }
    }
}

