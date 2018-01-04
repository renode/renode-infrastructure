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
    public interface IDisplay
    {
        void DrawFrame(byte[] frame);
        void DrawFrame(IntPtr pointer);
        void SetDisplayParameters(int width, int height, PixelFormat colorFormat);
    }
}

