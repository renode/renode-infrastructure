//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LED : IGPIOReceiver, ILed
    {
        public LED(bool invert = false)
        {
            inverted = invert;
            state = invert;
            sync = new object();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            State = inverted ? !value : value;
        }

        public void Reset()
        {
            state = inverted;
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
                    StateChanged?.Invoke(this, state);
                    this.Log(LogLevel.Noisy, "LED state changed to {0}", state);
                }
            }
        }

        [field: Transient]
        public event Action<ILed, bool> StateChanged;

        private bool state;

        private readonly bool inverted;
        private readonly object sync;
    }
}