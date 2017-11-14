//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.CAN
{
    [Icon("can")]
    public interface ICAN : IPeripheral
    {
        event Action<int, byte[]> FrameSent;
        void OnFrameReceived(int id, byte[] data);
    }
}

