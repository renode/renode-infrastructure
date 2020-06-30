//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LED : IGPIOReceiver, ILed
    {
        public LED(bool invert = false)
        {
            inverted = invert;
            State = invert;
            sync = new object();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var stateChanged = StateChanged;
            lock(sync)
            {
                if(stateChanged != null)
                {
                    stateChanged(this, inverted ? !value : value);
                }
                State = inverted ? !value : value;
                this.Log(LogLevel.Noisy, "LED state changed - {0}", inverted ? !value : value);
            }
        }

        public void Reset()
        {
            State = inverted;
        }

        [field: Transient]
        public event Action<ILed, bool> StateChanged;

        public bool State { get; private set; }

        private readonly bool inverted;
        private readonly object sync;
    }
}

