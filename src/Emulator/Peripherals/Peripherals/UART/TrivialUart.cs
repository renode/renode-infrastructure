//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    /// <summary>
    /// Trivial UART peripheral. Contains a single 32-bit register which can only be written to. Only the lowest 8 bytes matter.
    /// </summary>
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class TrivialUart : IDoubleWordPeripheral, IUART, IKnownSize
    {
        public TrivialUart()
        {
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            this.LogUnhandledRead(offset);
            return 0x0;
        }

        public void Reset()
        {
            // Intentionally left empty.
        }

        public void WriteChar(byte value)
        {
            // Intentionally left empty. This UART provides only a one-way communication.
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.NoisyLog("Received 0x{0:X}", value);
            CharReceived?.Invoke((byte)value);
        }

        public uint BaudRate => 0;
        public Parity ParityBit => Parity.None;
        public long Size => 0x4;
        public Bits StopBits => Bits.None;

        [field: Transient]
        public event Action<byte> CharReceived;
    }
}

