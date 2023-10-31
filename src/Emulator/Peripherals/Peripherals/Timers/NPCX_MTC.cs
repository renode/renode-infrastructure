//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NPCX_MTC : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NPCX_MTC()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TimingTicksCount, new DoubleWordRegister(this)
                    .WithTag("TTC (Timing Ticks Count)", 0, 32)
                },
                {(long)Registers.WakeUpTicksCount, new DoubleWordRegister(this)
                    .WithTaggedFlag("WIE (Wake-Up/Interrupt Enabled)", 31)
                    .WithTaggedFlag("PTO (Predefined Time Occurred)", 30)
                    .WithReservedBits(25, 5)
                    .WithTag("PT (Predefined Time)", 0, 25)
                },
            };
            RegistersCollection = new DoubleWordRegisterCollection(this, registerMap);

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x08;
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private enum Registers : long
        {
            TimingTicksCount = 0x00, // TTC
            WakeUpTicksCount = 0x04, // WTC
        }
    }
}
