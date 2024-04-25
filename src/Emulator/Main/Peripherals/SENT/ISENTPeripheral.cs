//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.SENT
{
    public enum SENTEdge
    {
        Falling,
        Rising,
    }

    public interface ISENTPeripheral : IPeripheral
    {
        bool TransmissionEnabled { get; set; }
        event Action<SENTEdge> SENTEdgeChanged;
    }
}
