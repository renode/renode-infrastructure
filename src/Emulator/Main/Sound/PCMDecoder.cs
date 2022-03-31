//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class PCMDecoder
    {
        public PCMDecoder(uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels, bool concatenatedChannels, IPeripheral loggingParent = null) 
        {
            if(concatenatedChannels)
            {
               throw new ConstructionException("Concatenated channels are currently not supported"); 
            }

            if(sampleWidthBits != 8u && sampleWidthBits != 16u && sampleWidthBits != 24u && sampleWidthBits != 32u)
            {
               throw new ConstructionException($"Not supported sample width: {0}. Only 8/16/24/32 bits are currently supported."); 
            }

            samples = new Queue<uint>();

            this.sampleWidthBits = sampleWidthBits;
            this.numberOfChannels = numberOfChannels;
            this.samplingRateHz = samplingRateHz;

            this.loggingParent = loggingParent;
        }

        public void Reset()
        {
            lock(samples)
            {
                samples.Clear();
            }
        }

        public void LoadFile(ReadFilePath path)
        {
            var sampleSize = (int)(sampleWidthBits / 8);

            lock(samples)
            {
                using(var br = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    while(true)
                    {
                        var bytes = br.ReadBytes(sampleSize);
                        if(bytes.Length == 0)
                        {
                            break;
                        }

                        var sample = BitHelper.ToUInt32(bytes, 0, bytes.Length, false);
                        samples.Enqueue(sample);
                    }
                }
            }
        }

        public void LoadSample(uint sample)
        {
            var croppedSample = BitHelper.GetMaskedValue(sample, 0, (int)sampleWidthBits);
            if(croppedSample != sample)
            {
                loggingParent?.Log(LogLevel.Warning, "Cropping the sample from 0x{0:X} to 0x{1:X} to fit to the sample width ({2} bits)", sample, croppedSample, sampleWidthBits);
            }

            lock(samples)
            {
                samples.Enqueue(croppedSample);
            }
        }

        public uint GetSingleSample()
        {
            return samples.TryDequeue(out var sample) ? sample : 0u;
        }

        public IEnumerable<uint> GetSamplesByCount(uint samplesCountPerChannel)
        {
            lock(samples)
            {
                return samples.DequeueRange((int)(samplesCountPerChannel * numberOfChannels));
            }
        }

        public IEnumerable<uint> GetSamplesByTime(uint ms)
        {
            lock(samples)
            {
                var samplesPerChannel = ms * (samplingRateHz / 1000);
                return GetSamplesByCount(samplesPerChannel);
            }
        }

        private readonly uint samplingRateHz;
        private readonly uint numberOfChannels;
        private readonly uint sampleWidthBits;

        private readonly IPeripheral loggingParent;
        private readonly Queue<uint> samples;
    }
}
