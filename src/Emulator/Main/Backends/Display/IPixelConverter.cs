//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿namespace Antmicro.Renode.Backends.Display
{
    public interface IPixelConverter
    {
        void Convert(byte[] inBuffer, byte[] clutBuffer, ref byte[] outBuffer);
        void Convert(byte[] inBuffer, ref byte[] outBuffer);

        PixelFormat Input { get; }
        PixelFormat Output { get; }
    }
}

