//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Network;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.HostInterfaces.Network
{
    public interface ITapInterface : IMACInterface, IHostMachineElement, IDisposable
    {
        string InterfaceName { get; }
    }
}

