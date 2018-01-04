//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Backends.Display
{
    public enum PixelFormat
    {
        A4,
        L4,
        A8,
        L8,
        AL44,
        AL88,
        RGB565,
        BGR565,
        BGR888,
        RGB888,
        ARGB1555,
        ARGB4444,
        RGBA4444,
        ABGR4444,
        BGRA4444,
        XRGB4444,
        RGBX4444,
        XBGR4444,
        BGRX4444,
        BGRA8888,
        RGBA8888,
        ABGR8888,
        ARGB8888,
        BGRX8888,
        RGBX8888,
        XRGB8888,
        XBGR8888
    }

    public enum ColorType
    {
        R,
        G,
        B,
        A,
        X,
        L
    }

    public static class PixelFormatExtensions
    {
        static PixelFormatExtensions()
        {
            var values = Enum.GetValues(typeof(PixelFormat));
            depths = new int[values.Length];
            for(var i = 0; i < depths.Length; i++)
            {
                // here we check if pixel format enum value has proper value
                // i.e. calculated from the position in enum (not set explicitly)
                var value = (int)values.GetValue(i);
                if(value != i)
                {
                    throw new ArgumentException(string.Format("Unexpected pixel format value: {0}", (PixelFormat)value));
                }
                depths[value] = GetColorsLengths((PixelFormat)value).Sum(x => x.Value) / 8;
            }
        }

        /// <summary>
        /// Returns a number of bytes needed to encode the color.
        /// </summary>
        /// <param name="format">Color format.</param>
        public static int GetColorDepth(this PixelFormat format)
        {
            if(format < 0 || (int)format >= depths.Length)
            {
                throw new ArgumentException(string.Format("Unsupported pixel format: {0}", format));
            }

            return depths[(int)format];
        }

        /// <summary>
        /// Calculates number of bits needed to encode each color channel.
        /// </summary>
        /// <param name="format">Color format</param>
        public static Dictionary<ColorType, byte> GetColorsLengths(this PixelFormat format)
        {
            var bits = new Dictionary<ColorType, byte>();

            var offset = 0;
            var formatAsCharArray = format.ToString().ToCharArray();
            var firstNumberPosition = formatAsCharArray.Length / 2;

            while (offset < firstNumberPosition)
            {
                ColorType colorType;
                if(!Enum.TryParse(formatAsCharArray[offset].ToString(), out colorType))
                {
                    throw new ArgumentException();
                }

                bits.Add(colorType, (byte)(formatAsCharArray[firstNumberPosition + offset] - '0'));
                offset++;
            }

            return bits;
        }

        private static int[] depths;
    }
}

