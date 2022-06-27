//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
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
        
        public bool WaitForStepCommand()
        {
            lock(Guard)
            {
                while(enabled && counter == 0)
                {
                    Monitor.Wait(Guard);
                }   
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
                        Monitor.PulseAll(Guard);
                    }
                }
            }
        }   
        
        private bool enabled;
        private int counter;
    }
}

