//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LIS2DW12_SPI : ISPIPeripheral
    {
        public LIS2DW12_SPI()
        {
            samplesFifo = new SensorSamplesFifo<ScalarSample>();
        }

        public void Reset()
        {
            // intentionally do nothing
        }

        public void FinishTransmission()
        {
            // intentionally do nothing
        }

        public byte Transmit(byte b)
        {
            //samplesFifo.TryDequeueNewSample();
            //var r = (byte)samplesFifo.Sample.Value;
            var r = (byte)0x0;
            switch(state) {
                case 0x8F:
                    r = (byte) 0x44;
                    break;
                case 0x8F:
                    r = (byte) 0x44;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Case not handled..");
                    break;
            }
            state=(int)b;
            
            this.Log(LogLevel.Info, "Received byte 0x{0:X}, returning 0x{1:X}", b, r);
            return r;
        }

        public void FeedSample(byte sample)
        {
            samplesFifo.FeedSample(new ScalarSample(sample));
        }

        public void FeedSamplesFromFile(string path)
        {
            samplesFifo.FeedSamplesFromFile(path);
        }

        private readonly SensorSamplesFifo<ScalarSample> samplesFifo;

        private int state;
    }
}




