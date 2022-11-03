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

        public long Size => 0x200;

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
            Control                                             = 0x000,
            HFRCOControl                                        = 0x010,
            HFRCOLDOControl                                     = 0x014,
            AUXHFRCOControl                                     = 0x018,
            AuxiliaryHFRCOLDOControl                            = 0x01C,
            LFRCOControl                                        = 0x020,
            HFXOControl                                         = 0x024,
            HFXOSTARTUPControl                                  = 0x02C,
            HFXOSteadyStateControl                              = 0x030,
            HFXOTimeoutControl                                  = 0x034,
            LFXOControl                                         = 0x038,
            ULFRCOControl                                       = 0x03C,
            DPLLControl                                         = 0x040,
            DPLLControl1                                        = 0x044,
            CalibrationControl                                  = 0x050,
            CalibartionCounter                                  = 0x054,
            OscillatorEnableDisableCommand                      = 0x060,
            Command                                             = 0x064,
            DebugTraceClockSelect                               = 0x070,
            HighFrequencyClockSelectCommand                     = 0x074,
            LowFrequencyAClockSelect                            = 0x080,
            LowFrequencyBClockSelect                            = 0x084,
            LowFrequencyEClockSelect                            = 0x088,
            Status                                              = 0x090,
            HighFrequencyClockStatus                            = 0x094,
            HFXOTrimStatus                                      = 0x09C,
            InterruptFlag                                       = 0x0A0,
            InterruptFlagSet                                    = 0x0A4,
            InterruptFlagClear                                  = 0x0A8,
            InterruptEnable                                     = 0x0AC,
            HighFrequencyBusClockEnable                         = 0x0B0,
            HighFrequencyCoreClockEnable                        = 0x0B8,
            HighFrequencyPeripheralClockEnable                  = 0x0C0,
            HighFrequencyRadioPeripheralClockEnable             = 0x0C8,
            HighFrequencyAlternateRadioPeripheralClockEnable    = 0x0CC,
            HighFrequencyUndividedClockEnable                   = 0x0D0,
            LowFrequencyAClockEnable                            = 0x0E0,
            LowFrequencyBClockEnable                            = 0x0E8,
            LowFrequencyEClockEnable                            = 0x0F0,
            HighFrequencyClockPrescaler                         = 0x100,
            HighFrequencyCoreClockPrescaler                     = 0x108,
            HighFrequencyPeripheralClockPrescaler               = 0x10C,
            HighFrequencyRadioPeripheralClockPrescaler          = 0x110,
            HighFrequencyExportClockPrescaler                   = 0x114,
            LowFrequencyAPrescaler                              = 0x120,
            LowFrequencyBPrescaler                              = 0x128,
            LowFrequencyEPrescaler                              = 0x130,
            HighFrequencyAlternateRadioPeripheralClockPrescaler = 0x138,
            SynchronizationBusy                                 = 0x140,
            Freeze                                              = 0x144,
            PCNTControl                                         = 0x150,
            LVDSControl                                         = 0x158,
            ADCControl                                          = 0x15C,
            RoutingPinEnable                                    = 0x170,
            RoutingLocation0                                    = 0x174,
            RoutingLocation1                                    = 0x178,
            ConfigurationLoc                                    = 0x180,
            HFRCOSpreadSpectrum                                 = 0x184,
            RadioDeFeaturing                                    = 0x188,
            HighFrequencyBusClockLock                           = 0x190,
            HighFrequencyCoreClockLock                          = 0x194,
            HighFrequencyPeripheralClockLock                    = 0x198,
            HighFrequencyRadioPeripheralClockLock               = 0x1A4,
            AlternateRadioPeripheralClockLock                   = 0x1AC,
            HighFrequencyUndividedClockLock                     = 0x1B0,
            LowFrequencyAClockLock                              = 0x1B4,
            LowFrequencyBClockLock                              = 0x1BC,
            LowFrequencyEClockLock                              = 0x1C4,
            PCNTClockLock                                       = 0x1CC,
            Test                                                = 0x1D0,
            HFRCOTestControl                                    = 0x1D4,
            AUXHRCOTestControl                                  = 0x1D8,
            LFRCOTestControl                                    = 0x1DC,
            HFXOTestControl                                     = 0x1E0,
            LFXOTestControl                                     = 0x1E4,
            DPLLOffset                                          = 0x1FC,
        }
    }
}
