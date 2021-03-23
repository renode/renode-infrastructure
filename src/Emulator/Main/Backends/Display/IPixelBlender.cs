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
    public interface IPixelBlender
    {
        void Blend(byte[] backBuffer, byte[] frontBuffer, ref byte[] output, Pixel background = null, byte backBufferAlphaMultiplayer = 0xFF, PixelBlendingMode backgroundBlendingMode = PixelBlendingMode.Multiply, byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply);

        void Blend(byte[] backBuffer, byte[] backClutBuffer, byte[] frontBuffer, byte[] frontClutBuffer, ref byte[] output, Pixel background = null, byte backBufferAlphaMultiplayer = 0xFF, PixelBlendingMode backgroundBlendingMode = default(PixelBlendingMode), byte frontBufferAlphaMultiplayer = 0xFF, PixelBlendingMode foregroundBlendingMode = PixelBlendingMode.Multiply);

        PixelFormat BackBuffer { get; }
        PixelFormat FrontBuffer { get; }
        PixelFormat Output { get; }
    }

    public enum PixelBlendingMode
    {
        NoModification,
        Replace,
        Multiply
    }
}

