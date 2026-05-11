//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToWord | AllowedTranslation.DoubleWordToWord)]
    public class NXP_IMX_Watchdog : BasicWordPeripheral, IKnownSize
    {
        public NXP_IMX_Watchdog(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            WDOG_RESET_B = new GPIO();

            wdogTimer = new LimitTimer(machine.ClockSource, ReferenceFrequency, this, "imx_wdog",
                limit: MaxTimerTicks, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            wdogTimer.LimitReached += OnWdogTimerElapsed;

            powerDownTimer = new LimitTimer(machine.ClockSource, frequency: 1, this, "imx_wdog_pd",
                limit: PowerDownTimeoutSeconds, workMode: WorkMode.OneShot, eventEnabled: true);
            powerDownTimer.LimitReached += OnPowerDownTimerElapsed;

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            WDOG_RESET_B.Unset();

            wdogTimer.Reset();
            powerDownTimer.Reset();

            wdogPhase = TimerPhase.Idle;
            serviceState = ServiceState.Idle;

            wictWritable = true;

            powerDownTimer.Enabled = true;
        }

        public long Size => 0x10;

        public GPIO IRQ { get; }

        public GPIO WDOG_RESET_B { get; }

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x0030)  // (SRS=1, WDA=1).
                .WithTaggedFlag("WDZST", 0)
                .WithTaggedFlag("WDBG", 1)
                .WithFlag(2, out wdogEnabled, FieldMode.Read | FieldMode.Set, name: "WDE", changeCallback: (_, __) => { ReloadCounters(); })
                .WithFlag(3, out wdogBAssertOnTimeout, name: "WDT")
                .WithFlag(4, out softwareResetSignal, name: "SRS",
                    writeCallback: (_, newVal) =>
                    {
                        if(!newVal)
                        {
                            softwareResetSignal.Value = true;
                            this.InfoLog("SRS cleared, triggering software system reset");
                            machine.RequestReset();
                        }
                    })
                .WithFlag(5, out wdogBAssertion, name: "WDA", changeCallback: (_, newVal) => { if(!newVal) AssertWdogB("WDA bit cleared by software"); })
                .WithTaggedFlag("SRE", 6)
                .WithTaggedFlag("WDW", 7)
                .WithValueField(8, 8, out timeoutValue, name: "WT");

            Registers.Service.Define(this)
                .WithValueField(0, 16, FieldMode.Write, name: "WSR", writeCallback: (_, value) => HandleService((ushort)value));

            Registers.ResetStatus.Define(this)
                .WithTaggedFlag("SFTW", 0)
                .WithTaggedFlag("TOUT", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("POR", 4)
                .WithReservedBits(5, 11);

            Registers.InterruptControl.Define(this, 0x0004)  // WICT = 0x4
                .WithConditionallyWritableValueField(0, 8, out interruptTimeoutValueField, () => wictWritable, name: "WICT", changeCallback: (_, __) => { wictWritable = false; })
                .WithReservedBits(8, 6)
                .WithFlag(14, out interruptStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "WTIS", changeCallback: (_, __) => UpdateInterrupt())
                .WithFlag(15, out interruptEnable, FieldMode.Read | FieldMode.Set, name: "WIE", changeCallback: (_, __) => UpdateInterrupt());

            Registers.MiscControl.Define(this, 0x0001)  // PDE = 0x1
                .WithFlag(0, FieldMode.Read | FieldMode.WriteZeroToClear, name: "PDE",
                    changeCallback: (_, __) =>
                    {
                        powerDownTimer.Enabled = false;
                        this.NoisyLog("Power-down counter disabled");
                    })
                .WithReservedBits(1, 15);
        }

        private void HandleService(ushort value)
        {
            switch(serviceState)
            {
            case ServiceState.Idle:
                if(value == ServiceFirstWord)
                {
                    serviceState = ServiceState.AwaitingSecondWord;
                }
                break;
            case ServiceState.AwaitingSecondWord:
                if(value == ServiceSecondWord)
                {
                    if(wdogEnabled.Value)
                    {
                        ReloadCounters();
                        this.DebugLog("Watchdog serviced");
                    }
                    else
                    {
                        this.WarningLog("Watchdog serviced but WDE is not set - counter not reloaded");
                    }
                    serviceState = ServiceState.Idle;
                }
                else
                {
                    this.DebugLog("Invalid second service word 0x{0:X4}, resetting sequence", value);
                    serviceState = ServiceState.Idle;
                }
                break;
            }
        }

        private void ReloadCounters()
        {
            // Counter counts down from (WT+1)*0.5s; interrupt fires when it reaches WICT*0.5s.
            var timeoutSteps = (long)timeoutValue.Value + 1;
            var interruptSteps = timeoutSteps - (long)interruptTimeoutValueField.Value;

            this.NoisyLog("Reloading: WT={0} (timeout={1}s), WICT={2}, interrupt in {3}s", timeoutValue.Value, timeoutSteps * 0.5, interruptTimeoutValueField.Value, interruptSteps * 0.5);

            interruptStatus.Value = false;
            UpdateInterrupt();

            wdogPhase = TimerPhase.Interrupt;
            ArmTimer(wdogTimer, (ulong)interruptSteps * SubTicksPerHalfSecond);
        }

        private void ArmTimer(LimitTimer timer, ulong limit)
        {
            this.NoisyLog("Arming timer '{0}': limit={1} ticks ({2:F3}s)", timer.LocalName, limit, limit / (double)ReferenceFrequency);
            timer.Enabled = false;
            timer.Limit = limit;
            timer.ResetValue();
            timer.ClearInterrupt();
            timer.Enabled = true;
        }

        private void OnWdogTimerElapsed()
        {
            if(wdogPhase == TimerPhase.Interrupt)
            {
                this.DebugLog("Pre-timeout interrupt fired");
                interruptStatus.Value = true;
                UpdateInterrupt();

                wdogPhase = TimerPhase.Timeout;
                var remainingTicks = (ulong)interruptTimeoutValueField.Value * SubTicksPerHalfSecond;

                // The duration between the interrupt and timeout events can be
                // programmed to 0 seconds - trigger the timeout phase (almost) immediately.
                // A single tick is left so the CPU can still enter the ISR before reset.
                if(remainingTicks == 0)
                {
                    remainingTicks = 1;
                }

                this.NoisyLog("Interrupt fired, re-arming for timeout in {0} ticks ({1:F3}s), WICT={2}", remainingTicks, remainingTicks / (double)ReferenceFrequency, interruptTimeoutValueField.Value);
                ArmTimer(wdogTimer, remainingTicks);
                return;
            }

            this.InfoLog("Watchdog timed out, requesting machine reset");
            interruptStatus.Value = true;
            UpdateInterrupt();
            if(wdogBAssertOnTimeout.Value)
            {
                AssertWdogB("Watchdog timeout with WDT set");
            }
            machine.RequestReset();
        }

        private void OnPowerDownTimerElapsed()
        {
            AssertWdogB("Power-down counter expired");
        }

        private void AssertWdogB(string reason)
        {
            wdogBAssertion.Value = false;
            if(WDOG_RESET_B.IsSet)
            {
                return;
            }
            WDOG_RESET_B.Set(true);
            this.DebugLog("WDOG_B asserted: {0}", reason);
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptStatus.Value && interruptEnable.Value);
        }

        private IFlagRegisterField wdogEnabled;
        private IFlagRegisterField wdogBAssertion;
        private IFlagRegisterField wdogBAssertOnTimeout;
        private IFlagRegisterField softwareResetSignal;
        private IFlagRegisterField interruptStatus;
        private IFlagRegisterField interruptEnable;
        private IValueRegisterField timeoutValue;
        private IValueRegisterField interruptTimeoutValueField;

        private bool wictWritable;

        private ServiceState serviceState;
        private TimerPhase wdogPhase;

        private readonly LimitTimer wdogTimer;
        private readonly LimitTimer powerDownTimer;

        private const ulong ReferenceFrequency = 32768;
        private const ulong SubTicksPerHalfSecond = ReferenceFrequency / 2;
        private const ulong MaxTimerTicks = 256 * SubTicksPerHalfSecond;
        private const ulong PowerDownTimeoutSeconds = 16;
        private const ushort ServiceFirstWord = 0x5555;
        private const ushort ServiceSecondWord = 0xAAAA;

        private enum ServiceState
        {
            Idle,
            AwaitingSecondWord,
        }

        private enum TimerPhase
        {
            Idle,
            Interrupt,
            Timeout,
        }

        private enum Registers : long
        {
            Control          = 0x0,
            Service          = 0x2,
            ResetStatus      = 0x4,
            InterruptControl = 0x6,
            MiscControl      = 0x8,
        }
    }
}
