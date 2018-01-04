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
    public class HexToken : DecimalIntegerToken
    {
        public HexToken(string value):base(Convert.ToInt64(value.Split('x')[1], 16).ToString())
        {
        }

        public override string ToString()
        {
            return string.Format("[HexToken: Value={0}]", Value);
        }
    }
}

