//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Sound;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class LiteX_I2S_Master : LiteX_I2S, IDisposable
    {
        public LiteX_I2S_Master(IMachine machine, DataFormat format, uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels) : base(machine, format, sampleWidthBits, samplingRateHz)
        {
            encoder = new PCMEncoder(sampleWidthBits, samplingRateHz, numberOfChannels, false);
        }

        public void ForceFlush()
        {
            encoder.FlushBuffer();
        }

        public void Dispose()
        {
            encoder.Dispose();
        }

        public void SetBuffering(uint ms)
        {
            encoder.SetBufferingByTime(ms);
        }

        public string Output
        {
            get => encoder.Output;

            set
            {
                encoder.Output = value;
            }
        }

        protected override bool IsReady()
        {
            return (buffer.Count < fifoIrqThreshold);
        }

        protected override void EnqueueSampleInner(uint sample)
        {
            encoder.AcceptSample(sample);
            this.Log(LogLevel.Noisy, "Encoded a sample");
        }

        private readonly PCMEncoder encoder;
    }
}
