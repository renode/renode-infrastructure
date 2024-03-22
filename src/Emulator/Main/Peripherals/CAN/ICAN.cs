//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.CAN
{
    [Icon("can")]
    public interface ICAN : IPeripheral, INetworkInterface
    {
        event Action<CANMessageFrame> FrameSent;
        void OnFrameReceived(CANMessageFrame message);
    }
}

