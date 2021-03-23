//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿namespace Antmicro.Renode.Backends.Display
{
    public interface IPixelConverter
    {
        /// <summary>
        /// Converts pixels stored in <paramref name="inBuffer"/> and having the <see cref="Input"/> format, into pixels having the <see cref="Output"/> format, and stores the result in <paramref name="outBuffer"/>. 
        /// </summary>
        /// <param name="inBuffer">input buffer</param>
        /// <param name="clutBuffer">buffer containing the LUT for each input pixel. Used if Input uses indexed colors (L mode)</param>
        /// <param name="alpha">fixed alpha value</param>
        /// <param name="alphaReplaceMode">controls how <paramref name="alpha"/> gets used to compute the alpha of output pixels</param>
        /// <param name="outBuffer">output buffer</param>
        void Convert(byte[] inBuffer, byte[] clutBuffer, byte alpha, PixelBlendingMode alphaReplaceMode, ref byte[] outBuffer);

        /// <summary>
        /// Converts pixels stored in <paramref name="inBuffer"/> and having the <see cref="Input"/> format, into pixels having the <see cref="Output"/> format, and stores the result in <paramref name="outBuffer"/>.
        /// </summary>
        /// <param name="inBuffer">input buffer</param>
        /// <param name="outBuffer">output buffer</param>
        void Convert(byte[] inBuffer, ref byte[] outBuffer);

        /// <summary>
        /// Pixel format of the conversion input
        /// </summary>
        PixelFormat Input { get; }

        /// <summary>
        /// Pixel format of the conversion output
        /// </summary>
        PixelFormat Output { get; }
    }
}

