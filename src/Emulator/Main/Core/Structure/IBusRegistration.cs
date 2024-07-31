//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Core.Structure
{
    /// <summary>
    /// Interface to mark a registration with an address on the bus
    /// (as opposed to a numbered or null registration).
    /// </summary>
    public interface IBusRegistration : IRegistrationPoint
    {
        ICPU CPU { get; }
        ICluster<ICPU> Cluster { get; }
        ulong Offset { get; }
        ulong StartingPoint { get; }
    }
}
