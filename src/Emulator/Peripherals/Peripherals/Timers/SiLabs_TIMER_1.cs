//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_TIMER_1
    {
        public SiLabs_TIMER_1(Machine machine, int width) : this(machine)
        {
            if(width < 1 || width > 32)
            {
                this.Log(LogLevel.Error, "Timer width {0} is outside 1..32, using 16", width);
                width = 16;
            }

            // 2^width. The counter maximum cannot exceed this, even if TOP is larger.
            counterCeiling = 1UL << width;
        }

        partial void SiLabs_TIMER_1_Constructor()
        {
            // Frequency and limit are replaced when the timer starts.
            timer = new LimitTimer(machine.ClockSource, 1, this, "timer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnTimerLimitReached;

            channels = new CompareChannel[NumberOfChannels];
            for(uint i = 0; i < NumberOfChannels; i++)
            {
                channels[i] = new CompareChannel(machine, this, i);
            }

            IRQ = new GPIO();
        }

        partial void TIMER_Reset()
        {
            timerIsRunning = false;
            timer.Enabled = false;
            foreach(var channel in channels)
            {
                channel.Reset();
            }

            registers.Reset();
            UpdateInterrupts();
        }

        partial void Cmd_Start_Write(bool a, bool b)
        {
            if(b)
            {
                StartCommand();
            }
        }

        partial void Cmd_Stop_Write(bool a, bool b)
        {
            if(b)
            {
                StopCommand();
            }
        }

        partial void En_En_Write(bool a, bool b)
        {
            if(b)
            {
                return;
            }

            // Clearing EN halts the counter and drops hardware-updated interrupt flags.
            timerIsRunning = false;
            timer.Enabled = false;
            foreach(var channel in channels)
            {
                channel.Reset();
            }
            if_of_bit.Value = false;
            if_uf_bit.Value = false;
            if_cc0_bit.Value = false;
            if_cc1_bit.Value = false;
            if_cc2_bit.Value = false;
            UpdateInterrupts();
        }

        partial void Cfg_Write(uint a, uint b)
        {
            RestartTimer(false);
        }

        partial void Cc_Cfg_Mode_Write(ulong index, CC_CFG_MODE a, CC_CFG_MODE b)
        {
            RestartTimer(false);
        }

        partial void Status_Running_ValueProvider(bool a)
        {
            status_running_bit.Value = timerIsRunning;
        }

        partial void Status_Dir_ValueProvider(STATUS_DIR a)
        {
            status_dir_bit.Value = CountingUp ? STATUS_DIR.UP : STATUS_DIR.DOWN;
        }

        partial void Cnt_Cnt_ValueProvider(ulong a)
        {
            cnt_cnt_field.Value = TimerCounter;
        }

        partial void Cnt_Cnt_Write(ulong a, ulong b)
        {
            timer.Enabled = false;
            cnt_cnt_field.Value = b;
            timer.Value = b;
            if(hostAllClocksGated)
            {
                frozenCounter = (uint)b;
            }
            else if(timerIsRunning)
            {
                timer.Enabled = true;
            }
            ArmCompareChannels((uint)b);
        }

        partial void Cc_Oc_Oc_Write(ulong index, ulong a, ulong b)
        {
            RestartTimer(false);
        }

        partial void If_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        partial void Ien_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        partial void Top_Write(uint a, uint b)
        {
            RestartTimer(false);
        }

        public uint TimerCounter
        {
            get
            {
                if(hostAllClocksGated)
                {
                    return frozenCounter;
                }
                if(timerIsRunning)
                {
                    if(timer.Enabled)
                    {
                        TrySyncTime();
                        return (uint)timer.Value;
                    }
                    else
                    {
                        return (uint)timer.Limit;
                    }
                }
                return 0;
            }

            set
            {
                timer.Value = value;
                RestartTimer(false);
            }
        }

        public GPIO IRQ { get; private set; }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = (ien_of_bit.Value && if_of_bit.Value)
                          || (ien_uf_bit.Value && if_uf_bit.Value)
                          || (ien_cc0_bit.Value && if_cc0_bit.Value)
                          || (ien_cc1_bit.Value && if_cc1_bit.Value)
                          || (ien_cc2_bit.Value && if_cc2_bit.Value);
                IRQ.Set(irq);
            });
        }

        private void StartCommand()
        {
            if(!en_en_bit.Value)
            {
                this.Log(LogLevel.Error, "Timer is disabled, ignoring START");
                return;
            }

            timerIsRunning = true;
            EnsureHostAllClockHooks();
            RestartTimer(true);
        }

        private void StopCommand()
        {
            timerIsRunning = false;
            timer.Enabled = false;
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        private void OnTimerLimitReached()
        {
            // LimitTimer has already reloaded. Up-count reloads to 0; down-count reloads to TOP.
            if(CountingUp)
            {
                if_of_bit.Value = true;
            }
            else
            {
                if_uf_bit.Value = true;
            }

            UpdateInterrupts();

            if(cfg_osmen_bit.Value)
            {
                timerIsRunning = false;
                foreach(var channel in channels)
                {
                    channel.Reset();
                }
                return;
            }

            RestartTimer(true);
        }

        private void RestartTimer(bool restartFromInitial)
        {
            if(!timerIsRunning)
            {
                return;
            }

            if(cfg_mode_field.Value != CFG_MODE.UP && cfg_mode_field.Value != CFG_MODE.DOWN)
            {
                this.Log(LogLevel.Error, "TIMER MODE {0} is not supported", cfg_mode_field.Value);
                timerIsRunning = false;
                timer.Enabled = false;
                return;
            }

            if(cfg_clksel_field.Value != CFG_CLKSEL.PRESCEM01GRPACLK)
            {
                this.Log(LogLevel.Error, "TIMER CLKSEL {0} is not supported", cfg_clksel_field.Value);
                timerIsRunning = false;
                timer.Enabled = false;
                return;
            }

            if(!en_en_bit.Value)
            {
                timerIsRunning = false;
                timer.Enabled = false;
                return;
            }

            ulong top = TopValue();
            uint currentValue = restartFromInitial ? (CountingUp ? 0 : (uint)top) : TimerCounter;

            timer.Enabled = false;
            timer.Frequency = CounterFrequency();
            timer.Direction = CountingUp ? Direction.Ascending : Direction.Descending;
            // Up-count holds every value from 0 through TOP, so the exclusive period is TOP + 1.
            // Down-count reloads to TOP, so the period is TOP.
            timer.Limit = CountingUp ? top + 1 : (top == 0 ? 1 : top);
            this.Log(LogLevel.Noisy, "SET timer limit to {0}", timer.Limit);
            timer.Value = currentValue;
            if(hostAllClocksGated)
            {
                frozenCounter = currentValue;
            }
            else
            {
                timer.Enabled = true;
            }

            ArmCompareChannels(currentValue);
        }

        private void ArmCompareChannels(uint counter)
        {
            ulong top = TopValue();
            foreach(var channel in channels)
            {
                channel.Arm(counter, top, timer.Direction);
            }
        }

        private void SetCompareFlag(uint index)
        {
            switch(index)
            {
            case 0:
                if_cc0_bit.Value = true;
                break;
            case 1:
                if_cc1_bit.Value = true;
                break;
            case 2:
                if_cc2_bit.Value = true;
                break;
            }
            UpdateInterrupts();
        }

        // TOP is the inclusive maximum. A counter of the configured width cannot exceed 2^width - 1.
        private ulong TopValue()
        {
            ulong widthLimit = counterCeiling - 1;
            ulong top = top_top_field.Value;
            return top < widthLimit ? top : widthLimit;
        }

        // TIMERn_CFG.PRESC: the selected clock is divided by PRESC + 1.
        private ulong CounterFrequency()
        {
            if(cmu == null)
            {
                this.Log(LogLevel.Error, "Timer has no CMU; using 1 Hz");
                return 1;
            }

            ulong divisor = (ulong)cfg_presc_field.Value + 1;
            ulong frequency = cmu.HfxoFrequency / divisor;
            if(frequency == 0)
            {
                this.Log(LogLevel.Error, "Prescaled timer clock is 0 Hz; using 1 Hz");
                return 1;
            }
            return frequency;
        }

        private void EnsureHostAllClockHooks()
        {
            if(hostAllClockHooksReady)
            {
                return;
            }
            var emu = machine.GetPeripheralsOfType<ISiLabs_Emu>().FirstOrDefault();
            if(emu == null)
            {
                return;
            }
            emu.AddEnterHostAllClockGateHook(EnterHostAllClockGate);
            emu.AddExitHostAllClockGateHook(ExitHostAllClockGate);
            hostAllClockHooksReady = true;
            hostAllClocksGated = !emu.HostAllClocksEnabled;
        }

        private void EnterHostAllClockGate()
        {
            if(hostAllClocksGated)
            {
                return;
            }
            if(timerIsRunning && timer.Enabled)
            {
                TrySyncTime();
                frozenCounter = (uint)timer.Value;
                timer.Enabled = false;
            }
            hostAllClocksGated = true;
            foreach(var channel in channels)
            {
                channel.SetClockGated(true);
            }
        }

        private void ExitHostAllClockGate()
        {
            if(!hostAllClocksGated)
            {
                return;
            }
            var resume = frozenCounter;
            hostAllClocksGated = false;
            foreach(var channel in channels)
            {
                channel.SetClockGated(false);
            }
            if(!timerIsRunning)
            {
                return;
            }
            timer.Value = resume;
            timer.Enabled = true;
            ArmCompareChannels(resume);
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private bool CountingUp => cfg_mode_field.Value != CFG_MODE.DOWN;

        private LimitTimer timer;
        private CompareChannel[] channels;
        private bool timerIsRunning = false;
        private bool hostAllClockHooksReady;
        private bool hostAllClocksGated;
        private uint frozenCounter;
        private readonly ulong counterCeiling = 1UL << 16;
        private const int NumberOfChannels = 3;

        private class CompareChannel
        {
            public CompareChannel(IMachine machine, SiLabs_TIMER_1 parent, uint index)
            {
                this.parent = parent;
                this.index = index;
                timer = new LimitTimer(machine.ClockSource, 1, parent, $"timer-cc{index}", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                       enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
                timer.LimitReached += OnCompare;
            }

            public void SetClockGated(bool gated)
            {
                if(gated)
                {
                    if(timer.Enabled)
                    {
                        compareRunning = true;
                        timer.Enabled = false;
                    }
                    return;
                }
                if(compareRunning)
                {
                    compareRunning = false;
                    timer.Enabled = true;
                }
            }

            public void Arm(uint value, ulong top, Direction direction)
            {
                timer.Enabled = false;
                if(!parent.timerIsRunning || parent.hostAllClocksGated)
                {
                    return;
                }

                var mode = parent.cc_cfg_mode_field[(int)index].Value;
                if(mode != CC_CFG_MODE.OUTPUTCOMPARE && mode != CC_CFG_MODE.PWM)
                {
                    return;
                }

                ulong compare = parent.cc_oc_oc_field[(int)index].Value;
                if(compare > top)
                {
                    parent.Log(LogLevel.Error, "CC{0}: OC 0x{1:X} is greater than TOP 0x{2:X}, compare will not match", index, compare, top);
                    return;
                }

                ulong delay = 0;
                if(direction == Direction.Ascending && compare > value)
                {
                    delay = compare - value;
                }
                else if(direction == Direction.Descending && compare < value)
                {
                    delay = value - compare;
                }
                if(delay == 0)
                {
                    return;
                }

                timer.Frequency = parent.CounterFrequency();
                timer.Limit = delay;
                timer.Enabled = true;
            }

            public void Reset()
            {
                timer.Enabled = false;
                compareRunning = false;
            }

            private void OnCompare()
            {
                timer.Enabled = false;
                parent.SetCompareFlag(index);
            }

            private bool compareRunning;
            private readonly SiLabs_TIMER_1 parent;
            private readonly uint index;
            private readonly LimitTimer timer;
        }
    }
}
