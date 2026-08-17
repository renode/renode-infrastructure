//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    /// <summary>
    /// If the wire is driven by multiple inputs simultaneously the input with the lowest
    /// registration number will drive the wire
    /// </summary>
    public class AnalogWire : SimpleContainer<IRESDSampleSource<VoltageSample>>, IFloatingVoltageSource
    {
        public AnalogWire(IMachine machine) : base(machine)
        {
        }

        public override void Reset()
        {
            // Intentionally left empty
        }

        public VoltageSample Sample
        {
            get
            {
                var (sample, _) = GetState();
                return sample;
            }
        }

        public bool IsFloating
        {
            get
            {
                var (_, isFloating) = GetState();
                return isFloating;
            }
        }

        public decimal Volts => Sample.Voltage / 1e6m;

        private (VoltageSample, bool) GetState()
        {
            foreach(var child in Children.OrderBy(x => x.RegistrationPoint.Address).Select(x => x.Peripheral))
            {
                if((child as IFloatingVoltageSource)?.IsFloating ?? false)
                {
                    continue;
                }

                return (child.Sample, false);
            }

            return (new VoltageSample(0), true);
        }
    }
}
