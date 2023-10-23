//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Network;

namespace Antmicro.Renode.Core
{
    public interface INetworkLogSwitch : INetworkLog<IMACInterface>
    {
    }
}

