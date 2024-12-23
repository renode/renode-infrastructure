//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RenesasRZG_CPG_SYSC : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasRZG_CPG_SYSC(IMachine machine): base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x10000;

        private void DefineRegisters()
        {
            DefineRegistersForPeripheral(Registers.ClockControlMHU, Registers.ClockMonitorMHU,
                Registers.ResetControlMHU, Registers.ResetMonitorMHU, NrOfMhuClocks, out mhuClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlGTM, Registers.ClockMonitorGTM,
                Registers.ResetControlGTM, Registers.ResetMonitorGTM, NrOfGtmClocks, out gtmClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlGPT, Registers.ClockMonitorGPT,
                Registers.ResetControlGPT, Registers.ResetMonitorGPT, NrOfGptClocks, out gptClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlSCIF, Registers.ClockMonitorSCIF,
                Registers.ResetControlSCIF, Registers.ResetMonitorSCIF, NrOfScifClocks, out scifClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlRSPI, Registers.ClockMonitorRSPI,
                Registers.ResetControlRSPI, Registers.ResetMonitorRSPI, NrOfRspiClocks, out rspiClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlGPIO, Registers.ClockMonitorGPIO,
                Registers.ResetControlGPIO, Registers.ResetMonitorGPIO, NrOfGpioClocks, out gpioClockEnabled);

            DefineRegistersForPeripheral(Registers.ClockControlI2C, Registers.ClockMonitorI2C,
                Registers.ResetControlI2C, Registers.ResetMonitorI2C, NrOfI2cClocks, out i2cClockEnabled);
        }

        private void DefineRegistersForPeripheral(Registers clockControl, Registers clockMonitor,
            Registers resetControl, Registers resetMonitor, int nrOfClocks, out IFlagRegisterField[] clockEnabled)
        {
            clockControl.Define(this)
                .WithFlags(0, nrOfClocks, out clockEnabled, name: "CLK_ON")
                .WithReservedBits(nrOfClocks, 16 - nrOfClocks)
                .WithFlags(16, nrOfClocks, FieldMode.Set | FieldMode.Read,
                    valueProviderCallback: (_, __) => false,
                    name: "CLK_ONWEN")
                .WithReservedBits(16 + nrOfClocks, 16 - nrOfClocks)
                // We have to use register write callback,
                // because multiple fields, depending on each other,
                // can be changed in one write.
                .WithWriteCallback(CreateClockControlWriteCallback(clockControl, clockEnabled));

            clockMonitor.Define(this)
                .WithFlags(0, nrOfClocks, FieldMode.Read,
                    valueProviderCallback: CreateClockMonitorValueProviderCallback(clockEnabled),
                    name: "CLK_MON")
                .WithReservedBits(nrOfClocks, 32 - nrOfClocks);

            // We don't really implement resets, but we still should log
            // if we write to register when write is disabled.
            resetControl.Define(this)
                .WithFlags(0, nrOfClocks,
                    valueProviderCallback: (_, __) => false,
                    name: "UNIT_RSTB")
                .WithReservedBits(nrOfClocks, 16 - nrOfClocks)
                .WithFlags(16, nrOfClocks, FieldMode.Set | FieldMode.Read,
                    valueProviderCallback: (_, __) => false,
                    name: "UNIT_RSTWEN")
                .WithReservedBits(16 + nrOfClocks, 16 - nrOfClocks)
                // We have to use register write callback,
                // because multiple fields, depending on each other,
                // can be changed in one write.
                .WithWriteCallback(CreateResetControlWriteCallback(resetControl));

            // Reset is instantenous, so we always return 0, which means that we aren't in reset state.
            resetMonitor.Define(this)
                .WithFlags(0, nrOfClocks, FieldMode.Read, name: "RST_MON")
                .WithReservedBits(nrOfClocks, 32 - nrOfClocks);
        }

        private Action<uint, uint> CreateClockControlWriteCallback(Registers register, IFlagRegisterField[] clockEnable)
        {
            return (oldVal, newVal) =>
            {
                var invalidClocks = GetInvalidBits(oldVal, newVal, ClockEnableBitsOffset,
                    ClockEnableBitsSize, ClockWriteEnableBitsOffset, ClockWriteEnableBitsSize);

                if(invalidClocks != 0)
                {
                    BitHelper.ForeachActiveBit(invalidClocks, clockIdx =>
                    {
                        this.WarningLog(
                            "Trying to toggle clock {0} in register {1}. Writing to this clock is disabled. Clock status won't be changed.",
                            clockIdx,
                            register
                        );
                        clockEnable[clockIdx].Value = !clockEnable[clockIdx].Value;
                    });
                }
            };
         }

        private Action<uint, uint> CreateResetControlWriteCallback(Registers register)
        {
            return (oldVal, newVal) =>
            {
                var invalidResets = GetInvalidBits(oldVal, newVal, ResetEnableBitsOffset,
                    ResetEnableBitsSize, ResetWriteEnableBitsOffset, ResetWriteEnableBitsSize);

                if(invalidResets != 0)
                {
                    BitHelper.ForeachActiveBit(invalidResets, resetIdx =>
                    {
                        this.WarningLog(
                            "Trying to toggle reset signal {0} in register {1}. Writing to this signal is disabled. Signal status won't be changed.",
                            resetIdx,
                            register
                        );
                    });
                }
            };
        }

        private Func<int, bool, bool> CreateClockMonitorValueProviderCallback(IFlagRegisterField[] clockEnabled)
        {
            return (clockIdx, _) => clockEnabled[clockIdx].Value;
        }

        private uint GetInvalidBits(uint oldVal, uint newVal, int valueOffset, int valueSize, int maskOffset, int maskSize)
        {
            var mask = BitHelper.GetValue(newVal, maskOffset, maskSize);
            oldVal = BitHelper.GetValue(oldVal, valueOffset, valueSize);
            newVal = BitHelper.GetValue(newVal, valueOffset, valueSize);

            // We mark as invalid, bits that changed, but were not masked.
            var changed = oldVal ^ newVal;
            var invalid = changed & ~mask;

            return invalid;
        }

        private IFlagRegisterField[] mhuClockEnabled;
        private IFlagRegisterField[] gtmClockEnabled;
        private IFlagRegisterField[] gptClockEnabled;
        private IFlagRegisterField[] i2cClockEnabled;
        private IFlagRegisterField[] scifClockEnabled;
        private IFlagRegisterField[] rspiClockEnabled;
        private IFlagRegisterField[] gpioClockEnabled;

        private const int NrOfMhuClocks = 1;
        private const int NrOfGtmClocks = 3;
        private const int NrOfGptClocks = 1;
        private const int NrOfI2cClocks = 4;
        private const int NrOfScifClocks = 5;
        private const int NrOfRspiClocks = 3;
        private const int NrOfGpioClocks = 1;

        private const int ClockEnableBitsOffset = 0;
        private const int ClockEnableBitsSize = 16;
        private const int ClockWriteEnableBitsOffset = 16;
        private const int ClockWriteEnableBitsSize = 16;
        private const int ResetEnableBitsOffset = 0;
        private const int ResetEnableBitsSize = 16;
        private const int ResetWriteEnableBitsOffset = 16;
        private const int ResetWriteEnableBitsSize = 16;

        private enum Registers
        {
            // Clock Control
            ClockControlMHU     = 0x520,    // CPG_CLKON_MHU
            ClockControlGTM     = 0x534,    // CPG_CLKON_GTM
            ClockControlGPT     = 0x540,    // CPG_CLKON_GPT
            ClockControlI2C     = 0x580,    // CPG_CLKON_I2C
            ClockControlSCIF    = 0x584,    // CPG_CLKON_SCIF
            ClockControlRSPI    = 0x590,    // CPG_CLKON_RSPI
            ClockControlGPIO    = 0x598,    // CPG_CLKON_GPIO

            // Clock Monitor
            ClockMonitorMHU     = 0x6A0,    // CPG_CLMON_MHU
            ClockMonitorGTM     = 0x6B4,    // CPG_CLMON_GTM
            ClockMonitorGPT     = 0x6C0,    // CPG_CLMON_GPT
            ClockMonitorI2C     = 0x700,    // CPG_CLMON_I2C
            ClockMonitorSCIF    = 0x704,    // CPG_CLMON_SCIF
            ClockMonitorRSPI    = 0x710,    // CPG_CLMON_RSPI
            ClockMonitorGPIO    = 0x718,    // CPG_CLMON_GPIO

            // Reset Control
            ResetControlMHU     = 0x820,    // CPG_RST_MHU
            ResetControlGTM     = 0x834,    // CPG_RST_GTM
            ResetControlGPT     = 0x844,    // CPG_RST_GPT
            ResetControlI2C     = 0x880,    // CPG_RST_I2C
            ResetControlSCIF    = 0x884,    // CPG_RST_SCIF
            ResetControlRSPI    = 0x890,    // CPG_RST_RSPI
            ResetControlGPIO    = 0x898,    // CPG_RST_GPIO

            // Reset Monitor
            ResetMonitorMHU     = 0x9A0,    // CPG_RSTMON_MHU
            ResetMonitorGTM     = 0x9B4,    // CPG_RSTMON_GTM
            ResetMonitorGPT     = 0x9C0,    // CPG_RSTMON_GPT
            ResetMonitorI2C     = 0xA00,    // CPG_RSTMON_I2C
            ResetMonitorSCIF    = 0xA04,    // CPG_RSTMON_SCIF
            ResetMonitorRSPI    = 0xA10,    // CPG_RSTMON_RSPI
            ResetMonitorGPIO    = 0xA18,    // CPG_RSTMON_GPIO
        }
    }
}
