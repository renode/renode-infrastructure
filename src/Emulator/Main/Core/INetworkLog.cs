//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core
{
    public interface INetworkLog<out T> : IExternal
    {
        event Action<IExternal, T, T, byte[]> FrameTransmitted;
        event Action<IExternal, T, byte[]> FrameProcessed;
    }
}

