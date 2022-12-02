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
using Antmicro.Renode.Exceptions;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class AbsoluteRangeToken : RangeToken
    {
        public AbsoluteRangeToken(string value) : base(value)
        {
            var trimmed = value.TrimStart('<').TrimEnd('>');
            var split = trimmed.Split(',').Select(x => x.Trim()).ToArray();
            var resultValues = ParseNumbers(split);

            Range temp;
            // we need a size, so we add 1 to range.end - range.being result. Range <0x0, 0xFFF> has a size of 0x1000.
            if(!Range.TryCreate(resultValues[0], resultValues[1] - resultValues[0] + 1, out temp))
            {
                throw new RecoverableException("Could not create range. Size has to be non-negative.");
            }
            Value = temp;
        }
    }
}
