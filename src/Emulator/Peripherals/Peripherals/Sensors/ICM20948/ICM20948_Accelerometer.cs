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
        public void FeedAccelerationSamplesFromRESD(string path, uint channel = 0, ulong startTime = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            accelerometerResdStream = this.CreateRESDStream<AccelerationSample>(path, channel, sampleOffsetType, sampleOffsetTime);
            accelerometerFeederThread?.Stop();
            accelerometerFeederThread = accelerometerResdStream.StartSampleFeedThread(this,
                (uint)AccelerometerOutputDataRateHz,
                startTime: startTime
            );
        }

        public decimal DefaultAccelerationX
        {
            get => defaultAccelerationX;

            set
            {
                if(!IsAccelerationInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {AccelerometerFullScaleRangeG}[g]");
                }
                defaultAccelerationX = value;
            }
        }

        public decimal DefaultAccelerationY
        {
            get => defaultAccelerationY;

            set
            {
                if(!IsAccelerationInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {AccelerometerFullScaleRangeG}[g]");
                }
                defaultAccelerationY = value;
            }
        }

        public decimal DefaultAccelerationZ
        {
            get => defaultAccelerationZ;

            set
            {
                if(!IsAccelerationInRange(value))
                {
                    throw new RecoverableException($"Value out of currently set range. Maximum value is {AccelerometerFullScaleRangeG}[g]");
                }
                defaultAccelerationZ = value;
            }
        }

        public decimal AccelerationX => accelerationX ?? DefaultAccelerationX;
        public decimal AccelerationY => accelerationY ?? DefaultAccelerationY;
        public decimal AccelerationZ => accelerationZ ?? DefaultAccelerationZ;

        [OnRESDSample(SampleType.Acceleration)]
        [BeforeRESDSample(SampleType.Acceleration)]
        private void HandleAccelerationSample(AccelerationSample sample, TimeInterval timestamp)
        {
            if(sample != null)
            {
                accelerationX = (decimal)sample.AccelerationX / 1e6m;
                accelerationY = (decimal)sample.AccelerationY / 1e6m;
                accelerationZ = (decimal)sample.AccelerationZ / 1e6m;
            }
            else
            {
                accelerationX = null;
                accelerationY = null;
                accelerationZ = null;
            }
        }

        [AfterRESDSample(SampleType.Acceleration)]
        private void HandleAccelerationSampleEnded(AccelerationSample sample, TimeInterval timestamp)
        {
            HandleAccelerationSample(sample, timestamp);
            accelerometerFeederThread?.Stop();
            accelerometerFeederThread = null;
        }

        private bool IsAccelerationInRange(decimal acc)
        {
            return Math.Abs(acc) <= AccelerometerFullScaleRangeG;
        }

        private ushort RawAccelerationX => ConvertMeasurement(AccelerationX, value => value * AccelerometerSensitivityScaleFactor);
        private ushort RawAccelerationY => ConvertMeasurement(AccelerationY, value => value * AccelerometerSensitivityScaleFactor);
        private ushort RawAccelerationZ => ConvertMeasurement(AccelerationZ, value => value * AccelerometerSensitivityScaleFactor);

        private RESDStream<AccelerationSample> accelerometerResdStream;
        private IManagedThread accelerometerFeederThread;

        private decimal? accelerationX = null;
        private decimal? accelerationY = null;
        private decimal? accelerationZ = null;
        private decimal defaultAccelerationX;
        private decimal defaultAccelerationY;
        private decimal defaultAccelerationZ;

        private const decimal AccelerometerMaxOutputDataRateHz = 4500;
        private const decimal AccelerometerWakeOnMotionThresholdStepSizeG = 0.004m;

        private decimal AccelerometerWakeOnMotionThreshold => accelerometerWakeOnMotionThreshold.Value * AccelerometerWakeOnMotionThresholdStepSizeG;
        private decimal AccelerometerSampleRateDivider => accelerometerSampleRateDividerHigh.Value << 8 | accelerometerSampleRateDividerLow.Value;

        private decimal AccelerometerOutputDataRateHz
        {
            get
            {
                if(accelerometerFilterChoice.Value)
                {
                    return InternalSampleRateHz / (1 + AccelerometerSampleRateDivider);
                }
                return AccelerometerMaxOutputDataRateHz;
            }
        }

        private decimal AccelerometerFullScaleRangeG
        {
            get
            {
                switch(accelerometerFullScaleRange.Value)
                {
                    case AccelerationFullScaleRangeSelection.Mode0_2G:
                        return 2m;
                    case AccelerationFullScaleRangeSelection.Mode1_4G:
                        return 4m;
                    case AccelerationFullScaleRangeSelection.Mode2_8G:
                        return 8m;
                    case AccelerationFullScaleRangeSelection.Mode3_16G:
                        return 16m;
                    default:
                        throw new Exception("Wrong accelerometer full scale range selection");
                }
            }
        }

        private decimal AccelerometerSensitivityScaleFactor
        {
            get
            {
                switch(accelerometerFullScaleRange.Value)
                {
                    case AccelerationFullScaleRangeSelection.Mode0_2G:
                        return 16384m;
                    case AccelerationFullScaleRangeSelection.Mode1_4G:
                        return 8192m;
                    case AccelerationFullScaleRangeSelection.Mode2_8G:
                        return 4096m;
                    case AccelerationFullScaleRangeSelection.Mode3_16G:
                        return 2048m;
                    default:
                        throw new Exception("Wrong accelerometer full scale range selection");
                }
            }
        }

        private int AccelerometerAveragedSamples
        {
            get
            {
                switch(accelerometerDecimatorConfig.Value)
                {
                    case AccelerometerDecimator.Mode0_4Samples:
                        if(!accelerometerFilterChoice.Value)
                        {
                            return 1;
                        }
                        return 4;
                    case AccelerometerDecimator.Mode1_8Samples:
                        return 8;
                    case AccelerometerDecimator.Mode2_16Samples:
                        return 16;
                    case AccelerometerDecimator.Mode3_32Samples:
                        return 32;
                    default:
                        throw new Exception("Wrong accelerometer decimator config selection");
                }
            }
        }

        private enum AccelerationFullScaleRangeSelection : byte
        {
            Mode0_2G = 0,
            Mode1_4G = 1,
            Mode2_8G = 2,
            Mode3_16G = 3
        }

        private enum AccelerometerDecimator
        {
            Mode0_4Samples = 0,
            Mode1_8Samples = 1,
            Mode2_16Samples = 2,
            Mode3_32Samples = 3
        }

        private enum WakeOnMotionCompareAlgorithm
        {
            InitialSample = 0,
            PreviousSample = 1
        }
    }
}
