//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Network;

namespace Antmicro.Renode.Peripherals.Network
{
    public interface IMACInterface : INetworkInterface
    {
        MACAddress MAC { get; set; }
        void ReceiveFrame(EthernetFrame frame);
        event Action<EthernetFrame> FrameReady;
    }
}

