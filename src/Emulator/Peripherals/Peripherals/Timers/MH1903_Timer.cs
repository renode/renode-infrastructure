//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    /// <summary>
    /// MH1903 Timer Block - Contains all 8 timers
    ///
    /// This peripheral implements the complete MH1903 timer block with 8 independent timers.
    /// Each timer counts down from LoadCount to 0.
    ///
    /// Register Layout (base 0x40013000):
    ///   0x00-0x0F: Timer0 (LoadCount, CurrentValue, ControlReg, EOI, IntStatus)
    ///   0x14-0x23: Timer1 (same registers)
    ///   0x28-0x37: Timer2 (same registers)
    ///   0x3C-0x4B: Timer3 (same registers)
    ///   0x50-0x5F: Timer4 (same registers)
    ///   0x64-0x73: Timer5 (same registers)
    ///   0x78-0x87: Timer6 (same registers)
    ///   0x8C-0x9B: Timer7 (same registers)
    ///   0xA0: TimersIntStatus - Combined interrupt status for all timers
    ///   0xA4: TimersEOI - Global End of Interrupt (clears all interrupts)
    ///   0xA8: TimersRawIntStatus - Raw interrupt status (before masking)
    ///   0xB0-0xCC: Timer0-7 LoadCount2 registers (for PWM mode)
    ///
    /// Each timer operates in count-down mode:
    /// - When enabled, CurrentValue starts at LoadCount (or 0xFFFFFFFF in free-running)
    /// - Decrements on each timer tick
    /// - When reaching 0, triggers interrupt if not masked
    /// - In free-running mode (mode=0): counts from 0xFFFFFFFF and reloads 0xFFFFFFFF
    /// - In user-defined mode (mode=1): counts from LoadCount and reloads LoadCount
    ///
    /// PWM Mode:
    /// - When PWM bit is set, timer alternates between LoadCount and LoadCount2
    /// - LoadCount sets LOW period: (LoadCount + 1) * PCLK_Period
    /// - LoadCount2 sets HIGH period: (LoadCount2 + 1) * PCLK_Period
    /// </summary>
    public class MH1903_Timer : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public MH1903_Timer(IMachine machine, ulong frequency = 72000000) : base(machine)
        {
            var irqs = new Dictionary<int, IGPIO>();
            timers = new TimerUnit[8];

            for(int i = 0; i < 8; i++)
            {
                irqs[i] = new GPIO();
                var timerIndex = i; // Capture for lambda
                timers[i] = new TimerUnit(machine, frequency, this, i, irqs[i], () => UpdateInterrupt(timerIndex));
            }

            Connections = irqs;
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var timer in timers)
            {
                timer.Reset();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            // Define registers for each of the 8 timers
            for(int i = 0; i < 8; i++)
            {
                var timerIndex = i;
                var baseOffset = i * 0x14; // Each timer block is 0x14 bytes apart

                // LoadCount register (offset 0x00 from timer base)
                ((Registers)(baseOffset + 0x00)).Define(this)
                    .WithValueField(0, 32, name: $"Timer{i}LoadCount",
                        writeCallback: (_, value) =>
                        {
                            timers[timerIndex].LoadCount = (uint)value;
                            this.Log(LogLevel.Noisy, "Timer{0}: LoadCount = 0x{1:X}", timerIndex, value);
                        },
                        valueProviderCallback: _ => timers[timerIndex].LoadCount);

                // CurrentValue register (offset 0x04 from timer base)
                ((Registers)(baseOffset + 0x04)).Define(this)
                    .WithValueField(0, 32, FieldMode.Read, name: $"Timer{i}CurrentValue",
                        valueProviderCallback: _ => timers[timerIndex].GetCurrentValue());

                // ControlReg register (offset 0x08 from timer base)
                ((Registers)(baseOffset + 0x08)).Define(this)
                    .WithFlag(0, name: $"Timer{i}_Enable",
                        writeCallback: (_, value) => timers[timerIndex].Enabled = value,
                        valueProviderCallback: _ => timers[timerIndex].Enabled)
                    .WithFlag(1, name: $"Timer{i}_Mode",
                        writeCallback: (_, value) => timers[timerIndex].Mode = value,
                        valueProviderCallback: _ => timers[timerIndex].Mode)
                    .WithFlag(2, name: $"Timer{i}_IntMask",
                        writeCallback: (_, value) => timers[timerIndex].InterruptMask = value,
                        valueProviderCallback: _ => timers[timerIndex].InterruptMask)
                    .WithFlag(3, name: $"Timer{i}_PWM",
                        writeCallback: (_, value) => timers[timerIndex].PwmMode = value,
                        valueProviderCallback: _ => timers[timerIndex].PwmMode)
                    .WithFlag(4, name: $"Timer{i}_PWM_Oneshot",
                        writeCallback: (_, value) => timers[timerIndex].PwmOneshot = value,
                        valueProviderCallback: _ => timers[timerIndex].PwmOneshot)
                    .WithFlag(5, name: $"Timer{i}_TIM_Reload",
                        writeCallback: (_, value) => timers[timerIndex].TimReload = value,
                        valueProviderCallback: _ => timers[timerIndex].TimReload)
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, value) =>
                    {
                        this.Log(LogLevel.Noisy, "Timer{0}: ControlReg written = 0x{1:X} (Enable={2}, Mode={3}, IntMask={4}, PWM={5}, PWM_Oneshot={6}, TIM_Reload={7})",
                            timerIndex, value,
                            timers[timerIndex].Enabled,
                            timers[timerIndex].Mode,
                            timers[timerIndex].InterruptMask,
                            timers[timerIndex].PwmMode,
                            timers[timerIndex].PwmOneshot,
                            timers[timerIndex].TimReload);
                    });

                // EOI register (offset 0x0C from timer base)
                ((Registers)(baseOffset + 0x0C)).Define(this)
                    .WithValueField(0, 32, FieldMode.Read, name: $"Timer{i}EOI",
                        valueProviderCallback: _ =>
                        {
                            var status = timers[timerIndex].InterruptStatus ? 1u : 0u;
                            this.Log(LogLevel.Noisy, "Timer{0}: EOI read, status was {1}, clearing interrupt", timerIndex, status);
                            timers[timerIndex].ClearInterrupt();
                            return status;
                        });

                // IntStatus register (offset 0x10 from timer base)
                ((Registers)(baseOffset + 0x10)).Define(this)
                    .WithFlag(0, FieldMode.Read, name: $"Timer{i}IntStatus",
                        valueProviderCallback: _ => timers[timerIndex].InterruptStatus)
                    .WithReservedBits(1, 31);
            }

            // Global registers
            Registers.TimersIntStatus.Define(this)
                .WithFlags(0, 8, FieldMode.Read, name: "TimersIntStatus",
                    valueProviderCallback: (i, _) => timers[i].InterruptStatus && !timers[i].InterruptMask)
                .WithReservedBits(8, 24);

            Registers.TimersEOI.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TimersEOI",
                    valueProviderCallback: _ =>
                    {
                        uint status = 0;
                        for(int i = 0; i < 8; i++)
                        {
                            if(timers[i].InterruptStatus)
                            {
                                status |= (1u << i);
                                timers[i].ClearInterrupt();
                            }
                        }
                        return status;
                    });

            Registers.TimersRawIntStatus.Define(this)
                .WithFlags(0, 8, FieldMode.Read, name: "TimersRawIntStatus",
                    valueProviderCallback: (i, _) => timers[i].InterruptStatus)
                .WithReservedBits(8, 24);

            // LoadCount2 registers (0xB0-0xCC, 4 bytes apart)
            for(var i = 0; i < 8; i++)
            {
                var timerIndex = i;
                ((Registers)(0xB0 + i * 4)).Define(this)
                    .WithValueField(0, 32, name: $"Timer{i}LoadCount2",
                        writeCallback: (_, value) =>
                        {
                            timers[timerIndex].LoadCount2 = (uint)value;
                            this.Log(LogLevel.Noisy, "Timer{0}: LoadCount2 = 0x{1:X}", timerIndex, value);
                        },
                        valueProviderCallback: _ => timers[timerIndex].LoadCount2);
            }
        }

        private void UpdateInterrupt(int timerIndex)
        {
            var timer = timers[timerIndex];
            var irqState = timer.InterruptStatus && !timer.InterruptMask;
            this.Log(LogLevel.Noisy, "Timer{0}: UpdateInterrupt - intStatus={1}, intMask={2}, irqState={3}",
                timerIndex, timer.InterruptStatus, timer.InterruptMask, irqState);
            timer.IRQ.Set(irqState);
        }

        private readonly TimerUnit[] timers;

        private class TimerUnit
        {
            public TimerUnit(IMachine machine, ulong frequency, MH1903_Timer parent, int index, IGPIO irq, Action onInterruptChanged)
            {
                this.parent = parent;
                this.index = index;
                this.IRQ = irq;
                this.onInterruptChanged = onInterruptChanged;

                innerTimer = new LimitTimer(
                    machine.ClockSource,
                    frequency,
                    parent,
                    $"timer{index}",
                    0xFFFFFFFF,
                    Direction.Ascending,
                    eventEnabled: true,
                    autoUpdate: true
                );

                innerTimer.LimitReached += OnLimitReached;
            }

            public void Reset()
            {
                innerTimer.Reset();
                loadCount = 0x00000000; // Reset value from table
                loadCount2 = 0x00000000;
                interruptStatus = false;
                pwmCurrentHigh = false;
                enabled = false;
                mode = false;
                interruptMask = false;
                pwmMode = false;
                pwmOneshot = false;
                timReload = false;
                onInterruptChanged?.Invoke();
            }

            public uint GetCurrentValue()
            {
                if(!innerTimer.Enabled)
                {
                    return loadCount;
                }

                // Sync time before reading
                if(parent.machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }

                return loadCount - (uint)innerTimer.Value;
            }

            public void ClearInterrupt()
            {
                interruptStatus = false;
                parent.Log(LogLevel.Noisy, "Timer{0}: interrupt cleared, calling UpdateInterrupt", index);
                onInterruptChanged?.Invoke();
            }

            public IGPIO IRQ { get; }

            public uint LoadCount
            {
                get => loadCount;
                set
                {
                    loadCount = value;
                    innerTimer.Limit = loadCount;
                    parent.Log(LogLevel.Noisy, "Timer{0}: LoadCount set to 0x{1:X}", index, loadCount);
                    if(innerTimer.Enabled)
                    {
                        innerTimer.Value = 0;
                    }
                }
            }

            public uint LoadCount2
            {
                get => loadCount2;
                set
                {
                    loadCount2 = value;
                    parent.Log(LogLevel.Noisy, "Timer{0}: LoadCount2 set to 0x{1:X}", index, loadCount2);
                }
            }

            public bool Enabled
            {
                get => enabled;
                set
                {
                    if(value && !enabled)
                    {
                        // Clear interrupt status when starting timer
                        interruptStatus = false;
                        pwmCurrentHigh = false;
                        innerTimer.Value = 0;
                        innerTimer.Limit = loadCount;
                        innerTimer.Enabled = true;
                        onInterruptChanged?.Invoke();
                        parent.Log(LogLevel.Noisy, "Timer{0}: enabled, counting down from 0x{1:X}", index, loadCount);
                    }
                    else if(!value && enabled)
                    {
                        innerTimer.Enabled = false;
                        parent.Log(LogLevel.Noisy, "Timer{0}: disabled", index);
                    }
                    enabled = value;
                }
            }

            public bool Mode
            {
                get => mode;
                set
                {
                    mode = value;
                    parent.Log(LogLevel.Noisy, "Timer{0}: mode set to {1}", index, value ? "user-defined" : "free-running");
                }
            }

            public bool InterruptMask
            {
                get => interruptMask;
                set
                {
                    interruptMask = value;
                    parent.Log(LogLevel.Debug, "Timer{0}: interrupt mask set to {1}", index, value ? "masked" : "unmasked");
                    onInterruptChanged?.Invoke();
                }
            }

            public bool PwmMode
            {
                get => pwmMode;
                set
                {
                    pwmMode = value;
                    parent.Log(LogLevel.Debug, "Timer{0}: PWM mode {1}", index, value ? "enabled" : "disabled");
                }
            }

            public bool PwmOneshot
            {
                get => pwmOneshot;
                set
                {
                    pwmOneshot = value;
                    parent.Log(LogLevel.Debug, "Timer{0}: PWM oneshot {1}", index, value ? "enabled" : "disabled");
                }
            }

            public bool TimReload
            {
                get => timReload;
                set
                {
                    timReload = value;
                    parent.Log(LogLevel.Debug, "Timer{0}: TIM_Reload {1}", index, value ? "enabled" : "disabled");
                }
            }

            public bool InterruptStatus => interruptStatus;

            private void OnLimitReached()
            {
                parent.Log(LogLevel.Noisy, "Timer{0}: reached 0", index);

                interruptStatus = true;
                onInterruptChanged?.Invoke();

                // Handle PWM mode
                if(pwmMode)
                {
                    if(pwmCurrentHigh)
                    {
                        // Was in HIGH period, now go to LOW
                        pwmCurrentHigh = false;
                        innerTimer.Limit = loadCount;
                        innerTimer.Value = 0;
                        parent.Log(LogLevel.Noisy, "Timer{0}: PWM switching to LOW period (LoadCount=0x{1:X})", index, loadCount);
                    }
                    else
                    {
                        // Was in LOW period, now go to HIGH
                        pwmCurrentHigh = true;
                        innerTimer.Limit = loadCount2;
                        innerTimer.Value = 0;
                        parent.Log(LogLevel.Noisy, "Timer{0}: PWM switching to HIGH period (LoadCount2=0x{1:X})", index, loadCount2);
                    }

                    // Check PWM oneshot mode
                    if(pwmOneshot && pwmCurrentHigh)
                    {
                        innerTimer.Enabled = false;
                        enabled = false;
                        parent.Log(LogLevel.Noisy, "Timer{0}: PWM oneshot completed", index);
                    }
                }
                // Check mode: mode=0 is free-running, mode=1 is user-defined
                else if(!mode)
                {
                    // Free-running mode (mode=0): reload from 0xFFFFFFFF
                    innerTimer.Limit = 0xFFFFFFFF;
                    innerTimer.Value = 0;
                    parent.Log(LogLevel.Noisy, "Timer{0}: free-running mode, reloading from 0xFFFFFFFF", index);
                }
                else
                {
                    // User-defined mode (mode=1): always reload from LoadCount
                    innerTimer.Value = 0;
                    innerTimer.Limit = loadCount;
                    parent.Log(LogLevel.Noisy, "Timer{0}: user-defined mode, reloading from LoadCount 0x{1:X}", index, loadCount);
                }
            }

            private uint loadCount;
            private uint loadCount2;
            private bool interruptStatus;
            private bool pwmCurrentHigh;
            private bool enabled;
            private bool mode;
            private bool interruptMask;
            private bool pwmMode;
            private bool pwmOneshot;
            private bool timReload;

            private readonly LimitTimer innerTimer;
            private readonly MH1903_Timer parent;
            private readonly int index;
            private readonly Action onInterruptChanged;
        }

        private enum Registers : long
        {
            // Timer 0-7 base offsets (each timer occupies 0x14 bytes, but only uses 5 registers)
            Timer0Base = 0x00,
            Timer1Base = 0x14,
            Timer2Base = 0x28,
            Timer3Base = 0x3C,
            Timer4Base = 0x50,
            Timer5Base = 0x64,
            Timer6Base = 0x78,
            Timer7Base = 0x8C,

            // Global registers
            TimersIntStatus = 0xA0,
            TimersEOI = 0xA4,
            TimersRawIntStatus = 0xA8,

            // LoadCount2 registers (0xB0-0xCC, 4 bytes apart)
            Timer0LoadCount2 = 0xB0,
            Timer1LoadCount2 = 0xB4,
            Timer2LoadCount2 = 0xB8,
            Timer3LoadCount2 = 0xBC,
            Timer4LoadCount2 = 0xC0,
            Timer5LoadCount2 = 0xC4,
            Timer6LoadCount2 = 0xC8,
            Timer7LoadCount2 = 0xCC,
        }
    }
}
