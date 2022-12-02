//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using System.Linq;
using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class RelativeRangeToken : RangeToken
    {
        public RelativeRangeToken(string value) : base(value)
        {
            var trimmed = value.TrimStart('<').TrimEnd('>');
            var split = trimmed.Split(new []{ ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            var resultValues = ParseNumbers(split);

            Value = new Range(resultValues[0], resultValues[1]);
        }
    }
}
