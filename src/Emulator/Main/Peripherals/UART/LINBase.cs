//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public abstract class LINBase : UARTBase, ILINDevice
    {
        public LINBase(IMachine machine) : base(machine)
        {
            linDecoder = new LINDecoder();
            linDecoder.FrameReceived += FrameReceived;
        }

        public virtual void ReceiveLINBreak()
        {
            linDecoder.Break();
        }

        public override void WriteChar(byte value)
        {
            linDecoder.Feed(value);
            if(linDecoder.CurrentState != LINDecoder.State.Ignoring)
            {
                base.WriteChar(value);
            }
        }

        public abstract void FrameReceived(byte protectedIdentifier, byte[] data, bool valid);

        public abstract void StartedTransmission(byte protectedIdentifier);

        protected ILINEntry RegisterProtectedIdentifier(byte pid, LINMode mode, int frameLength)
        {
            var entry = linDecoder.Register(pid);
            entry.Mode = mode;
            entry.FrameLength = frameLength;

            entry.StartedTransmission += () => StartedTransmission(pid);

            return entry;
        }

        protected readonly LINDecoder linDecoder;
    }
}