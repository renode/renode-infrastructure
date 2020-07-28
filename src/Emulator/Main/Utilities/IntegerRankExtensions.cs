//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Utilities
{
    public enum Rank
    {
        Units = 1,
        Tens = 10
    }

    public static class IntegerRankExtensions
    {
        public static int ReadRank(this int value, Rank rank)
        {
            switch(rank)
            {
                case Rank.Tens:
                    return (value / 10) % 10;
                case Rank.Units:
                    return value % 10;
                default:
                    throw new ArgumentException($"Unsupported rank: {rank}");
            }
        }

        // Returns 'current' with updated tens or units
        public static int WithUpdatedRank(this int current, int value, Rank rank)
        {
            if(value < 0 || value > 9)
            {
                throw new ArgumentException($"Expected a single-digit value, but got: {value}");
            }

            switch(rank)
            {
                case Rank.Tens:
                    return current + 10 * (value - current.ReadRank(Rank.Tens));
                case Rank.Units:
                    return current + (value - current.ReadRank(Rank.Units));
                default:
                    throw new ArgumentException($"Unsupported rank: {rank}");
            }
        }
    }
}
