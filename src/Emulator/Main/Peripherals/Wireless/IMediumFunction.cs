//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public interface IMediumFunction : IEmulationElement
    {
        string FunctionName { get; }
        bool CanReach(Position from, Position to);
        bool CanTransmit(Position from);
    }
}
