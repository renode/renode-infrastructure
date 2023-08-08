//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Video
{
    public class TegraSyncpts : IDoubleWordPeripheral, IKnownSize
    {
        public TegraSyncpts(IMachine machine)
        {
  //          this.machine = machine;

//            sync = new object();

            sync_pts = new uint[23];
            for (int i = 0; i < sync_pts.Length; i++) sync_pts[i] = 0;
        }

        public long Size
        {
            get
            {
                return 0x4000;
            }
        }

        public void WriteDoubleWord(long address, uint value)
        {
            this.Log(LogLevel.Warning, "Write to unknown offset {0:X}, value {1:X}",address,value);
        }

        public uint ReadDoubleWord(long offset)
        {
            if ((offset >= 0x3400) && (offset <= 0x3458)) {
                       uint sync_id = (uint)((offset - 0x3400) / 4);
                       sync_pts[sync_id] += 1;
                       return sync_pts[sync_id];
            }
            switch (offset) {
               case 0x3040: // HOST1X_SYNC_SYNCPT_THRESH_CPU0_INT_STATUS_0
                       this.Log(LogLevel.Warning, "Read from CPU0_INT_STATUS");
                       return (1<<22) | (1<<13);
               default:
                       this.Log(LogLevel.Warning, "Read from unknown offset {0:X}, returning 0",offset);
                       break;
            }
            return 0;
        }

        public void Reset() {
        }

        uint[] sync_pts;

//        private object sync;

//        private readonly IMachine machine;
    }
}

