//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class TegraUsecTimer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public TegraUsecTimer(IMachine machine) : base(machine.ClockSource, 1000000, direction: Direction.Ascending, limit: uint.MaxValue, enabled: true)
        {
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Value:
                return (uint)Value;
            case Registers.Config:
                return (uint)((usecDividend << 8) | usecDivisor);
            case Registers.Freeze:
                return freeze;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Value:
                this.Log(LogLevel.Warning, "Unexpected write to readonly register 0x{0:X}, value 0x{1:X}.", offset, value);
                break;
            case Registers.Config:
                usecDivisor = (byte)(value & 0xF);
                usecDividend = (byte)((value >> 8) & 0xF);
                break;
            case Registers.Freeze:
                freeze = (byte)value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public override void Reset()
        {
            usecDivisor = 0xc;
            usecDividend = 0x0;
        }

        public long Size
        {
            get
            {
                return 64;
            }
        }

        private byte usecDivisor;
        private byte usecDividend;
        private byte freeze;

        private enum Registers
        {
            Value  = 0x00,
            Config = 0x04,
            Freeze = 0x3c,
        }
    }
}