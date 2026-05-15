//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class Potentiometer : IRESDSampleSource<VoltageSample>
    {
        public Potentiometer(uint minMicroVolt, uint maxMicroVolt)
        {
            if(minMicroVolt > maxMicroVolt)
            {
                throw new ConstructionException($"'{nameof(minMicroVolt)}' value should be less or equal to '{nameof(maxMicroVolt)}' value");
            }

            Sample = new VoltageSample(min);

            min = minMicroVolt;
            max = maxMicroVolt;
            percentage = 0;
        }

        public Potentiometer(double minVolt, double maxVolt) : this((uint)(minVolt * 1e6), (uint)(maxVolt * 1e6))
        {
        }

        public void Reset()
        {
            // The inputs are intentionally not reset on machine reset.
        }

        public void SetPercentage(decimal newPercentage)
        {
            var newValue = Misc.RemapNumber(newPercentage, 0, 100, min, max);
            if(!newValue.HasValue)
            {
                throw new RecoverableException($"Invalid percentage '{newPercentage}%'");
            }

            Sample = new VoltageSample((uint)newValue.Value); // The cast to uint is because the value is in µV, not in V
            percentage = newPercentage;
            NewSample?.Invoke(Sample);
        }

        public string GetVoltage()
        {
            return Sample.ToString();
        }

        public VoltageSample Sample { get; private set; }

        public decimal Percentage
        {
            get => percentage;
            set => SetPercentage(value);
        }

        public event Action<VoltageSample> NewSample;

        private decimal percentage;

        private readonly uint min;
        private readonly uint max;
    }
}
