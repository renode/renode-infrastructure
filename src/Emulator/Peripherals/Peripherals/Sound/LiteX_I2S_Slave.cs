//
// Copyright (c) 2010-2022 Antmicro
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
        public LiteX_I2S_Slave(IMachine machine, DataFormat format, uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels) : base(machine, format, sampleWidthBits, samplingRateHz)
        {
            decoder = new PCMDecoder(sampleWidthBits, samplingRateHz, numberOfChannels, false, this);
            SamplesPushFrequency = 100;
        }

        public override void Reset()
        {
            base.Reset();
            TryStopPusher();

            decoder.Reset();
            globalEnabled = false;
        }

        public void FeedSample(uint sample, int count = 1)
        {
            for(var i = 0; i < count; i++)
            {
                decoder.LoadSample(sample);
            }

            TryStartPusher();
        }

        public void LoadPCM(string path)
        {
            decoder.LoadFile(path);
            TryStartPusher();
        }

        public void ForceBufferClear()
        {
            lock(buffer)
            {
                buffer.Clear();
                UpdateInterrupts();
            }
        }

        public uint SamplesPushFrequency { get; set; }

        protected override bool IsReady()
        {
            return (buffer.Count >= fifoIrqThreshold);
        }

        protected override void HandleEnable(bool enabled)
        {
            globalEnabled = enabled;
            if(enabled)
            {
                TryStartPusher();
            }
            else
            {
                TryStopPusher();
            }
        }

        private bool TryStartPusher()
        {
            if(!globalEnabled)
            {
                return false;
            }

            // stop a previous thread (if any)
            TryStopPusher();

            pusher = machine.ObtainManagedThread(PushSamples, SamplesPushFrequency);
            pusher.Start();

            return true;
        }

        private bool TryStopPusher()
        {
            if(pusher == null)
            {
                return false;
            }

            pusher.Stop();
            pusher = null;
            previousPushTimestamp = null;
            return true;
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

            var any = false;
            var samples = decoder.GetSamplesByTime((uint)(timeDiff.TotalMicroseconds / 1000));
            foreach(var sample in samples)
            {
                any = true;
                buffer.Enqueue(sample);
            }
            UpdateInterrupts();

            if(!any)
            {
                TryStopPusher();
            }
        }

        private IManagedThread pusher;
        private TimeInterval? previousPushTimestamp;
        private bool globalEnabled;

        private readonly PCMDecoder decoder;
    }
}
