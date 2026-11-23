
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RGBLed : IPeripheral, IGPIOReceiver
    {
        private readonly IMachine machine;

        private bool[] lastState = new bool[3];
        private PWMCounter[] rgb = new PWMCounter[3];

        public double Red
        {
            get
            {
                return rgb[0].DutyCycle;
            }
        }

        public double Green
        {
            get
            {
                return rgb[1].DutyCycle;
            }
        }

        public double Blue
        {
            get
            {
                return rgb[2].DutyCycle;
            }
        }

        public RGBLed(
            IMachine machine,
            double expectedFrequencyHz,
            double frequencyTolerancePercent = 5.0)
        {
            this.machine = machine;

            for (int i = 0; i < 3; i ++)
            {
                rgb[i] = new PWMCounter(this, expectedFrequencyHz, frequencyTolerancePercent);
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if (number < 0 || number > 2)
            {
                this.Log(LogLevel.Warning, $"Received GPIO id {number} outside of range 0-2.");
                return;
            }

            if(value && !lastState[number])
            {
                rgb[number].RisingEdge(machine.ElapsedVirtualTime);
            }
            else if(!value && lastState[number])
            {
                rgb[number].FallingEdge(machine.ElapsedVirtualTime);
            }

            lastState[number] = value;
        }

        public void Reset()
        {
            for (int i = 0; i < 3; i ++)
            {
                rgb[i].Reset();
            }   
        }
    }
}
