//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class FSLNAND : IDoubleWordPeripheral, IKnownSize
    {
        public FSLNAND()
        {
            IRQ = new GPIO();
        }

        public long Size
        {
            get
            {
                return 0x10000;
            }
        }

        public GPIO IRQ { get; private set; }

        public uint ReadDoubleWord(long offset)
        {
            if(offset == 0x3F38)
            {
                return 0xFFFFFFFF;
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == 0x3F04)
            {
                IRQ.Set();
            }
            else if(offset == 0x3F38)
            {
                IRQ.Unset();
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public void Reset()
        {
        }

    }
}

