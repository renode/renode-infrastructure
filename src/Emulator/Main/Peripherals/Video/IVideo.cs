//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Backends.Display;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.Video
{
    public interface IVideo : IPeripheral
    {
        event Action<byte[]> FrameRendered;
        event Action<int, int, PixelFormat, Endianess> ConfigurationChanged;
    }
}

