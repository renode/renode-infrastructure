//
// Copyright (c) 2010-2018 Antmicro
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
        public Button(bool invert = false)
        {
            Inverted = invert;
            IRQ = new GPIO();

            Reset();
        }

        public void Reset()
        {
            // We call Press here to refresh states after reset.
            Press();
            Release();
        }

        public void PressAndRelease()
        {
            Press();
            Release();
        }

        public void Press()
        {
            IRQ.Set(!Inverted);
            Pressed = true;
            OnStateChange(true);
        }

        public void Release()
        {
            IRQ.Set(Inverted);
            Pressed = false;
            OnStateChange(false);
        }

        public void Toggle()
        {
            if(Pressed)
            {
                Release();
            }
            else
            {
                Press();
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

        public bool Inverted { get; private set; }
    }
}

