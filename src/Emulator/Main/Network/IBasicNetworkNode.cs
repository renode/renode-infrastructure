//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Network
{
    public delegate bool BasicNetworkSendDataDelegate<TData, TAddress>(TData data, TAddress source, TAddress destination);

    public interface IBasicNetworkNode<TData, TAddress> : IPeripheral
    {
        void ReceiveData(TData data, TAddress source, TAddress destination);
        event BasicNetworkSendDataDelegate<TData, TAddress> TrySendData;
        TAddress NodeAddress { get; }
    }
}
