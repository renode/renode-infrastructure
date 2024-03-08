using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Sysrtc_1
    {
        private const int CounterWidth = 32;
        private const int PreCounterWidth = 15;
        private const int NumberOfCaptureCompareChannels = 3;
        private const int UNLOCKKEY = 0x0000_4776;
        private EFR32_RTCCCounter innerTimer;

        /// <summary>
        /// Generate a SYSRTC model with one interrupt (IRQ)
        /// <param name="frequency">The timebase used for the counter.</param>
        /// </summary>
        public Sysrtc_1(Machine machine, long frequency)
            : base(machine)
        {
            IRQ = new GPIO();

            innerTimer = new EFR32_RTCCCounter(
                machine,
                frequency,
                this,
                "rtcc",
                CounterWidth,
                PreCounterWidth,
                NumberOfCaptureCompareChannels
            );
            innerTimer.Prescaler = 1;
            innerTimer.PreCounter = 1;

            innerTimer.Channels[0].Mode = EFR32_RTCCCounter.CCChannelMode.OutputCompare;
            innerTimer.Channels[0].ComparisonBase = EFR32_RTCCCounter
                .CCChannelComparisonBase
                .Counter;
            innerTimer.Channels[1].Mode = EFR32_RTCCCounter.CCChannelMode.OutputCompare;
            innerTimer.Channels[1].ComparisonBase = EFR32_RTCCCounter
                .CCChannelComparisonBase
                .Counter;
            innerTimer.Channels[2].Mode = EFR32_RTCCCounter.CCChannelMode.InputCapture;
            innerTimer.Channels[2].ComparisonBase = EFR32_RTCCCounter
                .CCChannelComparisonBase
                .Counter;

            innerTimer.LimitReached += delegate
            {
                if_group0_ovf_bit.Value = true;
                UpdateInterrupts();
            };

            for (var idx = 0; idx < NumberOfCaptureCompareChannels; ++idx)
            {
                var i = idx;
                innerTimer.Channels[i].CompareReached += delegate
                {
                    if (i == 0)
                    {
                        if_group0_cmp0_bit.Value = true;
                    }
                    else if (i == 1)
                    {
                        if_group0_cmp1_bit.Value = true;
                    }
                    UpdateInterrupts();
                };
            }

            Define_Registers();
        }

        public GPIO IRQ { get; }

        /// <summary>
        /// Reset the SYSRTC model.
        /// </summary>
        partial void SYSRTC_Reset()
        {
            innerTimer.Reset();
            UpdateInterrupts();
        }

        /// <summary>
        /// Check if the UNLOCKKEY is written to the register, unlock if so.
        /// </summary>
        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (lock_lockkey_field.Value == UNLOCKKEY)
            {
                status_lockstatus_bit.Value = STATUS_LOCKSTATUS.UNLOCKED;
            }
            else
            {
                status_lockstatus_bit.Value = STATUS_LOCKSTATUS.LOCKED;
            }
        }

        /// <summary>
        /// Return current count.
        /// </summary>
        partial void Cnt_Cnt_ValueProvider(ulong a)
        {
            cnt_cnt_field.Value = innerTimer.Counter;
        }

        partial void Cnt_Cnt_Read(ulong a, ulong b) { }

        /// <summary>
        /// Set starting count.
        /// </summary>
        partial void Cnt_Cnt_Write(ulong a, ulong b)
        {
            innerTimer.Counter = b;
        }

        /// <summary>
        /// Start the counter.
        /// </summary>
        partial void Cmd_Start_Write(bool a, bool b)
        {
            if (b)
            {
                innerTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Stop the counter.
        /// </summary>
        partial void Cmd_Stop_Write(bool a, bool b)
        {
            if (b)
            {
                innerTimer.Enabled = false;
            }
        }

        /// <summary>
        /// Check if counter running.
        /// </summary>
        partial void Status_Running_ValueProvider(bool a)
        {
            status_running_bit.Value = innerTimer.Enabled;
        }

        /// <summary>
        /// Update the IF register.
        /// This will force a recomputation of the IRQ line state
        /// </summary>
        partial void If_Group0_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        /// <summary>
        /// Update the IE register.
        /// This will force a recomputation of the IRQ line state
        /// </summary>
        partial void Ien_Group0_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        /// <summary>
        /// Generate a software reset of the model.
        /// </summary>
        partial void Swrst_Swrst_Write(bool a, bool b)
        {
            if (b)
            {
                innerTimer.Enabled = false;
                innerTimer.Reset();
                innerTimer.Channels[0].CompareValue = 0;
                innerTimer.Channels[1].CompareValue = 0;

                ien_group0_ovf_bit.Value = false;
                ien_group0_cmp0_bit.Value = false;
                ien_group0_cmp1_bit.Value = false;
                ien_group0_cap0_bit.Value = false;

                if_group0_ovf_bit.Value = false;
                if_group0_cmp0_bit.Value = false;
                if_group0_cmp1_bit.Value = false;
                if_group0_cap0_bit.Value = false;

                ctrl_group0_cmp0en_bit.Value = false;
                ctrl_group0_cmp1en_bit.Value = false;
                ctrl_group0_cap0en_bit.Value = false;
                ctrl_group0_cmp0cmoa_field.Value = 0;
                ctrl_group0_cmp1cmoa_field.Value = 0;
                ctrl_group0_cap0edge_field.Value = 0;

                cmp0value_group0_cmp0value_field.Value = 0;
                cmp1value_group0_cmp1value_field.Value = 0;
                cap0value_group0_cap0value_field.Value = 0;
            }
        }

        /// <summary>
        /// Set the channel0 compare value.
        /// </summary>
        partial void Cmp0value_Group0_Cmp0value_Write(ulong a, ulong b)
        {
            innerTimer.Channels[0].CompareValue = b;
        }

        partial void Cmp0value_Group0_Cmp0value_Read(ulong a, ulong b) { }

        /// <summary>
        /// Get the channel0 compare value.
        /// </summary>
        partial void Cmp0value_Group0_Cmp0value_ValueProvider(ulong a)
        {
            cmp0value_group0_cmp0value_field.Value = innerTimer.Channels[0].CompareValue;
        }

        /// <summary>
        /// Set the channel1 compare value.
        /// </summary>
        partial void Cmp1value_Group0_Cmp1value_Write(ulong a, ulong b)
        {
            innerTimer.Channels[1].CompareValue = b;
        }

        partial void Cmp1value_Group0_Cmp1value_Read(ulong a, ulong b) { }

        /// <summary>
        /// Get the channel1 compare value.
        /// </summary>
        partial void Cmp1value_Group0_Cmp1value_ValueProvider(ulong a)
        {
            cmp1value_group0_cmp1value_field.Value = innerTimer.Channels[1].CompareValue;
        }

        /// <summary>
        /// Check if an interrupt is active (IF) and if the interrupt request is enable (IE).
        /// </summary>
        private void UpdateInterrupts()
        {
            // Executed in lock as a precaution against the gpio/BaseClockSource deadlock until a proper fix is ready
            machine.ClockSource.ExecuteInLock(
                delegate
                {
                    var value = false;
                    value |= if_group0_ovf_bit.Value && ien_group0_ovf_bit.Value;
                    value |= if_group0_cmp0_bit.Value && ien_group0_cmp0_bit.Value;
                    value |= if_group0_cmp1_bit.Value && ien_group0_cmp1_bit.Value;

                    IRQ.Set(value);
                }
            );
        }
    }
}
