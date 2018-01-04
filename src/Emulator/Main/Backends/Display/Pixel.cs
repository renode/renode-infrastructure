//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
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
    }
}

