//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948 : ITemperatureSensor
    {
        public void FeedTemperatureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            resdTemperatureStream?.Dispose();
            resdTemperatureStream = this.CreateRESDStream<TemperatureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        }

        public decimal DefaultTemperature { get; set; }

        public decimal Temperature
        {
            get
            {
                if(resdTemperatureStream == null)
                {
                    return DefaultTemperature;
                }

                switch(resdTemperatureStream.TryGetCurrentSample(this, out var sample, out _))
                {
                case RESDStreamStatus.OK:
                    break;
                case RESDStreamStatus.BeforeStream:
                    return DefaultTemperature;
                case RESDStreamStatus.AfterStream:
                    resdTemperatureStream.Dispose();
                    resdTemperatureStream = null;
                    break;
                default:
                    return DefaultTemperature;
                }
                return sample.Temperature / 1000m;
            }

            set => throw new RecoverableException($"Explicitly setting temperature is not supported by this model. " +
                $"Temperature should be provided from a RESD file or set via the '{nameof(DefaultTemperature)}' property");
        }

        private ushort RawTemperature => ConvertMeasurement(Temperature, value => ((value - RoomTemperatureDegreeCelsius) * TemperatureSensitivity) + RoomTemperatureOffsetDegreeCelsius);

        private RESDStream<TemperatureSample> resdTemperatureStream;

        private const decimal RoomTemperatureDegreeCelsius = 21.0m;
        private const decimal RoomTemperatureOffsetDegreeCelsius = 0.0m;
        private const decimal TemperatureSensitivity = 333.87m;
    }
}
