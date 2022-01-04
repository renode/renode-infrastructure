//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IPerCoreRegistration : IRegistrationPoint
    {
         ICPU CPU { get; }
    }
}

