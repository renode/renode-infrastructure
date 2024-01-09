//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.CAN
{
    public class CANToUART : UARTBase, ICAN
    {
        public CANToUART(IMachine machine, uint rxFromCANId, uint txToCANId) : base(machine)
        {
            this.rxFromCANId = rxFromCANId;
            this.txToCANId = txToCANId;
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            if(message.Id == rxFromCANId)
            {
                this.NoisyLog("Received {0} bytes [{1}] on id 0x{2:X}", message.Data.Length, message.DataAsHex, message.Id);
                foreach(var character in message.Data)
                {
                    TransmitCharacter(character);
                }
            }
            else
            {
                this.DebugLog("Received an unexpected message on id 0x{0:X} and length {1}!", message.Id, message.Data.Length);
            }
        }

        public override void WriteChar(byte value)
        {
            var fs = FrameSent;
            if(fs == null)
            {
                return;
            }
            this.NoisyLog("Transmitting byte 0x{0:X2}", value);
            fs(new CANMessageFrame(txToCANId, new byte[] { value }));
        }

        public event Action<CANMessageFrame> FrameSent;

        public override Bits StopBits => Bits.One;
        public override Parity ParityBit => Parity.None;
        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // Intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // Intentionally left blank
        }

        private readonly uint rxFromCANId;
        private readonly uint txToCANId;
    }
}
