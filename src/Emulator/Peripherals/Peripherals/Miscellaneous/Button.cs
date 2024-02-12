//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class Button : IPeripheral, IGPIOSender
    {
        // Registration address ('gpio 3' in the example below) has no influence on the button's logic.
        // It's just a way to inform the peripherals tree ('peripherals' command) how the button is
        // connected to the GPIO controller. The actual connection is done with '-> gpio@3'.
        //
        // button: Miscellaneous.Button @ gpio 3
        //     -> gpio@3
        public Button(bool invert = false)
        {
            ReleaseOnReset = true;
            Inverted = invert;
            IRQ = new GPIO();

            Reset();
        }

        public void Reset()
        {
            if(ReleaseOnReset)
            {
                Press();
                Release();
            }
            else
            {
                // Toggle the button twice to refresh it's state to the same value as before the reset
                Toggle();
                Toggle();
            }
        }

        public void PressAndRelease()
        {
            Press();
            Release();
        }

        public void Press()
        {
            SetGPIO(!Inverted);
            Pressed = true;
            OnStateChange(true);
        }

        public void Release()
        {
            SetGPIO(Inverted);
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

        public GPIO IRQ { get; }

        public event Action<bool> StateChanged;

        public bool Pressed { get; private set; }

        public bool Inverted { get; private set; }

        public bool ReleaseOnReset { get; set; }

        private void OnStateChange(bool pressed)
        {
            var sc = StateChanged;
            if (sc != null)
            {
                sc(pressed);
            }
        }

        private void SetGPIO(bool value)
        {
            if(!this.TryGetMachine(out var machine))
            {
                // can happen during button creation
                IRQ.Set(value);
                return;
            }
            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                // this is almost always the case, but maybe someday we'll be able to press the
                // button by a machine-controlled actuator
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }

            machine.HandleTimeDomainEvent(IRQ.Set, value, vts);
        }
    }
}
