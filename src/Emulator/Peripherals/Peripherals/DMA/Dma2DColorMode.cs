//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Backends.Display;

namespace Antmicro.Renode.Peripherals.DMA
{
    // ordering of entry is taken from the documentation and should not be altered!
    internal enum Dma2DColorMode
    {
        ARGB8888,
        RGB888,
        RGB565,
        ARGB1555,
        ARGB4444,
        L8,
        AL44,
        AL88,
        L4,
        A8,
        A4
    }

    // ordering of entry is taken from the documentation and should not be altered!
    internal enum Dma2DAlphaMode
    {
        NoModification,
        Replace,
        Combine
    }

    internal static class Dma2DColorModeExtensions
    {
        static Dma2DColorModeExtensions()
        {
            cache = new Dictionary<Dma2DColorMode, PixelFormat>();
            foreach(Dma2DColorMode mode in Enum.GetValues(typeof(Dma2DColorMode)))
            {
                PixelFormat format;
                if(!Enum.TryParse(mode.ToString(), out format))
                {
                    throw new ArgumentException(string.Format("Could not find pixel format matching DMA2D color mode: {0}", mode));
                }

                cache[mode] = format;
            }
        }

        public static PixelFormat ToPixelFormat(this Dma2DColorMode mode)
        {
            PixelFormat result;
            if(!cache.TryGetValue(mode, out result))
            {
                throw new ArgumentException(string.Format("Unsupported color mode: {0}", mode));
            }

            return result;
        }

        private static Dictionary<Dma2DColorMode, PixelFormat> cache;
    }

    internal static class Dma2DAlphaModeExtensions
    {
        public static PixelBlendingMode ToPixelBlendingMode(this Dma2DAlphaMode mode)
        {
            switch(mode)
            {
                case Dma2DAlphaMode.NoModification:
                    return PixelBlendingMode.NoModification;
                case Dma2DAlphaMode.Replace:
                    return PixelBlendingMode.Replace;
                case Dma2DAlphaMode.Combine:
                    return PixelBlendingMode.Multiply;
            }
            return PixelBlendingMode.NoModification;
        }
    }
}

