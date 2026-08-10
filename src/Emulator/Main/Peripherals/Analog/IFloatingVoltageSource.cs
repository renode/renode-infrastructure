//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public interface IFloatingVoltageSource : IRESDSampleSource<VoltageSample>
    {
        bool IsFloating { get; }
    }
}
