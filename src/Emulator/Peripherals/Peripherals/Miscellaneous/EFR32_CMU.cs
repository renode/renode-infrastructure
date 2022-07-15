//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class EFR32_CMU : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32_CMU(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.OscillatorEnableDisableCommand.Define32(this)
                .WithFlag(0, FieldMode.Write, name: "HFRCOEN", writeCallback: (_, val) => { if(val) { hfrcoens.Value = true; } })
                .WithFlag(1, FieldMode.Write, name: "HFRCODIS", writeCallback: (_, val) => { if(val) { hfrcoens.Value = false; } })
                .WithFlag(2, FieldMode.Write, name: "HFXOEN", writeCallback: (_, val) => { if(val) { hfxoens.Value = true; } })
                .WithFlag(3, FieldMode.Write, name: "HFXODIS", writeCallback: (_, val) => { if(val) { hfxoens.Value = false; } })
                .WithFlag(4, FieldMode.Write, name: "AUXHRFCOEN", writeCallback: (_, val) => { if(val) { auxhrfcoens.Value = true; } })
                .WithFlag(5, FieldMode.Write, name: "AUXHRFCODIS", writeCallback: (_, val) => { if(val) { auxhrfcoens.Value = false; } })
                .WithFlag(6, FieldMode.Write, name: "LFRCOEN", writeCallback: (_, val) => { if(val) { lfrcoens.Value = true; } })
                .WithFlag(7, FieldMode.Write, name: "LFRCODIS", writeCallback: (_, val) => { if(val) { lfrcoens.Value = false; } })
                .WithFlag(8, FieldMode.Write, name: "LFXOEN", writeCallback: (_, val) => { if(val) { lfxoens.Value = true; } })
                .WithFlag(9, FieldMode.Write, name: "LFXODIS", writeCallback: (_, val) => { if(val) { lfxoens.Value = false; } })
                .WithReservedBits(10, 22)
            ;
            
            Registers.Status.Define32(this)
                .WithFlag(0, out hfrcoens, FieldMode.Read, name: "HFRCOENS")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hfrcoens.Value, name: "HFRCORDY")
                .WithFlag(2, out hfxoens, FieldMode.Read, name: "HFXOENS")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => hfxoens.Value, name: "HFXORDY")
                .WithFlag(4, out auxhrfcoens, FieldMode.Read, name: "AUXHRFCOENS")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => auxhrfcoens.Value, name: "AUXHRFCORDY")
                .WithFlag(6, out lfrcoens, FieldMode.Read, name: "LFRCOENS")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => lfrcoens.Value, name: "LFRCORDY")
                .WithFlag(8, out lfxoens, FieldMode.Read, name: "LFXOENS")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => lfxoens.Value, name: "LFXORDY")
                .WithReservedBits(10, 6)
                .WithFlag(16, name: "CALRDY", valueProviderCallback: _ => true)
                .WithReservedBits(17, 4)
                .WithTaggedFlag("HFXOREQ", 21)
                .WithFlag(22, name: "HFXOPEAKDETRDY", valueProviderCallback: _ => true)
                .WithFlag(23, name: "HFXOSHUNTOPTRDY", valueProviderCallback: _ => true)
                .WithTaggedFlag("HFXOAMPHIGH", 24)
                .WithTaggedFlag("HFXOAMPLOW", 25)
                .WithTaggedFlag("HFXOREGILOW", 26)
                .WithReservedBits(27, 5)
            ;

            Registers.HighFrequencyClockSelectCommand.Define32(this)
                .WithEnumField(0, 3, out highFrequencyClockSelect, FieldMode.Write, name: "HF")
                .WithReservedBits(3, 29)
            ;

            Registers.HighFrequencyClockStatus.Define32(this)
                .WithEnumField<DoubleWordRegister, ClockSource>(0, 3, FieldMode.Read, name: "SELECTED", valueProviderCallback: _ => highFrequencyClockSelect.Value)
                .WithReservedBits(3, 29)
            ;
        }

        private IFlagRegisterField hfrcoens;
        private IFlagRegisterField hfxoens;
        private IFlagRegisterField auxhrfcoens;
        private IFlagRegisterField lfrcoens;

        private IFlagRegisterField lfxoens;

        private IEnumRegisterField<ClockSource> highFrequencyClockSelect;

        private enum ClockSource
        {
            HFRCO = 0x1,
            HFXO = 0x2,
            LFRCO = 0x3,
            LFXO = 0x4,
            HFRCODIV2 = 0x5,
            CLKIN0 = 0x7,
        }

        private enum Registers
        {
            Control = 0x000, //RW CMU Control Register
            HFRCOControl = 0x010, //RWH HFRCO Control Register
            AUXHFRCOControl = 0x018, //RW AUXHFRCO Control Register
            LFRCOControl = 0x020, //RW LFRCO Control Register
            HFXOControl = 0x024, //RW HFXO Control Register
            HFXOSTARTUPControl = 0x02C, //RW HFXO Startup Control
            HFXOSteadyStateControl = 0x030, //RW HFXO Steady State Control
            HFXOTimeoutControl = 0x034, //RW HFXO Timeout Control
            LFXOControl = 0x038, //RW LFXO Control Register
            CalibrationControl = 0x050, //RW Calibration Control Register
            CalibartionCounter = 0x054, //RWH Calibration Counter Register
            OscillatorEnableDisableCommand = 0x060, //W1 Oscillator Enable/Disable Command Register
            Command = 0x064, //W1 Command Register
            DebugTraceClockSelect = 0x070, //RW Debug Trace Clock Select
            HighFrequencyClockSelectCommand = 0x074, //W1 High Frequency Clock Select Command Register
            LowFrequencyAClockSelect = 0x080, //RW Low Frequency A Clock Select Register
            LowFrequencyBClockSelect = 0x084, //RW Low Frequency B Clock Select Register
            LowFrequencyEClockSelect = 0x088, //RW Low Frequency E Clock Select Register
            Status = 0x090, //R Status Register
            HighFrequencyClockStatus = 0x094, //R HFCLK Status Register
            HFXOTrimStatus = 0x09C, //R HFXO Trim Status
            InterruptFlag = 0x0A0, //R Interrupt Flag Register
            InterruptFlagSet = 0x0A4, //W1 Interrupt Flag Set Register
            InterruptFlagClear = 0x0A8, //(R)W1 Interrupt Flag Clear Register
            InterruptEnable = 0x0AC, //RW Interrupt Enable Register
            HighFrequencyBusClockEnable = 0x0B0, //RW High Frequency Bus Clock Enable Register 0
            HighFrequencyPeripheralClockEnable = 0x0C0, //RW High Frequency Peripheral Clock Enable Register 0
            HighFrequencyAlternateRadioPeripheralClockEnable = 0x0CC, //RW High Frequency Alternate Radio Peripheral Clock Enable Register 0
            LowFrequencyAClockEnable = 0x0E0, //RW Low Frequency a Clock Enable Register 0 (Async Reg)
            LowFrequencyBClockEnable = 0x0E8, //RW Low Frequency B Clock Enable Register 0 (Async Reg)
            LowFrequencyEClockEnable = 0x0F0, //RW Low Frequency E Clock Enable Register 0 (Async Reg)
            HighFrequencyClockPrescaler = 0x100, //RW High Frequency Clock Prescaler Register
            HighFrequencyCoreClockPrescaler = 0x108, //RW High Frequency Core Clock Prescaler Register
            HighFrequencyPeripheralClockPrescaler = 0x10C, //RW High Frequency Peripheral Clock Prescaler Register
            HighFrequencyRadioPeripheralClockPrescaler = 0x110, //RW High Frequency Radio Peripheral Clock Prescaler Register
            HighFrequencyExportClockPrescaler = 0x114, //RW High Frequency Export Clock Prescaler Register
            LowFrequencyAPrescaler = 0x120, //RW Low Frequency a Prescaler Register 0 (Async Reg)
            LowFrequencyBPrescaler = 0x128, //RW Low Frequency B Prescaler Register 0 (Async Reg)
            LowFrequencyEPrescaler = 0x130, //RW Low Frequency E Prescaler Register 0 (Async Reg)
            HighFrequencyAlternateRadioPeripheralClockPrescaler = 0x138, //RW High Frequency Alternate Radio Peripheral Clock Prescaler Register
            SynchronizationBusy = 0x140, //R Synchronization Busy Register
            Freeze = 0x144, //RW Freeze Register
            PCNTControl = 0x150, //RWH PCNT Control Register
            ADCControl = 0x15C, //RWH ADC Control Register
            RoutingPinEnable = 0x170, //RW I/O Routing Pin Enable Register
            RoutingLocation0 = 0x174, //RW I/O Routing Location Register
            RoutingLocation1 = 0x178, //RW I/O Routing Location Register
            ConfigurationLoc = 0x180, //RWH Configuration Lock Register
        }
    }
}
