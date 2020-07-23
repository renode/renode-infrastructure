//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class K6xF_SIM : IDoubleWordPeripheral, IKnownSize
    {
        public K6xF_SIM(uint? uniqueIdHigh = null, uint? uniqueIdMidHigh = null, uint? uniqueIdMidLow = null, uint? uniqueIdLow = null)
        {
            var rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            
            this.uniqueIdHigh = uniqueIdHigh.HasValue ? uniqueIdHigh.Value : (uint)rng.Next();
            this.uniqueIdMidHigh = uniqueIdMidHigh.HasValue ? uniqueIdMidHigh.Value : (uint)rng.Next();
            this.uniqueIdMidLow = uniqueIdMidLow.HasValue ? uniqueIdMidLow.Value : (uint)rng.Next();
            this.uniqueIdLow = uniqueIdLow.HasValue ? uniqueIdLow.Value : (uint)rng.Next();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.UniqueIdHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdHigh;
                    }, name: "SIM_UIDH")
                },
                {(long)Registers.UniqueIdMidHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdMidHigh;
                    }, name: "SIM_UIDMH")
                },
                {(long)Registers.UniqueIdMidLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdMidLow;
                    }, name: "SIM_UIDML")
                },
                {(long)Registers.UniqueIdLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdLow;
                    }, name: "SIM_UIDL")
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1060;

        private readonly DoubleWordRegisterCollection registers;
        private readonly uint uniqueIdHigh;
        private readonly uint uniqueIdMidHigh;
        private readonly uint uniqueIdMidLow;
        private readonly uint uniqueIdLow;

        private enum Registers
        {
            Options1 = 0x0,
            Configuration = 0x4,
            Options2 = 0x1004,
            Options4 = 0x100C,
            Options5 = 0x1010,
            Options7 = 0x1018,
            DeviceID = 0x1024,
            ClockGatingControl1 = 0x1028,
            ClockGatingControl2 = 0x102C,
            ClockGatingControl3 = 0x1030,
            ClockGatingControl4 = 0x1034,
            ClockGatingControl5 = 0x1038,
            ClockGatingControl6 = 0x103C,
            ClockGatingControl7 = 0x1040,
            ClockDiv1 = 0x1044,
            ClockDiv2 = 0x1048,
            FlashConfig1 = 0x104C,
            FlashConfig2 = 0x1050,
            UniqueIdHigh = 0x1054,
            UniqueIdMidHigh = 0x1058,
            UniqueIdMidLow = 0x105C,
            UniqueIdLow = 0x1060
        }
    }
}
