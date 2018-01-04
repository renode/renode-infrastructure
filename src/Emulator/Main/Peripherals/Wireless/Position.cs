//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public struct Position
    {
        public Position(decimal x, decimal y, decimal z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public decimal X { get; private set; }
        public decimal Y { get; private set; }
        public decimal Z { get; private set; }
    }
}
