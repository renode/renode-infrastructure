//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Utilities;
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
            Reset();
        }

        public override void Reset()
        {
            isFloating = true;
            isUpdating = false;
            sample = new VoltageSample(0);
            NewSample?.Invoke(Sample);
        }

        public VoltageSample Sample
        {
            get
            {
                Update();
                return sample;
            }
        }

        public bool IsFloating
        {
            get
            {
                Update();
                return isFloating;
            }
        }

        public decimal Volts => Sample.Voltage / 1e6m;

        public event Action<VoltageSample> NewSample;

        private void Update()
        {
            // This check is needed to ensure that accessing `Sample` or `IsFloating` from the
            // `NewSample` callback doesn't cause infinite recurions
            if(isUpdating)
            {
                return;
            }

            isUpdating = true;
            using(DisposableWrapper.New(() => isUpdating = false))
            {
                foreach(var child in Children.OrderBy(x => x.RegistrationPoint.Address).Select(x => x.Peripheral))
                {
                    if((child as IFloatingVoltageSource)?.IsFloating ?? false)
                    {
                        continue;
                    }

                    sample = child.Sample;
                    isFloating = false;
                    NewSample?.Invoke(sample);
                    return;
                }

                sample = new VoltageSample(0);
                isFloating = true;
                NewSample?.Invoke(sample);
            }
        }

        private bool isFloating;
        private VoltageSample sample;
        private bool isUpdating;
    }
}
