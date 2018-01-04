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
    public class RightBraceToken : Token 
    {
        public RightBraceToken(string token) : base(token)
        {
        }

        public override object GetObjectValue()
        {
            return OriginalValue;
        }

        public override string ToString()
        {
            return string.Format("[RightBraceToken]");
        }
    }
}

