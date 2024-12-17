//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This peripheral implements an interface to Arm CoreSight SoC-400 timestamp generator module
    public class RenesasRZG_SYC : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasRZG_SYC(IMachine machine, long frequency) : base(machine)
        {
            timer = new LimitTimer(machine.ClockSource, frequency, this, "Timestamp generator", ulong.MaxValue, Direction.Ascending, workMode: WorkMode.Periodic);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            timer.Reset();
            timerValueLowerOverride = null;
        }

        public long Size => 0x10000;

        private void DefineRegisters()
        {
            Registers.CounterControl.Define(this)
                .WithFlag(0, name: "EN",
                    valueProviderCallback: _ => timer.Enabled,
                    writeCallback: (_, value) => timer.Enabled = value)
                .WithTaggedFlag("HDBG", 1)
                .WithReservedBits(2, 30);

            Registers.CounterStatus.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("DBGH (Debug Halted)", 1)
                .WithReservedBits(2, 30);

            Registers.CounterValueLower.Define(this)
                .WithValueField(0, 32, name: "CNTCVL_L_32",
                    valueProviderCallback: _ => timer.Value,
                    writeCallback: (_, value) => timerValueLowerOverride = (uint)value);

            Registers.CounterValueUpper.Define(this)
                .WithValueField(0, 32, name: "CNTCVU_U_32",
                    valueProviderCallback: _ => timer.Value >> 32,
                    writeCallback: (_, value) =>
                    {
                        var newValue = (value << 32) | (timerValueLowerOverride ?? (uint)timer.Value);
                        timerValueLowerOverride = null;

                        if(!timer.Enabled)
                        {
                            this.WarningLog("Attempt to set the counter value to {0} while timer is running, ignoring", newValue);
                            return;
                        }
                        timer.Value = newValue;
                    });

            Registers.BaseFrequencyId.Define(this)
                .WithValueField(0, 32, name: "FREQ",
                    valueProviderCallback: _ => (uint)timer.Frequency,
                    writeCallback: (_, value) => timer.Frequency = (long)value);

            Registers.CounterValueLowerReadOnly.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CNTCVL_L_32",
                    valueProviderCallback: _ => timer.Value);

            Registers.CounterValueUpperReadOnly.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CNTCVU_U_32",
                    valueProviderCallback: _ => timer.Value >> 32);

            DefineManagementRegisters(0x0);
            DefineManagementRegisters(0x1000);
        }

        private void DefineManagementRegisters(int offset)
        {
            (ManagementRegisters.PeripheralID4 + offset).Define(this, 0x4)
                .WithTag("DES_2", 0, 4)
                .WithTag("SIZE", 4, 4)
                .WithReservedBits(8, 24);

            (ManagementRegisters.PeripheralID0 + offset).Define(this, 0x1)
                .WithTag("PART_0", 0, 8)
                .WithReservedBits(8, 24);

            (ManagementRegisters.PeripheralID1 + offset).Define(this, 0xB1)
                .WithTag("PART_1", 0, 4)
                .WithTag("DES_0", 4, 4)
                .WithReservedBits(8, 24);

            (ManagementRegisters.PeripheralID2 + offset).Define(this, 0x1B)
                .WithTag("DES_1", 0, 3)
                .WithTaggedFlag("JEDEC", 3)
                .WithTag("REVISION", 4, 4)
                .WithReservedBits(8, 24);

            (ManagementRegisters.PeripheralID3 + offset).Define(this, 0x0)
                .WithTag("CMOD", 0, 4)
                .WithTag("REVAND", 4, 4)
                .WithReservedBits(8, 24);

            (ManagementRegisters.ComponentID0 + offset).Define(this, 0xD)
                .WithTag("PRMBL_0", 0, 8)
                .WithReservedBits(8, 24);

            (ManagementRegisters.ComponentID1 + offset).Define(this, 0xF0)
                .WithTag("PRMBL_1", 0, 4)
                .WithTag("CLASS", 4, 4)
                .WithReservedBits(8, 24);

            (ManagementRegisters.ComponentID2 + offset).Define(this, 0x5)
                .WithTag("PRMBL_2", 0, 8)
                .WithReservedBits(8, 24);

            (ManagementRegisters.ComponentID3 + offset).Define(this, 0xB1)
                .WithTag("PRMBL_3", 0, 8)
                .WithReservedBits(8, 24);
        }

        private readonly LimitTimer timer;
        // Nullable uint is used, so that software has the ability to only override
        // the upper half of the counter's value
        private uint? timerValueLowerOverride;

        private enum Registers
        {
            CounterControl            = 0x0000, // CNTCR
            CounterStatus             = 0x0004, // CNTSR
            CounterValueLower         = 0x0008, // CNTCVL
            CounterValueUpper         = 0x000C, // CNTCVU
            BaseFrequencyId           = 0x0020, // CNTFID0
            CounterValueLowerReadOnly = 0x1000, // CNTCVL
            CounterValueUpperReadOnly = 0x1004, // CNTCVU
        }

        private enum ManagementRegisters
        {
            PeripheralID4 = 0xFD0, // PIDR4
            PeripheralID0 = 0xFE0, // PIDR0
            PeripheralID1 = 0xFE4, // PIDR1
            PeripheralID2 = 0xFE8, // PIDR2
            PeripheralID3 = 0xFEC, // PIDR3
            ComponentID0  = 0xFF0, // CIDR0
            ComponentID1  = 0xFF4, // CIDR1
            ComponentID2  = 0xFF8, // CIDR2
            ComponentID3  = 0xFFC, // CIDR3
        }
    }
}
