//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class RenesasRZG_GTM : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasRZG_GTM(IMachine machine, long frequency) : base(machine)
        {
            timer = new LimitTimer(machine.ClockSource, frequency, this, "timer", FreeRunLimit, Direction.Descending, workMode: WorkMode.Periodic, eventEnabled: true);
            timer.LimitReached += HandleLimitReached;
            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            timer.Reset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x24;

        private void DefineRegisters()
        {
            Registers.Compare.Define(this)
                .WithValueField(0, 32, out newTimerLimit, name: "OSTMnCMP",
                    writeCallback: (_, value) =>
                    {
                        if(operatingMode.Value == OperatingMode.FreeRunning && value >= TimerValue)
                        {
                            // Limit is only updated if the new compare
                            // value can be reached before the roll-over
                            SetTimerLimit(value);
                        }
                    });

            Registers.Counter.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "OSTMnCNT",
                    valueProviderCallback: _ => TimerValue);

            Registers.CountEnableStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "OSTMnTE",
                    valueProviderCallback: _ => timer.Enabled)
                .WithReservedBits(1, 31);

            Registers.CountStartTrigger.Define(this)
                .WithFlag(0, FieldMode.Write, name: "OSTMnTS",
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        SetTimerLimit(newTimerLimit.Value);
                        if(timer.Enabled)
                        {
                            // Restart
                            if(operatingMode.Value == OperatingMode.Interval)
                            {
                                timer.ResetValue();
                                UpdateInterrupts(countingStart: true);
                            }
                        }
                        else
                        {
                            // Regular start
                            timer.ResetValue();
                            timer.Enabled = true;
                            UpdateInterrupts(countingStart: true);
                        }
                    })
                .WithReservedBits(1, 31);

            Registers.CountStopTrigger.Define(this)
                .WithFlag(0, FieldMode.Write, name: "OSTMnTT",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            timer.Enabled = false;
                        }
                    })
                .WithReservedBits(1, 31);

            Registers.Control.Define(this)
                .WithFlag(0, out interruptWhenCountingStarts, name: "OSTMnMD0")
                .WithEnumField(1, 1, out operatingMode, name: "OSTMnMD1",
                    changeCallback: (_, newMode) =>
                    {
                        switch(newMode)
                        {
                            case OperatingMode.Interval:
                                timer.Direction = Direction.Descending;
                                break;
                            case OperatingMode.FreeRunning:
                                timer.Direction = Direction.Ascending;
                                break;
                            default:
                                throw new Exception("Unreachable");
                        }
                    })
                .WithReservedBits(2, 30);
        }

        private void UpdateInterrupts(bool countingStart = false)
        {
            if(countingStart && !interruptWhenCountingStarts.Value)
            {
                return;
            }

            IRQ.Blink();
            this.DebugLog("IRQ triggered");
        }

        private void SetTimerLimit(ulong value)
        {
            value++;
            if(timer.Limit != value)
            {
                timer.Limit = value;
            }
        }

        private void HandleLimitReached()
        {
            switch(operatingMode.Value)
            {
                case OperatingMode.Interval:
                    SetTimerLimit(newTimerLimit.Value);
                    break;
                case OperatingMode.FreeRunning:
                {
                    var currentValue = timer.Value;
                    // Handle roll-over
                    if(currentValue == FreeRunLimit)
                    {
                        SetTimerLimit(newTimerLimit.Value);
                        return;
                    }
                    else
                    {
                        SetTimerLimit(FreeRunLimit);
                        timer.Value = currentValue;
                        // fallthrough to trigger IRQ
                    }
                    break;
                }
                default:
                    throw new Exception("Unreachable");
            }
            UpdateInterrupts();
        }

        private ulong TimerValue
        {
            get
            {
                if(timer.Enabled && sysbus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                return timer.Value;
            }
        }

        private IEnumRegisterField<OperatingMode> operatingMode;
        private IFlagRegisterField interruptWhenCountingStarts;
        private IValueRegisterField newTimerLimit;

        private readonly LimitTimer timer;

        private const ulong FreeRunLimit = (1UL << 32) - 1;

        private enum OperatingMode
        {
            Interval    = 0,
            FreeRunning = 1,
        }

        private enum Registers
        {
            Compare           = 0x00, // OSTMnCMP
            Counter           = 0x04, // OSTMnCNT
            CountEnableStatus = 0x10, // OSTMnTE
            CountStartTrigger = 0x14, // OSTMnTS
            CountStopTrigger  = 0x18, // OSTMnTT
            Control           = 0x20, // OSTMnCTL
        }
    }
}
