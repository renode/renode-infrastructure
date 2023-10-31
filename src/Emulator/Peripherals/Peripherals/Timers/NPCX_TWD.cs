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
    public class NPCX_TWD : IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public NPCX_TWD()
        {
            RegistersCollection = new ByteRegisterCollection(this, BuildRegisterMap());

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x08;
        public ByteRegisterCollection RegistersCollection { get; }

        private Dictionary<long, ByteRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.Configuration, new ByteRegister(this)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("WDSDME (Watchdog Touch Select)", 5)
                    .WithTaggedFlag("WDCT0I (Watchdog Clock Select)", 4)
                    .WithTaggedFlag("LWDCNT (Lock Watchdog Counter)", 3)
                    .WithTaggedFlag("LTWDT0 (Lock T0 Timer)", 2)
                    .WithTaggedFlag("LTWCP (Lock Prescalers)", 1)
                    .WithTaggedFlag("LTWCFG (Lock Watchdog Configuration)", 0)
                },
                {(long)Registers.ClockPrescaler, new ByteRegister(this)
                    .WithReservedBits(4, 4)
                    .WithTag("MDIV", 0, 4)
                },
                {(long)Registers.Timer0_0, new ByteRegister(this)
                    .WithTag("T0_PRESET (T0 Counter Preset)", 0, 8)
                },
                {(long)Registers.Timer0_1, new ByteRegister(this)
                    .WithTag("T0_PRESET (T0 Counter Preset)", 0, 8)
                },
                {(long)Registers.Timer0ControlAndStatus, new ByteRegister(this)
                    .WithTaggedFlag("TESDIS (Too Early Service Disable)", 7)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag("WD_RUN (Watchdog Run Status)", 5)
                    .WithTaggedFlag("WDRST_STS (Watchdog Reset Status)", 4)
                    .WithTaggedFlag("WDLTD (Watchdog Last Touch Delay)", 3)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("TC (Terminal Count)", 1)
                    .WithTaggedFlag("RST (Reset)", 0)
                },
                {(long)Registers.WatchdogCount, new ByteRegister(this)
                    .WithTag("WD_PRESET (Watchdog Counter Preset)", 0, 8)
                },
                {(long)Registers.WatchdogServiceDataMatch, new ByteRegister(this)
                    .WithTag("RSDATA (Watchdog Restart Data)", 0, 8)
                },
                {(long)Registers.Timer0Counter_0, new ByteRegister(this)
                    .WithTag("T0_COUNT (T0 Counter Value)", 0, 8)
                },
                {(long)Registers.Timer0Counter_1, new ByteRegister(this)
                    .WithTag("T0_COUNT (T0 Counter Value)", 0, 8)
                },
                {(long)Registers.WatchdogCounter, new ByteRegister(this)
                    .WithTag("WD_COUNT (Watchdog Counter Value)", 0, 8)
                },
                {(long)Registers.WatchdogClockPrescaler, new ByteRegister(this)
                    .WithReservedBits(4, 4)
                    .WithTag("WDIV", 0, 4)
                },
            };

            return registerMap;
        }

        private enum Registers : long
        {
            Configuration            = 0x00, // TWCFG
            ClockPrescaler           = 0x02, // TWCP
            Timer0_0                 = 0x04,
            Timer0_1                 = 0x05, // TWDT0
            Timer0ControlAndStatus   = 0x06, // T0CSR
            WatchdogCount            = 0x08, // WDCNT
            WatchdogServiceDataMatch = 0x0A, // WDSDM
            Timer0Counter_0          = 0x0C,
            Timer0Counter_1          = 0x0D, // TWMT0
            WatchdogCounter          = 0x0E, // TWMWD
            WatchdogClockPrescaler   = 0x10, // WDCP
        }
    }
}
