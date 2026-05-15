//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class NamedDiscreteValues : Dictionary<string, VoltageSample>, IRESDSampleSource<VoltageSample>
    {
        public NamedDiscreteValues(Dictionary<string, uint> namedMicroVoltValues)
        {
            if(namedMicroVoltValues.Count == 0)
            {
                throw new ConstructionException($"{nameof(NamedDiscreteValues)} must have at least one state:value");
            }
            foreach(var item in namedMicroVoltValues)
            {
                this.Add(item.Key, new VoltageSample(item.Value));
            }

            CurrentState = this.First().Key;
        }

        public NamedDiscreteValues(Dictionary<string, decimal> namedVoltValues)
        {
            if(namedVoltValues.Count == 0)
            {
                throw new ConstructionException($"{nameof(NamedDiscreteValues)} must have at least one state:value");
            }
            foreach(var item in namedVoltValues)
            {
                this.Add(item.Key, new VoltageSample(item.Value));
            }

            CurrentState = this.First().Key;
        }

        public void Reset()
        {
            // The inputs are intentionally not reset on machine reset.
        }

        public string CurrentState
        {
            get => currentState;

            set
            {
                if(!this.ContainsKey(value))
                {
                    throw new RecoverableException($"Unknown state '{value}'");
                }
                currentState = value;
                NewSample?.Invoke(Sample);
            }
        }

        public VoltageSample Sample => this[CurrentState];

        public event Action<VoltageSample> NewSample;

        private string currentState;
    }
}
