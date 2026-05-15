//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class ADCChannelSource : IRESDSampleSource<VoltageSample>
    {
        public ADCChannelSource(VoltageSample sample)
        {
            defaultSample = sample;
            resetSample = sample;
        }

        public ADCChannelSource(uint defaultValueMicroVolts = 0)
        {
            defaultSample = new VoltageSample(defaultValueMicroVolts);
            resetSample = defaultSample;
        }

        public void Reset()
        {
            resdStream?.Dispose();
            resdStream = null;
            defaultSample = resetSample;
        }

        public void FeedSamplesFromRESD(ReadFilePath filePath, uint resdChannel = 0,
                                        RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.CurrentVirtualTime,
                                        long sampleOffsetTime = 0)
        {
            try
            {
                resdStream = this.CreateRESDStream<VoltageSample>(filePath, resdChannel, sampleOffsetType, sampleOffsetTime);
            }
            catch(RESDException)
            {
                resdStream?.Dispose();
                throw new RecoverableException($"Could not load RESD channel {resdChannel} from {filePath}");
            }
        }

        public VoltageSample Sample
        {
            get
            {
                if(resdStream != null
                   && resdStream.TryGetCurrentSample(this, sample => sample, out var sample, out _) == RESDStreamStatus.OK)
                {
                    return sample;
                }
                else
                {
                    this.DebugLog("Returning default sample, voltage = {0}", DefaultSample);
                    return DefaultSample;
                }
            }

            set
            {
                resdStream?.Dispose();
                resdStream = null;
                DefaultSample = value;
                NewSample?.Invoke(value);
            }
        }

        public decimal Volts
        {
            get
            {
                return (decimal)(Sample.Voltage) / 1e6m;
            }

            set
            {
                Sample = new VoltageSample(value);
            }
        }

        public VoltageSample DefaultSample
        {
            get => defaultSample;
            set
            {
                defaultSample = value;
            }
        }

        public event Action<VoltageSample> NewSample;

        private RESDStream<VoltageSample> resdStream;

        private VoltageSample defaultSample;

        private readonly VoltageSample resetSample;
    }

    public class ADCDefaultChannelSource : ADCChannelSource
    {
        public ADCDefaultChannelSource() : base()
        {
        }
    }
}
