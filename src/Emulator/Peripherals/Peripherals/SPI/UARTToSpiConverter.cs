//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class UARTToSpiConverter : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IUART
    {
        public UARTToSpiConverter(Machine machine) : base(machine)
        {
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public override void Reset()
        {
        }

        public void WriteChar(byte value)
        {
            if(RegisteredPeripheral != null)
            {
                return;
            }
            var charReceived = CharReceived;
            var read = RegisteredPeripheral.Transmit(value);
            charReceived(read);
        }

        public Bits StopBits
        {
            get
            {
                throw new ArgumentException();
            }
        }

        public Parity ParityBit
        {
            get
            {
                throw new ArgumentException();
            }
        }

        public uint BaudRate
        {
            get
            {
                throw new ArgumentException();
            }
        }
    }
}

