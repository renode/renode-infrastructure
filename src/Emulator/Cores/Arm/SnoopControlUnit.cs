//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SnoopControlUnit : IDoubleWordPeripheral
    {
        public SnoopControlUnit(Machine machine)
        {
            this.machine = machine;
        }

        public uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
            case 0x0:
                return scu;
            case 0x4:
                //TODO: should work!
                var numOfCPUs = machine.SystemBus.GetCPUs().Count();
                return (uint)(0x30 + numOfCPUs - 1);//((0xffffffff << NumOfCPUs) ^ 0xffffffff) << 4 + NumOfCPUs - 1;// [7:4] - 1 for SMP, 0 for AMP, [1:0] - number of processors - 1
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
            case 0x0:
                scu = value & 1;
                return;
            }
            this.LogUnhandledWrite(offset, value);
        }

        public void Reset()
        {
            scu = 0;
        }

        private uint scu;
        private readonly Machine machine;
    }
}

