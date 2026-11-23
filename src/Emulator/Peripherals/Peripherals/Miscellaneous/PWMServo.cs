
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class Servo : IPeripheral, IGPIOReceiver
    {
        private readonly IMachine machine;

        private bool lastState;

        private PWMCounter pwmCounter;

        private double MinPulseWidthUs;
        private double MaxPulseWidthUs;

        public double MinAngle { get; }
        public double MaxAngle { get; }
        public bool Error { get; private set; }

        public double Angle
        {
            get
            {
                if (pwmCounter.PulseWidthUs < MinPulseWidthUs)
                {
                    this.Log(LogLevel.Warning, $"Servo received pulse width {pwmCounter.PulseWidthUs:2F} below range {MinPulseWidthUs:2F} - {MaxPulseWidthUs:2F}. Clamping output.");
                    Error = true;

                    return MinAngle;
                } else if (pwmCounter.PulseWidthUs > MaxPulseWidthUs)
                {
                    this.Log(LogLevel.Warning, $"Servo received pulse width {pwmCounter.PulseWidthUs:2F} above range {MinPulseWidthUs:2F} - {MaxPulseWidthUs:2F}. Clamping output.");
                    Error = true;

                    return MaxAngle;
                } else {
                    return (pwmCounter.PulseWidthUs - MinPulseWidthUs) / (MaxPulseWidthUs - MinPulseWidthUs) * (MaxAngle - MinAngle) + MinAngle;
                }
            }
        }

        public Servo(
            IMachine machine,
            double minPulseWidthUs,
            double maxPulseWidthUs,
            double minAngle,
            double maxAngle,
            double expectedFrequencyHz,
            double frequencyTolerancePercent = 5.0)
        {
            if(minPulseWidthUs < 0 ||
               maxPulseWidthUs <= minPulseWidthUs)
            {
                throw new ArgumentException(
                    "Invalid pulse width range.");
            }

            if(minAngle >= maxAngle)
            {
                throw new ArgumentException(
                    "Invalid angle range.");
            }

            this.machine = machine;

            MinAngle = minAngle;
            MaxAngle = maxAngle;
            MinPulseWidthUs = minPulseWidthUs;
            MaxPulseWidthUs = maxPulseWidthUs;

            pwmCounter = new PWMCounter(this, expectedFrequencyHz, frequencyTolerancePercent);
        }

        public void OnGPIO(int number, bool value)
        {
            if(value && !lastState)
            {
                pwmCounter.RisingEdge(machine.ElapsedVirtualTime);
            }
            else if(!value && lastState)
            {
                pwmCounter.FallingEdge(machine.ElapsedVirtualTime);
            }

            lastState = value;
        }

        public void Reset()
        {
            Error = false;
            pwmCounter.Reset();            
        }
    }
}
