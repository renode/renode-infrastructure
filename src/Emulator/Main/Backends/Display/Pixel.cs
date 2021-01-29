//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Backends.Display
{
    public class Pixel
    {
        public Pixel(byte red, byte green, byte blue, byte alpha)
        {
            Alpha = alpha;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public byte Alpha { get; private set; }
        public byte Red   { get; private set; }
        public byte Green { get; private set; }
        public byte Blue  { get; private set; }

        public override bool Equals(object obj)
        {
            return obj is Pixel pixel &&
                   Alpha == pixel.Alpha &&
                   Red == pixel.Red &&
                   Green == pixel.Green &&
                   Blue == pixel.Blue;
        }

        public override int GetHashCode()
        {
            return Alpha << 24 + Red << 16 + Green << 8 + Blue;
        }
    }
}

