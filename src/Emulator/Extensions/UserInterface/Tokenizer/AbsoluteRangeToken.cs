//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class AbsoluteRangeToken : RangeToken
    {
        public AbsoluteRangeToken(string value) : base(value)
        {
            var trimmed = value.TrimStart('<').TrimEnd('>');
            var split = trimmed.Split(',').Select(x => x.Trim()).ToArray();
            var resultValues = ParseNumbers(split);

            if(resultValues[0] > resultValues[1])
            {
                // split is used instead of resultValues to print numbers using the same format as input.
                throw new RecoverableException(
                    "Could not create range; the start address can't be higher than the end address.\n"
                    + $"Use '<{split[0]} {split[1]}>' without a comma if the second argument is size."
                );
            }
            Value = resultValues[0].To(resultValues[1]);
        }
    }
}
