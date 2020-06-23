//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;
using System.IO;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class PCMDecoder
    {
        public PCMDecoder(uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels, bool concatenatedChannels) 
        {
            if(concatenatedChannels)
            {
               throw new ConstructionException("Concatenated channels are currently not supported"); 
            }

            if(sampleWidthBits != 8u && sampleWidthBits != 16u && sampleWidthBits != 24u && sampleWidthBits != 32u)
            {
               throw new ConstructionException($"Not supported sample width: {0}. Only 8/16/24/32 values are currently supported."); 
            }

            samples = new Queue<uint>();

            this.sampleWidthBits = sampleWidthBits;
            this.numberOfChannels = numberOfChannels;
            this.samplingRateHz = samplingRateHz;
        }

        public void LoadFile(string path)
        {
            var sampleSize = (int)(sampleWidthBits/ 8);

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
            lock(samples)
            {
                samples.Enqueue(sample);
            }
        }

        public IEnumerable<uint> GetSamplesCount(uint samplesCountPerChannel)
        {
            lock(samples)
            {
                return samples.DequeueRange((int)(samplesCountPerChannel * numberOfChannels));
            }
        }

        public IEnumerable<uint> GetSamplesTime(uint ms)
        {
            lock(samples)
            {
                var samplesPerChannel = ms * (samplingRateHz / 1000);
                return GetSamplesCount(samplesPerChannel);
            }
        }

        private readonly uint samplingRateHz;
        private readonly uint numberOfChannels;
        private readonly uint sampleWidthBits;

        private readonly Queue<uint> samples;
    }
}
