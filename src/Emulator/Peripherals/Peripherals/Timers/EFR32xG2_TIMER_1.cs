//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class EFR32xG2_TIMER_1
    {
        public EFR32xG2_TIMER_1(Machine machine, uint frequency, int width) : this(machine)
        {
            this.timerFrequency = frequency;
            this.timerLimit = (uint)(1 << width) - 1;   // setting timerLimit to max possible value of the given bit width
            timer.Frequency = timerFrequency;
            timer.Limit = timerLimit;
        }
        partial void EFR32xG2_TIMER_1_Constructor()
        {
            // frequency and limit set to arbitrary values as the will be overwritten by the constructor defined above
            timer = new LimitTimer(machine.ClockSource, 1, this, "timer", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnTimerLimitReached;

            IRQ = new GPIO();
        }

        partial void TIMER_Reset()
        {
            timerIsRunning = false;
            timer.Enabled = false;
        }

        partial void Cmd_Start_Write(bool a, bool b)
        {
            if (b)
            {
                StartCommand();
            }
        }

        partial void Cmd_Stop_Write(bool a, bool b)
        {
            if (b)
            {
                StopCommand();
            }
        }

        partial void Cnt_Cnt_ValueProvider(ulong a)
        {
            cnt_cnt_field.Value = TimerCounter;
        }

        partial void Cnt_Cnt_Write(ulong a, ulong b)
        {
            // Update the count variables when the timer is disabled
            timer.Enabled = false;
            cnt_cnt_field.Value = b;
            timer.Value = b;
            timer.Enabled = true;
        }

        partial void Cc_Oc_Oc_Write(ulong index, ulong a, ulong b)
        {
            // Function call to check whether the timer limit needs to be reset
            RestartTimer();
        }

        partial void If_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        partial void Ien_Write(uint a, uint b)
        {
            UpdateInterrupts();

            // Function call to check whether the timer limit needs to be reset
            RestartTimer();
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((ien_of_bit.Value && if_of_bit.Value)
                            || (ien_cc0_bit.Value && if_cc0_bit.Value));
                IRQ.Set(irq);
            });
        }

        private void StartCommand()
        {
            timerIsRunning = true;
            RestartTimer(true);
        }

        private void StopCommand()
        {
            timerIsRunning = false;
            timer.Enabled = false;
        }


        private void OnTimerLimitReached()
        {
            this.Log(LogLevel.Debug, "Timer Limit Reached");
            bool restartFromZero = false;

            if (timer.Limit == timerLimit)
            {
                if_of_bit.Value = true;
                restartFromZero = true;
            }
            if (timer.Limit == cc_oc_oc_field[0].Value + 1)
            {
                if_cc0_bit.Value = true;
            }

            UpdateInterrupts();
            RestartTimer(restartFromZero);
        }

        private void RestartTimer(bool restartFromZero = false)
        {
            if (!timerIsRunning)
            {
                return;
            }

            uint currentValue = restartFromZero ? 0 : TimerCounter;

            timer.Enabled = false;
            uint limit = timerLimit;

            // Check whether the limit should be set to a compare value
            if (ien_cc0_bit.Value 
                && currentValue < (cc_oc_oc_field[0].Value + 1)
                && (cc_oc_oc_field[0].Value + 1) < limit)
            {
                limit = (uint)cc_oc_oc_field[0].Value + 1;
            }

            // Add remaining compare channels as needed
            
            timer.Limit = limit;
            this.Log(LogLevel.Noisy, "SET timer limit to {0}", timer.Limit);
            timer.Enabled = true;
            timer.Value = currentValue;
        }

        public uint TimerCounter
        {
            get
            {
                if (timerIsRunning)
                {
                    if (timer.Enabled)
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
                RestartTimer();
            }
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

        public GPIO IRQ { get; private set;}
        private LimitTimer timer;
        private uint timerFrequency;
        private uint timerLimit;
        public bool timerIsRunning = false;
    }
}