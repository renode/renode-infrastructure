//
// Copyright (c) 2010-2022 Antmicro
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
        public UARTToSpiConverter(IMachine machine) : base(machine)
        {
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public override void Reset()
        {
        }

        public void WriteChar(byte value)
        {
            if(RegisteredPeripheral == null)
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
                // StopBits are always None
                return Bits.None;
            }
        }

        public Parity ParityBit
        {
            get
            {
                // Parity is always None
                return Parity.None;
            }
        }

        public uint BaudRate
        {
            get
            {
                // BaudRate is always 0
                return 0;
            }
        }
    }
}

