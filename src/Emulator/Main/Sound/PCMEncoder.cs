//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Sound
{
    public class PCMEncoder : IDisposable
    {
        public PCMEncoder(uint sampleWidthBits, uint samplingRateHz, uint numberOfChannels, bool concatenatedChannels)
        {
            if(concatenatedChannels)
            {
               throw new ConstructionException("Concatenated channels are currently not supported");
            }

            if(sampleWidthBits != 8u && sampleWidthBits != 16u && sampleWidthBits != 24u && sampleWidthBits != 32u)
            {
               throw new ConstructionException($"Not supported sample width: {0}. Only 8/16/24/32 values are currently supported.");
            }

            this.samplingRateHz = samplingRateHz;
            this.sampleWidthBits = sampleWidthBits;
            this.numberOfChannels = numberOfChannels;

            buffer = new Queue<byte>();
        }

        public void Dispose()
        {
            // flush and close the output file (if any)
            Output = null;
        }

        public void AcceptSample(uint sample)
        {
            lock(buffer)
            {
                for(var i = 0; i < (sampleWidthBits / 8); i++)
                {
                    buffer.Enqueue((byte)BitHelper.GetValue(sample, (int)(sampleWidthBits - 8 * (i + 1)), 8));
                }
                TryFlushBuffer();
            }
        }

        public void FlushBuffer()
        {
            byte[] data;
            lock(buffer)
            {
                data = buffer.DequeueAll();
            }

            file.Write(data, 0, data.Length);
            file.Flush();
        }

        public void SetBufferingBySamplesCount(uint samplesCount)
        {
            bufferingThreshold = (int)(samplesCount * numberOfChannels * (sampleWidthBits / 8));
            TryFlushBuffer();
        }

        public void SetBufferingByTime(uint milliseconds)
        {
            bufferingThreshold = (int)(milliseconds * (samplingRateHz / 1000) * numberOfChannels * (sampleWidthBits / 8));
            TryFlushBuffer();
        }

        public string Output
        {
            get => outputPath;

            set
            {
                outputPath = value;
                if(value == null)
                {
                    if(file != null)
                    {
                        file.Dispose();
                        file = null;
                    }
                }
                else
                {
                    file = File.OpenWrite(value);
                }
            }
        }

        private void TryFlushBuffer()
        {
            if(buffer.Count >= bufferingThreshold)
            {
                FlushBuffer();
            }
        }

        private int bufferingThreshold;
        private string outputPath;
        private FileStream file;

        private readonly uint samplingRateHz;
        private readonly uint numberOfChannels;
        private readonly uint sampleWidthBits;

        private readonly Queue<byte> buffer;
    }
}
