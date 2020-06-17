//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class LiteX_I2S_Slave : LiteX_I2S
    {
        public LiteX_I2S_Slave(Machine machine, DataFormat format, uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels) : base(machine, format, sampleWidthBits, samplingRateHz, 31)
        {
            decoder = new PCMDecoder(sampleWidthBits, samplingRateHz, numberOfChannels, false);
        }

        public void Start(int freq)
        {
            // stop a previous thread (if any)
            Stop();

            pusher = machine.ObtainManagedThread(PushSamples, freq);
            pusher.Start();
        }

        public void Stop()
        {
            if(pusher != null)
            {
                pusher.Stop();
                pusher = null;
            }
        }

        public void FeedSample(uint sample, int count = 1)
        {
            for(var i = 0; i < count; i++)
            {
                decoder.LoadSample(sample);
            }
        }

        public void LoadPCM(string path)
        {
            decoder.LoadFile(path);
        }

        public void ForceBufferClear()
        {
            lock(buffer)
            {
                buffer.Clear();
                UpdateInterrupts();
            }
        }

        protected override bool IsReady()
        {
            return (buffer.Count >= fifoIrqThreshold);
        }

        private void PushSamples()
        {
            var currentTimestamp = machine.LocalTimeSource.ElapsedVirtualTime;
            if(!previousPushTimestamp.HasValue)
            {
                previousPushTimestamp = currentTimestamp;
                return;
            }

            var timeDiff = currentTimestamp - previousPushTimestamp.Value;
            previousPushTimestamp = currentTimestamp;

            var samples = decoder.GetSamplesTime((uint)(timeDiff.InMicroseconds / 1000));
            foreach(var sample in samples)
            {
                buffer.Enqueue(sample);
            }
            this.Log(LogLevel.Noisy, "Enqueued {0} samples", samples);
            UpdateInterrupts();
        }

        private IManagedThread pusher;
        private TimeInterval? previousPushTimestamp;

        private readonly PCMDecoder decoder;
    }
}
