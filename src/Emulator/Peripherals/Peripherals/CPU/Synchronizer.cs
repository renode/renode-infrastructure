//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Threading;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class Synchronizer
    {
        public Synchronizer()
        {
            Guard = new object();
        }

        public void StepFinished()
        {
            lock(Guard)
            {
                if(counter > 0)
                {
                    counter--;
                }
                if(counter == 0)
                {
                    Monitor.Pulse(Guard);
                }
            }
        }

        public void CommandStep(int steps = 1)
        {
            lock(Guard)
            {
                counter = steps;
                Monitor.Pulse(Guard);
            }
        }

        public bool WaitForStepCommand(out StepResult result)
        {
            lock(Guard)
            {
                while(enabled && !stepDelayed && counter == 0)
                {
                    Monitor.Wait(Guard);
                }

                if(stepDelayed)
                {
                    stepDelayed = false;
                    result = StepResult.Delayed;
                    return enabled;
                }

                result = enabled ? StepResult.Granted : StepResult.Disabled;
                return enabled;
            }
        }

        public void WaitForStepFinished()
        {
            lock(Guard)
            {
                while(counter > 0)
                {
                    Monitor.Wait(Guard);
                }
            }
        }

        public void StepInterrupted()
        {
            lock(Guard)
            {
                counter = 0;
                Monitor.Pulse(Guard);
            }
        }

        public void DelayStepCommand()
        {
            lock(Guard)
            {
                stepDelayed = true;
                Monitor.Pulse(Guard);
            }
        }

        public object Guard { get; }

        public bool Enabled
        {
            get
            {
                return enabled;
            }

            set
            {
                lock(Guard)
                {
                    enabled = value;
                    if(!enabled)
                    {
                        stepDelayed = false;
                        Monitor.PulseAll(Guard);
                    }
                }
            }
        }

        private bool enabled;
        private bool stepDelayed;
        private int counter;

        public enum StepResult
        {
            Granted,
            Delayed,
            Disabled,
        }
    }
}