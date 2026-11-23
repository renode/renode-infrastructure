
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities
{
    public class PWMCounter
    {
        private bool hasPreviousRisingEdge;
        private bool hasPulseWidth;

        private TimeStamp risingEdgeTime;
        private TimeStamp previousRisingEdgeTime;

        private IEmulationElement logTarget;

        // Constructor configuration
        public double MinPulseWidthUs { get; }
        public double MaxPulseWidthUs { get; }

        public double ExpectedFrequencyHz { get; }
        public double FrequencyTolerancePercent { get; }

        // Measured values
        public double PulseWidthUs { get; private set; }
        public double FrequencyHz { get; private set; }
        public double PeriodUs
        {
            get
            {
                return 1000000.0f / FrequencyHz;
            }
        }
        public double DutyCycle
        {
            get
            {
                return PulseWidthUs / PeriodUs;
            }
        }

        public bool ValidSignal { get; private set; }

        public PWMCounter(
            IEmulationElement logTarget,
            double expectedFrequencyHz,
            double frequencyTolerancePercent = 5.0)
        {
            if(expectedFrequencyHz <= 0)
            {
                throw new ArgumentException(
                    "Expected frequency must be positive.");
            }

            if(frequencyTolerancePercent < 0 ||
               frequencyTolerancePercent >= 100)
            {
                throw new ArgumentException(
                    "Frequency tolerance must be between 0 and 100 percent.");
            }

            ExpectedFrequencyHz = expectedFrequencyHz;
            FrequencyTolerancePercent = frequencyTolerancePercent;

            this.logTarget = logTarget;
        }

        public void RisingEdge(TimeStamp virtualTime)
        {
            if(hasPreviousRisingEdge && hasPulseWidth)
            {
                var period =
                    virtualTime.TimeElapsed -
                    previousRisingEdgeTime.TimeElapsed;

                double periodUs = period.TotalMicroseconds;

                if(periodUs > 0)
                {
                    FrequencyHz = 1000000.0 / periodUs;

                    ValidateSignal();
                }
            }

            risingEdgeTime = virtualTime;
            previousRisingEdgeTime = virtualTime;

            hasPreviousRisingEdge = true;
            hasPulseWidth = false;
        }

        public void FallingEdge(TimeStamp virtualTime)
        {
            if(!hasPreviousRisingEdge)
            {
                return;
            }

            var elapsed =
                virtualTime.TimeElapsed -
                risingEdgeTime.TimeElapsed;

            PulseWidthUs = elapsed.TotalMicroseconds;
            hasPulseWidth = true;
        }

        private void ValidateSignal()
        {
            bool validFrequency =
                Math.Abs(FrequencyHz - ExpectedFrequencyHz)
                <= ExpectedFrequencyHz *
                   FrequencyTolerancePercent / 100.0;

            if(!validFrequency)
            {
                ValidSignal = false;

               logTarget.Log(LogLevel.Error,
                    "PWM frequency error! Expected: {0:F2} Hz, Actual: {1:F2} Hz (Tolerance: {2:F1}%)",
                    ExpectedFrequencyHz,
                    FrequencyHz,
                    FrequencyTolerancePercent);

                return;
            }

            ValidSignal = true;
        }

        public void Reset()
        {
            hasPreviousRisingEdge = false;
            hasPulseWidth = false;

            risingEdgeTime = default(TimeStamp);
            previousRisingEdgeTime = default(TimeStamp);

            PulseWidthUs = 0;
            FrequencyHz = 0;

            ValidSignal = false;
        }
    }
}
