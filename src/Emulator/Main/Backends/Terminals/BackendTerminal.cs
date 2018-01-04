//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Backends.Terminals
{
    public abstract class BackendTerminal : IExternal, IConnectable<IUART>
    {
        public virtual event Action<byte> CharReceived;

        public abstract void WriteChar(byte value);

        public virtual void AttachTo(IUART uart)
        {
            CharReceived += uart.WriteChar;
            uart.CharReceived += WriteChar;
        }
        public virtual void DetachFrom(IUART uart)
        {
            CharReceived -= uart.WriteChar;
            uart.CharReceived -= WriteChar;
        }

        protected void CallCharReceived(byte value)
        {
            var charReceived = CharReceived;
            if(charReceived != null)
            {
                charReceived(value);
            }
        }
    }
}

