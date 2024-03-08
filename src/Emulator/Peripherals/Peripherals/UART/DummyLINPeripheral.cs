//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.LIN
{
    public class DummyLINPeripheral : LINBase
    {
        public DummyLINPeripheral(IMachine machine, byte protectedId, int frameLength = 2) : base(machine)
        {
            linDecoder.ValidateProtectedIdentifier = true;
            try
            {
                linEntry = RegisterProtectedIdentifier(protectedId, LINMode.Sink, frameLength);
            }
            catch(ArgumentException)
            {
                throw new ConstructionException($"{protectedId} is not valid protected identifier");
            }

            ProtectedIdentifier = protectedId;
            internalData = new Queue<byte>();
        }

        public void EnqueueByte(byte value)
        {
            internalData.Enqueue(value);
        }

        public void ClearTransmittingData()
        {
            internalData.Clear();
        }

        public override void FrameReceived(byte privateIdentifier, byte[] data, bool valid)
        {
            this.Log(LogLevel.Info, "Received frame (valid={0}): {1}", valid, Misc.PrettyPrintCollectionHex(data));
        }

        public override void StartedTransmission(byte privateIdentifier)
        {
            linDecoder.TransmitAndFinalize(internalData.ToArray(), TransmitCharacter);
        }

        public byte ProtectedIdentifier { get; }

        public LINMode Mode
        {
            get => linEntry.Mode;
            set => linEntry.Mode = value;
        }

        public bool ExtendedFrameChecksum
        {
            get => linEntry.ExtendedChecksum;
            set => linEntry.ExtendedChecksum = value;
        }

        public bool ValidateFrame
        {
            get => linEntry.ValidateFrame;
            set => linEntry.ValidateFrame = value;
        }

        public int FrameLength
        {
            get => linEntry.FrameLength;
            set => linEntry.FrameLength = value;
        }

        public override Bits StopBits => Bits.None;
        public override Parity ParityBit => Parity.None;
        public override uint BaudRate => 19200;

        protected override void CharWritten()
        {
        }

        protected override void QueueEmptied()
        {
        }

        private readonly Queue<byte> internalData;
        private readonly ILINEntry linEntry;
    }
}
