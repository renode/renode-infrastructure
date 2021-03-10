//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Backends.Terminals
{
    public abstract class BackendTerminal : IExternal, IConnectable<IUART>
    {
        public virtual event Action<byte> CharReceived;

        public abstract void WriteChar(byte value);

        public virtual void AttachTo(IUART uart)
        {
            this.uart = uart;
            this.machine = uart.GetMachine();
            
            CharReceived += WriteToUART;
            uart.CharReceived += WriteChar;
        }

        public virtual void DetachFrom(IUART uart)
        {
            CharReceived -= WriteToUART;
            uart.CharReceived -= WriteChar;

            this.uart = null;
            this.machine = null;
        }

        protected void CallCharReceived(byte value)
        {
            var charReceived = CharReceived;
            if(charReceived != null)
            {
                charReceived(value);
            }
        }

        private void WriteToUART(byte value)
        {
            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }

            machine.HandleTimeDomainEvent(uart.WriteChar, value, vts);
        }

        private IUART uart;
        private Machine machine;
    }
}

