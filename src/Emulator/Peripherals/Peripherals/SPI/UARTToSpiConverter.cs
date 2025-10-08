//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class UARTToSpiConverter : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IUART
    {
        public UARTToSpiConverter(IMachine machine) : base(machine)
        {
        }

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

        [field: Transient]
        public event Action<byte> CharReceived;
    }
}