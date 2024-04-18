//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class PathToken : StringToken
    {
        public PathToken(string value) : base(value)
        {
            Value = value.TrimStart('@').Replace(@"\ ", " ");
        }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[PathToken: Value={0}]", Value);
        }
    }
}

