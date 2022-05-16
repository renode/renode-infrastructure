//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ResetPin : IGPIOReceiver
    {
        public ResetPin(Machine machine, bool invert = true)
        {
            inverted = invert;
            this.machine = machine;
            state = false;
            sync = new object();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                this.Log(LogLevel.Error, "Tried to set pin {0} to value {1}, but only pin 0 is supported in this model", number, value);
                return;
            }

            State = inverted ? !value : value;
        }

        public void Reset()
        {
            state = false;
        }

        public bool State
        {
            get => state;

            private set
            {
                lock(sync)
                {
                    if(value == state)
                    {
                        return;
                    }

                    state = value;

                    if(state)
                    {
                        machine.RequestReset();
                    }
                }
            }
        }

        private bool state;

        private readonly bool inverted;
        private readonly Machine machine;
        private readonly object sync;
    }
}
