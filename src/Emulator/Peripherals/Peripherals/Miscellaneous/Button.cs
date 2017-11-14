//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using System;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class Button : IPeripheral, IGPIOSender
    {
        public Button()
        {
            IRQ = new GPIO();
        }

        public void PressAndRelease()
        {
            IRQ.Set();
            IRQ.Unset();
            OnStateChange(true);
            OnStateChange(false);
        }

        public void Toggle()
        {
            if (Pressed)
            {
                IRQ.Unset();
                Pressed = false;
                OnStateChange(false);
            }
            else
            {
                IRQ.Set();
                Pressed = true;
                OnStateChange(true);
            }
        }

        private void OnStateChange(bool pressed)
        {
            var sc = StateChanged;
            if (sc != null)
            {
                sc(pressed);
            }
        }

        public GPIO IRQ { get; private set; }

        public event Action<bool> StateChanged;

        public bool Pressed { get; private set; }

        #region IPeripheral implementation

        public void Reset()
        {
            // despite apperances, nothing
        }

        #endregion
    }
}

