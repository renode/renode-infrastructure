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
    public class NullToken:Token
    {
        public NullToken(string value):base(value)
        {
        }

        public object Value {get{return null;}}

        public override object GetObjectValue()
        {
            return Value;
        }
    }
}

