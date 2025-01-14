//
// Copyright (c) 2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.CFU
{
    public interface ICFU : IPeripheral
    {
        ICPU ConnectedCpu { get; set; }
        string SimulationFilePath { get; }
    }
}
