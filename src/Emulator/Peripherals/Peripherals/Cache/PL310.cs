//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2025 CarByte
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
// Based on the PL310 documentation:
// https://developer.arm.com/documentation/ddi0246/a/

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
                return 0x410000C0; // ARM Revision r0p0
            case Offset.AuxiliaryControl:
                return 0x02020000; // Default value for Auxiliary Control register       
            case Offset.Control:
                return registerControl;
            case Offset.PrefetchControl:
                return 0x00000000; // Default value for Prefetch Control register 
            case Offset.PowerControl:
                return 0x00000000; // Default value for Power Control register
            case Offset.InvalidateByWay:
                return 0x00000000; // Invalidation is always finished
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
            case Offset.Control:
                registerControl = value;
                break;
            case Offset.CacheSync:
            case Offset.CacheSyncProbably:
            case Offset.InvalidateLineByPA:
            case Offset.CleanLinebyPA:
            case Offset.CleanAndInvalidateLine:
            case Offset.Debug:            
            case Offset.AuxiliaryControl:
            case Offset.TagRamControl:
            case Offset.DataRamControl:
            case Offset.InvalidateByWay:
            case Offset.PrefetchControl:
            case Offset.PowerControl:
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            registerControl = 0x00000000; // Default value for Control register
        }

        private uint registerControl;

        public enum Offset
        {
            CacheId = 0x0,
            Control = 0x100,
            AuxiliaryControl = 0x104,
            TagRamControl = 0x108,
            DataRamControl = 0x10C,
            CacheSync = 0x730,
            CacheSyncProbably = 0x740, // TODO: check this offset,
            InvalidateLineByPA = 0x770,
            InvalidateByWay = 0x77C,
            CleanLinebyPA = 0x7b0,
            CleanAndInvalidateLine = 0x7f0,
            Debug = 0xf40,
            PrefetchControl = 0xf60,
            PowerControl = 0xf80
        }
    }
}