//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Timers
{
    public sealed class CC2538Watchdog : SimpleTicker, IKnownSize
    {
        public CC2538Watchdog(ulong periodInMs, IMachine machine) : base(periodInMs, machine)
        {
            Reset();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(value == 0x5 && previousValue == 0xA)
            {
                Reset();
            }
            else
            {
                previousValue = value;
            }
        }

        public override void Reset()
        {
            previousValue = 0;
            base.Reset();
        }

        public long Size
        {
            get
            {
                return 0x4;
            }
        }

        private uint previousValue;
    }
}

