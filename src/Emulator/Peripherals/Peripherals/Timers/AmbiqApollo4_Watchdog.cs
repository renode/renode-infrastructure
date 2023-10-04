//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class AmbiqApollo4_Watchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_Watchdog(IMachine machine) : base(machine)
        {
            resetTimer = new LimitTimer(machine.ClockSource, 1, this, "Reset", enabled: false, eventEnabled: true, direction: Direction.Ascending, workMode: WorkMode.OneShot);
            resetTimer.LimitReached += () =>
            {
                if(resetEnabled.Value)
                {
                    this.Log(LogLevel.Warning, "Watchog reset triggered");
                    machine.RequestReset();
                }
            };

            DefineRegisters();
        }

        private void DefineRegisters()
        {
            Registers.Configuration.Define(this, 0xFFFF00)
                .WithFlag(0, out var timerEnabled, name: "WDTEN")
                .WithTaggedFlag("INTEN", 1)
                .WithFlag(2, out resetEnabled, name: "RESEN")
                .WithTaggedFlag("DSPRESETINTEN", 3)
                .WithReservedBits(4, 4)
                .WithValueField(8, 8, out var resetLimit, name: "RESVAL")
                .WithValueField(16, 8, out var interruptLimit, name: "INTVAL")
                .WithEnumField<DoubleWordRegister, ClockSelect>(24, 3, out var clockSelect, name: "CLKSEL")
                .WithReservedBits(27, 5)
                .WithChangeCallback((_, __) =>
                    {
                        var enableTimers = timerEnabled.Value;
                        long frequency = 1;

                        switch(clockSelect.Value)
                        {
                            case ClockSelect.Off:
                                enableTimers = false;
                                break;
                            case ClockSelect._128Hz:
                                frequency = 128 * FrequencyMultiplier;
                                break;
                            case ClockSelect._16Hz:
                                frequency = 16 * FrequencyMultiplier;
                                break;
                            case ClockSelect._1Hz:
                                frequency = 1 * FrequencyMultiplier;
                                break;
                            case ClockSelect._1_16Hz:
                                frequency = FrequencyMultiplier / 16;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Invalid frequency value: {0}. Timer will be disabled", clockSelect.Value);
                                enableTimers = false;
                                break;
                        }

                        resetTimer.Frequency = frequency;
                        resetTimer.Limit = resetLimit.Value * FrequencyMultiplier;
                        resetTimer.Enabled = enableTimers && resetEnabled.Value;
                    });

            Registers.Restart.Define(this)
                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if(value == WatchdogReloadValue)
                        {
                            resetTimer.ResetValue();
                        }
                    });

            Registers.CounterValue.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "COUNT", valueProviderCallback: _ => TimerValue);
        }

        private ulong TimerValue
        {
            get
            {
                if(sysbus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                return resetTimer.Value / FrequencyMultiplier;
            }
        }

        public long Size => 0x400;

        private IFlagRegisterField resetEnabled;

        private readonly LimitTimer resetTimer;

        private const ulong WatchdogReloadValue = 0xB2;
        private const int FrequencyMultiplier = 16;

        private enum ClockSelect
        {
            Off = 0x0,
            _128Hz = 0x1,
            _16Hz = 0x2,
            _1Hz = 0x3,
            _1_16Hz = 0x4,
        }

        private enum Registers
        {
            Configuration       = 0x0,      // CFG
            Restart             = 0x4,      // RSTRT
            Lock                = 0x8,      // LOCK
            CounterValue        = 0xC,      // COUNT
            DSP0Configuration   = 0x10,     // DPS0CFG
            DSP0Restart         = 0x14,     // DSP0RSTRT
            DSP0Lock            = 0x18,     // DSP0LOCK
            DSP0CounterValue    = 0x1C,     // DSP1COUNT
            DSP1Configuration   = 0x20,     // DPS1CFG
            DSP1Restart         = 0x24,     // DSP1RSTRT
            DSP1Lock            = 0x28,     // DSP1LOCK
            DSP1CounterValue    = 0x2C,     // DSP1COUNT
            InterruptEnable     = 0x200,    // WDTIEREN
            InterruptStatus     = 0x204,    // WDTIERSTAT
            InterruptClear      = 0x208,    // WDTIERCLR
            InterruptSet        = 0x20C,    // WDTIERSET
            DSP0InterruptEnable = 0x210,    // DSP0IEREN
            DSP0InterruptStatus = 0x214,    // DSP0IERSTAT
            DSP0InterruptClear  = 0x218,    // DSP0IERCLR
            DSP0InterruptSet    = 0x21C,    // DSP0IERSET
            DSP1InterruptEnable = 0x220,    // DSP1IEREN
            DSP1InterruptStatus = 0x224,    // DSP1IERSTAT
            DSP1InterruptClear  = 0x228,    // DSP1IERCLR
            DSP1InterruptSet    = 0x22C,    // DSP1IERSET
        }
    }
}
