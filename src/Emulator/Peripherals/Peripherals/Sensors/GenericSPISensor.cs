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
    public class GenericSPISensor : ISPIPeripheral
    {
        public GenericSPISensor()
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
            samplesFifo.TryDequeueNewSample();
            var result = (byte)samplesFifo.Sample.Value;

            this.Log(LogLevel.Info, "Received byte 0x{0:X}, returning 0x{1:X}", b, result);
            return result;
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
    }
}

