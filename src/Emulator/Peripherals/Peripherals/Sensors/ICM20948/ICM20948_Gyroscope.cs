//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.RESD;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948 : IUnderstandRESD
    {
        public void FeedAngularRateSamplesFromRESD(string path, uint channel = 0, ulong startTime = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            gyroResdStream = this.CreateRESDStream<AngularRateSample>(path, channel, sampleOffsetType, sampleOffsetTime);
            gyroFeederThread?.Stop();
            gyroFeederThread = gyroResdStream.StartSampleFeedThread(this,
                (uint)GyroOutputDataRateHz,
                startTime: startTime
            );
        }

        public decimal DefaultAngularRateX
        {
            get => defaultAngularRateX;

            set
            {
                if(!IsAngularRateInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {GyroFullScaleRangeDPS}[g]");
                }
                defaultAngularRateX = value;
            }
        }

        public decimal DefaultAngularRateY
        {
            get => defaultAngularRateY;

            set
            {
                if(!IsAngularRateInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {GyroFullScaleRangeDPS}[g]");
                }
                defaultAngularRateY = value;
            }
        }

        public decimal DefaultAngularRateZ
        {
            get => defaultAngularRateZ;

            set
            {
                if(!IsAngularRateInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {GyroFullScaleRangeDPS}[g]");
                }
                defaultAngularRateZ = value;
            }
        }

        public decimal AngularRateX => angularRateX ?? DefaultAngularRateX;
        public decimal AngularRateY => angularRateY ?? DefaultAngularRateY;
        public decimal AngularRateZ => angularRateZ ?? DefaultAngularRateZ;

        [OnRESDSample(SampleType.AngularRate)]
        [BeforeRESDSample(SampleType.AngularRate)]
        private void HandleAngularRateSample(AngularRateSample sample, TimeInterval timestamp)
        {
            if(sample != null)
            {
                angularRateX = RadiansToDegrees * (decimal)sample.AngularRateX / 1e5m;
                angularRateY = RadiansToDegrees * (decimal)sample.AngularRateY / 1e5m;
                angularRateZ = RadiansToDegrees * (decimal)sample.AngularRateZ / 1e5m;
            }
            else
            {
                angularRateX = null;
                angularRateY = null;
                angularRateZ = null;
            }
        }

        [AfterRESDSample(SampleType.AngularRate)]
        private void HandleAngularRateSampleEnded(AngularRateSample sample, TimeInterval timestamp)
        {
            HandleAngularRateSample(sample, timestamp);
            gyroFeederThread.Stop();
            gyroFeederThread = null;
        }

        private bool IsAngularRateInRange(decimal value)
        {
            return Math.Abs(value) <= GyroFullScaleRangeDPS;
        }

        private ushort RawAngularRateX => ConvertMeasurement(AngularRateX, value => value * GyroSensitivityScaleFactor);
        private ushort RawAngularRateY => ConvertMeasurement(AngularRateY, value => value * GyroSensitivityScaleFactor);
        private ushort RawAngularRateZ => ConvertMeasurement(AngularRateZ, value => value * GyroSensitivityScaleFactor);

        private decimal? angularRateX;
        private decimal? angularRateY;
        private decimal? angularRateZ;
        private decimal defaultAngularRateX;
        private decimal defaultAngularRateY;
        private decimal defaultAngularRateZ;

        private RESDStream<AngularRateSample> gyroResdStream;
        private IManagedThread gyroFeederThread;

        private const decimal RadiansToDegrees = 180m / (decimal)Math.PI;
        private const decimal GyroMaxOutputDataRateHz = 9000;
        private const decimal GyroOffsetCancellationStepSizeDPS = 0.0305m;

        private decimal GyroOutputDataRateHz
        {
            get
            {
                if(gyroFilterChoice.Value)
                {
                    return InternalSampleRateHz / (1 + gyroSampleRateDivider.Value);
                }
                return GyroMaxOutputDataRateHz;
            }
        }

        private decimal GyroFullScaleRangeDPS
        {
            get
            {
                switch(gyroFullScaleRange.Value)
                {
                    case GyroFullScaleRangeSelection.Mode0_250DPS:
                        return 250;
                    case GyroFullScaleRangeSelection.Mode1_500DPS:
                        return 500;
                    case GyroFullScaleRangeSelection.Mode2_1000DPS:
                        return 1000;
                    case GyroFullScaleRangeSelection.Mode3_2000DPS:
                        return 2000;
                    default:
                        throw new Exception("Wrong gyroscope full scale range selection");
                }
            }
        }

        private decimal GyroSensitivityScaleFactor
        {
            get
            {
                switch(gyroFullScaleRange.Value)
                {
                    case GyroFullScaleRangeSelection.Mode0_250DPS:
                        return 131m;
                    case GyroFullScaleRangeSelection.Mode1_500DPS:
                        return 65.5m;
                    case GyroFullScaleRangeSelection.Mode2_1000DPS:
                        return 32.8m;
                    case GyroFullScaleRangeSelection.Mode3_2000DPS:
                        return 16.4m;
                    default:
                        throw new Exception("Wrong gyroscope full scale range selection");
                }
            }
        }

        private int GyroAveragedSamples => (int)Math.Pow(2, gyroAveragingFilterExponent.Value);

        private enum GyroFullScaleRangeSelection : byte
        {
            Mode0_250DPS = 0,
            Mode1_500DPS = 1,
            Mode2_1000DPS = 2,
            Mode3_2000DPS = 3
        }
    }
}
