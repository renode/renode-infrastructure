//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class DWT: BasicDoubleWordPeripheral, IKnownSize
    {
        public DWT(IMachine machine, uint frequency): base(machine)
        {
            CreateRegisters();
            cycleCounter = new LimitTimer(machine.ClockSource, frequency, this, "CycleCounter", direction: Direction.Ascending);
        }

        public override void Reset()
        {
            base.Reset();
            cycleCounter.Reset();
        }

        private void CreateRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, writeCallback: (_, val) => 
                    {
                        cycleCounter.Enabled = val;
                        this.Log(LogLevel.Debug, "{0}", val ? "Enabled" : "Disabled");
                    }, valueProviderCallback: _ => cycleCounter.Enabled, name: "CYCCNTENA")
                .WithTag("POSTPRESET", 1, 4)
                .WithTag("POSTCNT", 5, 4)
                .WithTag("CYCTAP", 9, 1)
                .WithTag("SYNCTAP", 10, 2)
                .WithTag("PCSAMPLEENA", 12, 1)
                .WithReservedBits(13, 3)
                .WithTag("EXCTRCENA", 16, 1)
                .WithTag("CPIEVTENA", 17, 1)
                .WithTag("EXCEVTENA", 18, 1)
                .WithTag("SLEEPEVTENA", 19, 1)
                .WithTag("LSUEVTENA", 20, 1)
                .WithTag("FOLDEVTENA", 21, 1)
                .WithTag("CYCEVTEN", 22, 1)
                .WithReservedBits(23, 5)
                .WithTag("NUMCOMP", 28, 4);
            Registers.CycleCounter.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => { cycleCounter.Value = val; },
                    valueProviderCallback: _ =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        return (uint)cycleCounter.Value;
                    }, name: "CYCCNT");
            Registers.Count.Define(this)
                .WithTag("CPICNT", 0, 8) 
                .WithReservedBits(8, 24);
            Registers.ExceptionOverheadCounter.Define(this)
                .WithTag("EXCCNT", 0, 8) 
                .WithReservedBits(8, 24);
            Registers.SleepCounter.Define(this)
                .WithTag("SLEEPCNT", 0, 8) 
                .WithReservedBits(8, 24);
            Registers.LoadStoreUnitCounter.Define(this)
                .WithTag("LSUCNT", 0, 8) 
                .WithReservedBits(8, 24);
            Registers.FoldCounter.Define(this)
                .WithTag("FOLDCNT", 0, 8) 
                .WithReservedBits(8, 24);
            Registers.ProgramCounterSample.Define(this)
                .WithTag("EIASAMPLE", 0, 32);
            Registers.Comparator0.Define(this)
                .WithTag("COMP", 0, 32);
            Registers.Mask0.Define(this)
                .WithTag("MASK", 0, 4)
                .WithReservedBits(4, 28);
            Registers.Function0.Define(this)
                .WithTaggedFlag("FUNCTION", 0)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("EMITRANGE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("CYCMATCH", 7)
                .WithTaggedFlag("DATAVMATCH", 8)
                .WithTaggedFlag("LNK1ENA", 9)
                .WithTag("DATAVSIZE", 10, 2)
                .WithTag("DATAVADDR0", 12, 4)
                .WithTag("DATAVADDR1", 16, 4)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("MATCHED", 24)
                .WithReservedBits(25, 7);
            Registers.Comparator1.Define(this)
                .WithTag("COMP", 0, 32);
            Registers.Mask1.Define(this)
                .WithTag("MASK", 0, 4)
                .WithReservedBits(4, 28);
            Registers.Function1.Define(this)
                .WithTaggedFlag("FUNCTION", 0)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("EMITRANGE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("CYCMATCH", 7)
                .WithTaggedFlag("DATAVMATCH", 8)
                .WithTaggedFlag("LNK1ENA", 9)
                .WithTag("DATAVSIZE", 10, 2)
                .WithTag("DATAVADDR0", 12, 4)
                .WithTag("DATAVADDR1", 16, 4)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("MATCHED", 24)
                .WithReservedBits(25, 7);
            Registers.Comparator2.Define(this)
                .WithTag("COMP", 0, 32);
            Registers.Mask2.Define(this)
                .WithTag("MASK", 0, 4)
                .WithReservedBits(4, 28);
            Registers.Function2.Define(this)
                .WithTaggedFlag("FUNCTION", 0)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("EMITRANGE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("CYCMATCH", 7)
                .WithTaggedFlag("DATAVMATCH", 8)
                .WithTaggedFlag("LNK1ENA", 9)
                .WithTag("DATAVSIZE", 10, 2)
                .WithTag("DATAVADDR0", 12, 4)
                .WithTag("DATAVADDR1", 16, 4)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("MATCHED", 24)
                .WithReservedBits(25, 7);
            Registers.Comparator3.Define(this)
                .WithTag("COMP", 0, 32);
            Registers.Mask3.Define(this)
                .WithTag("MASK", 0, 4)
                .WithReservedBits(4, 28);
            Registers.Function3.Define(this)
                .WithTaggedFlag("FUNCTION", 0)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("EMITRANGE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("CYCMATCH", 7)
                .WithTaggedFlag("DATAVMATCH", 8)
                .WithTaggedFlag("LNK1ENA", 9)
                .WithTag("DATAVSIZE", 10, 2)
                .WithTag("DATAVADDR0", 12, 4)
                .WithTag("DATAVADDR1", 16, 4)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("MATCHED", 24)
                .WithReservedBits(25, 7);
            Registers.PeripheralID4.Define(this, 0x04)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID5.Define(this, 0x00)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID6.Define(this, 0x00)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID7.Define(this, 0x00)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID0.Define(this, 0x02)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID1.Define(this, 0xB0)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID2.Define(this, 0x1B)
                .WithTag("PID", 0, 32);
            Registers.PeripheralID3.Define(this, 0x00)
                .WithTag("PID", 0, 32);
            Registers.ComponentID0.Define(this, 0x0D)
                .WithTag("CID", 0, 32);
            Registers.ComponentID1.Define(this, 0xE0)
                .WithTag("CID", 0, 32);
            Registers.ComponentID2.Define(this, 0x05)
                .WithTag("CID", 0, 32);
            Registers.ComponentID3.Define(this, 0xB1)
                .WithTag("CID", 0, 32);
        }

        public long Size => 0x1000;

        private readonly LimitTimer cycleCounter;

        private enum Registers
        {
            Control                  = 0x000, // Control Register 
            CycleCounter             = 0x004, // Cycle Count Register 
            Count                    = 0x008, // Count Register 
            ExceptionOverheadCounter = 0x00C, // Exception OverheadCount Register 
            SleepCounter             = 0x010, // Sleep Counter Register 
            LoadStoreUnitCounter     = 0x014, // Load-Store-UnitCounter Register 
            FoldCounter              = 0x018, // Fold Counter Register 
            ProgramCounterSample     = 0x01C, // Program CounterSample Register 
            Comparator0              = 0x020, // ComparatorRegister #0 
            Mask0                    = 0x024, // Mask Register #0 
            Function0                = 0x028, // Function Register#0 
            Comparator1              = 0x030, // ComparatorRegister #1 
            Mask1                    = 0x034, // Mask Register #1 
            Function1                = 0x038, // Function Register#1 
            Comparator2              = 0x040, // ComparatorRegister #2 
            Mask2                    = 0x044, // Mask Register #2 
            Function2                = 0x048, // Function Register#2 
            Comparator3              = 0x050, // ComparatorRegister #3 
            Mask3                    = 0x054, // Mask Register #3 
            Function3                = 0x058, // Function Register#3 
            PeripheralID4            = 0xFD0, // Peripheral ID4 
            PeripheralID5            = 0xFD4, // Peripheral ID5 
            PeripheralID6            = 0xFD8, // Peripheral ID6 
            PeripheralID7            = 0xFDC, // Peripheral ID7 
            PeripheralID0            = 0xFE0, // Peripheral ID0 
            PeripheralID1            = 0xFE4, // Peripheral ID1 
            PeripheralID2            = 0xFE8, // Peripheral ID2 
            PeripheralID3            = 0xFEC, // Peripheral ID3 
            ComponentID0             = 0xFF0, // Component ID0 
            ComponentID1             = 0xFF4, // Component ID1 
            ComponentID2             = 0xFF8, // Component ID2 
            ComponentID3             = 0xFFC, // Component ID3 
        }
    }
}

