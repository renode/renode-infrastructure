//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // MCG = Multipurpose Clock Generator
    public class K6xF_MCG : IBytePeripheral, IKnownSize
    {
        public K6xF_MCG()
        {
            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.Control1, new ByteRegister(this)
                    .WithTaggedFlag("IREFSTEN", 0)
                    .WithTaggedFlag("IRCLKEN", 1)
                    .WithTaggedFlag("IREFS", 2)
                    .WithTag("FRDIV", 3, 3)
                    .WithEnumField(6, 2, out clockSource, name: "CLKS")
                },
                {(long)Registers.Control2, new ByteRegister(this)
                    .WithTaggedFlag("IRCS", 0)
                    .WithTaggedFlag("LP", 1)
                    .WithTaggedFlag("EREFS", 2)
                    .WithTaggedFlag("HGO", 3)
                    .WithTag("RANGE", 4, 2)
                    .WithTaggedFlag("FCFTRIM", 6)
                    .WithTaggedFlag("LOCRE0", 7)
                },
                {(long)Registers.Control4, new ByteRegister(this)
                    .WithTaggedFlag("SCFTRIM", 0)
                    .WithTag("FCTRIM", 1, 4)
                    .WithEnumField(5, 2, out encoding, name: "DRST_DRS")
                    .WithTaggedFlag("DMX32", 7)
                },                
                {(long)Registers.Control5, new ByteRegister(this)
                    .WithTag("PRDIV0", 0, 5)
                    .WithTaggedFlag("PLLSTEN0", 5)
                    .WithEnumField(6, 1, out mcgPllStatus, name: "PLLCLKEN0")
                    .WithReservedBits(7, 1)
                },
                {(long)Registers.Control6, new ByteRegister(this)
                    .WithTag("VDIV0", 0, 5)
                    .WithTaggedFlag("CME0", 5)
                    .WithEnumField(6, 1, out pllSelected, name: "PLLS")
                    .WithTaggedFlag("LOLIE0", 7)
                },
                {(long)Registers.Status, new ByteRegister(this)
                    .WithTaggedFlag("IRCST", 0)
                    .WithTaggedFlag("OSCINIT0", 1)
                    .WithValueField(2, 2, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        switch(clockSource.Value)
                        {
                            case ClockSourceValues.External:
                                return (uint)ClockModeStatusValues.ExternalClock;
                            case ClockSourceValues.Internal:
                                return (uint)ClockModeStatusValues.InternalClock;
                            case ClockSourceValues.Either:
                                if (pllSelected.Value == PLLSelectValues.FLLSelected)
                                    return (uint)ClockModeStatusValues.FLL;
                                return (uint)ClockModeStatusValues.PLL;
                            default:
                                throw new ArgumentException("Unhandled clock source");
                        }
                    },name: "LOLS0")
                    .WithTaggedFlag("IREFST", 4)
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return (PLLSelectValues.PLLSelected == pllSelected.Value);
                    },name: "PLLST")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return (MCGPLLClockStatusValues.Active == mcgPllStatus.Value);
                    },name: "LOCK0")
                    .WithTaggedFlag("LOLS0", 7)
                }
            };

            registers = new ByteRegisterCollection(this, registersMap);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public byte ReadByte(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1001;

        private readonly ByteRegisterCollection registers;
        private readonly IEnumRegisterField<ClockSourceValues> clockSource;
        private readonly IEnumRegisterField<MCGPLLClockStatusValues> mcgPllStatus;
        private readonly IEnumRegisterField<PLLSelectValues> pllSelected;
        private readonly IEnumRegisterField<EncodingValues> encoding;

        private enum Registers
        {
            Control1 = 0x0,
            Control2 = 0x01,
            Control3 = 0x02,
            Control4 = 0x03,
            Control5 = 0x04,
            Control6 = 0x05,
            Status = 0x06,
            StatusControl = 0x08,
            AutoTrimHigh = 0x0A,
            AutoTrimLow = 0x0B,
            Control7 = 0x0C,
            Control8 = 0x0D,
            Control12 = 0x11,
            Status2 = 0x12,
            Test3 = 0x13,
            OSC_Control = 0x1000
        }

        private enum EncodingValues
        {
            LowRange = 0,
            MidRange = 1,
            MidHighRange = 2,
            HighRange = 3
        }

        private enum ClockSourceValues
        {
            Either = 0,
            Internal = 1,
            External = 2,
            Reserved = 3
        }

        private enum MCGPLLClockStatusValues
        {
            Inactive = 0,
            Active = 1
        }

        private enum PLLSelectValues
        {
            FLLSelected = 0,
            PLLSelected = 1
        }

        private enum ClockModeStatusValues
        {
            FLL = 0,
            InternalClock = 1,
            ExternalClock = 2,
            PLL = 3
        }
    }
}
