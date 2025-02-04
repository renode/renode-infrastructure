//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Network;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public interface IRadio : IPeripheral, INetworkInterface
    {
        int Channel { get; set; }
        event Action<IRadio, byte[]> FrameSent;
        void ReceiveFrame(byte[] frame, IRadio sender);
    }
}

