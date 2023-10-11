//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PL031 : IDoubleWordPeripheral, IKnownSize
    {
        public PL031(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0xfe0 && offset < 0x1000)
            {
                return id[(offset - 0xfe0) >> 2];
            }
            switch((Offset)offset)
            {
            case Offset.Data:
                return (uint)(machine.RealTimeClockDateTime - Misc.UnixEpoch).TotalSeconds + tickOffset;
            case Offset.Match:
                return matchRegister;
            case Offset.InterruptMaskSetOrClear:
                return interruptMaskRegister;
            case Offset.RawInterruptStatus:
                return rawInterruptStatusRegister;
            case Offset.Control:
                return 1;
            case Offset.MaskedInterruptStatus:
                return interruptMaskRegister & rawInterruptStatusRegister;
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Offset)offset)
            {
            case Offset.Match:
                matchRegister = value;
                break;
            case Offset.InterruptMaskSetOrClear:
                interruptMaskRegister = value & 0x1;
                if((rawInterruptStatusRegister & interruptMaskRegister) != 0)
                {
                    UpdateInterrupt(true);
                }
                break;
            case Offset.Load:
                tickOffset += value - (uint)(machine.RealTimeClockDateTime - Misc.UnixEpoch).TotalSeconds + tickOffset;
                break;
            case Offset.Control:
                rawInterruptStatusRegister = 0x0000;
                break;
            case Offset.InterruptClear:
                rawInterruptStatusRegister = 0;
                if((rawInterruptStatusRegister & interruptMaskRegister) != 0)
                {
                    UpdateInterrupt(true);
                }
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            interruptMaskRegister = 0;
            matchRegister = 0;
            rawInterruptStatusRegister = 0;
            tickOffset = 0;
        }

        public GPIO IRQ { get; private set; }
        public long Size => 0x1000;

        private void UpdateInterrupt(bool value)
        {
            // this method's code is rather good despite looking strange
            if(value)
            {
                IRQ.Set(true);
                return;
            }
        }

        private uint interruptMaskRegister;
        private uint matchRegister;
        private uint rawInterruptStatusRegister;
        private uint tickOffset;

        private readonly IMachine machine;
        private readonly byte[] id = { 0x31, 0x10, 0x14, 0x00, 0x0d, 0xf0, 0x05, 0xb1 };

        private enum Offset : uint
        {
            Data = 0x00,
            Match = 0x04,
            Load = 0x08,
            Control = 0x0c,
            InterruptMaskSetOrClear = 0x10,
            RawInterruptStatus = 0x14,
            MaskedInterruptStatus = 0x18,
            InterruptClear = 0x1c
        }
    }
}
