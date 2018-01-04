//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Cache
{
    public class PL310 : IDoubleWordPeripheral
    {
        public uint ReadDoubleWord(long offset)
        {
            switch((Offset)offset)
            {
            case Offset.CacheId:
                return 0x410000C0;
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
            case Offset.CacheSync:
            case Offset.CacheSyncProbably:
            case Offset.InvalidateLineByPA:
            case Offset.CleanLinebyPA:
            case Offset.CleanAndInvalidateLine:
            case Offset.Debug:
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {

        }

        public enum Offset
        {
            CacheId = 0x0,
            CacheSync = 0x730,
            CacheSyncProbably = 0x740, // TODO: check this offset,
            InvalidateLineByPA = 0x770,
            CleanLinebyPA = 0x7b0,
            CleanAndInvalidateLine = 0x7f0,
            Debug = 0xf40
        }

    }
}

