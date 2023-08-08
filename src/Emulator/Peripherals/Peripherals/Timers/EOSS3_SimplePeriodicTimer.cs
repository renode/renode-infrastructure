//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EOSS3_SimplePeriodicTimer : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public EOSS3_SimplePeriodicTimer(IMachine machine) : base(machine)
        {
            interruptTimestamps = new ushort[NumberOfInterrupts];
            FFEKickOff = new GPIO();

            timerSoftware30Bit = new LimitTimer(machine.ClockSource, 1000 /*count every 1ms*/, this, "Software Use Timer", limit: 0x3FFFFFFF, enabled: false, eventEnabled: false, direction: Time.Direction.Ascending);
            timerFFEKickOffUpCounter = new LimitTimer(machine.ClockSource, 1000 /*count every 1ms*/, this, "FFE Kick-Off", limit: 0x4 /* the minimal legal limit, as LimitTimer does not accept 0 */,
                enabled: false, eventEnabled: true, direction: Time.Direction.Ascending);

            timerFFEKickOffUpCounter.LimitReached += delegate
            {
                if(sleepMode.Value)
                {
                    this.Log(LogLevel.Noisy, "FFE Kick-Off is masked");
                }
                else
                {
                    FFEKickOff.Blink();
                    this.Log(LogLevel.Noisy, "FFE Kick-Off!");
                }
            };
            DefineRegisters();
        }

        public override void Reset()
        {
            enabled = false;
            ffeKickOffPeriod = 0;
            FFEKickOff.Unset();
            interruptTimestamps = new ushort[NumberOfInterrupts];

            timerFFEKickOffUpCounter.Reset();
            timerSoftware30Bit.Reset();

            base.Reset();
        }

        /*
        Saving timestamps for FFE.
        */
        public void OnGPIO(int number, bool value)
        {
            if(number >= 8)
            {
                this.Log(LogLevel.Warning, "Invalid interrupt number: {0} set to {1}", number, value);
                return;
            }
            if(!value)
            {
                //we don't have to filter out repeated calls with `true`, as the GPIO class does it by itself
                return;
            }

            if(((int)interruptMask.Value & (1 << number)) != 0x0) //interrupt is not masked
            {
                sleepMode.Value = false;
                interruptTimestamps[number] = (ushort)timerSoftware30Bit.Value; //only lower bits are latched

                this.Log(LogLevel.Noisy, "Saved timestamp for interrupt {0}.", number);
            }
            else
            {
                this.Log(LogLevel.Warning, "The interrupt {0} is masked: 0x{1:X}.", number, interruptMask.Value);
            }
        }

        //This is currently not used - exposed for debug purposes.
        public ushort GetInterruptTimestamp(uint number)
        {
            if(number >= 8)
            {
                this.Log(LogLevel.Warning, "No such interrupt: {0}", number);
                return 0;
            }
            return interruptTimestamps[number];
        }

        public long Size => 0x200;

        private void DefineRegisters()
        {
            /*
                Ignoring source frequency and all compensation registers,
                as we only simulate the 30-bit timer, which is
                supposed to count every 1ms
            */
            Registers.TimerConfiguration.Define32(this)
                .WithFlag(0, writeCallback: EnableCounter, name: "SPT_EN")
                .WithTag("CLK_SRC_SEL", 1, 1)
                .WithValueField(2, 8, out interruptMask, name: "INT_MASK_N")
                .WithValueField(10, 8, writeCallback: (_, val) => UpdateFFEKickOffPeriod((uint)val), name: "FFE_TO_PERIOD")
                .WithTag("PMU_TO_PERIOD", 18, 4)
                .WithReservedBits(22, 10);

            Registers.SleepMode.Define32(this)
                .WithFlag(0, flagField: out sleepMode, name: "SLEEP_MODE")
                .WithReservedBits(1, 31);

            Registers.UpdateTimerValue.Define32(this)
                .WithValueField(0, 30, writeCallback: (_, val) => UpdateSoftwareTimer((uint)val), name: "UPDATE_TIMER_VALUE")
                .WithReservedBits(30, 1);

            Registers.SpareBits.Define32(this)
                .WithValueField(0, 8, name: "SPARE_BITS")
                .WithReservedBits(8, 24);

            Registers.TimerValue.Define32(this)
                .WithValueField(0, 30, FieldMode.Read, valueProviderCallback: _ => (uint)timerSoftware30Bit.Value, name: "TIMER_VALUE")
                .WithReservedBits(30, 1);

            Registers.EventCounterValue.Define32(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (uint)ffeKickOffPeriod, name: "EVENT_CNT_VALUE")
                .WithReservedBits(8, 24);

            Registers.MsecCounterValue.Define32(this)
                //this should count the clock events reported by Clock Event Generator, and drive the 1ms timer. We don't support this granularity, so we return a default value
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x40, name: "MS_CNT_VALUE")
                .WithReservedBits(8, 24);
        }

        private void EnableCounter(bool _, bool value)
        {
            if(value)
            {
                timerSoftware30Bit.Enabled = true;
                this.Log(LogLevel.Noisy, "30-bit counter enabled");
                if(ffeKickOffPeriod != 0x0)
                {
                    timerFFEKickOffUpCounter.Enabled = true;
                    timerFFEKickOffUpCounter.Limit = ffeKickOffPeriod;
                    this.Log(LogLevel.Noisy, "FFE Kick off counter enabled");
                }
            }
            else
            {
                timerFFEKickOffUpCounter.Enabled = false;
                timerFFEKickOffUpCounter.Reset();

                timerSoftware30Bit.Enabled = false;
                this.Log(LogLevel.Noisy, "Counters disabled");
            }
            enabled = value;
        }

        private void UpdateFFEKickOffPeriod(uint value)
        {
            if(value == 0x0)
            {
                timerFFEKickOffUpCounter.Enabled = false;
            }
            else if(value < 0x4 || value > 0x64)
            {
                this.Log(LogLevel.Warning, "Trying to update the FFE kick off period with a reserved value 0x{0:X}", value);
            }
            else
            {
                if(enabled)
                {
                    timerFFEKickOffUpCounter.Enabled = true;
                    this.Log(LogLevel.Noisy, "FFE Kick off counter enabled");
                }
                timerFFEKickOffUpCounter.Limit = value;
            }

            ffeKickOffPeriod = value;
        }

        private void UpdateSoftwareTimer(uint value)
        {
            if(enabled)
            {
                this.Log(LogLevel.Warning, "Updating the 30-bit timer while it is enabled");
            }
            timerSoftware30Bit.Value = value;
        }

        public GPIO FFEKickOff { get; private set; }

        private bool enabled;
        private IFlagRegisterField sleepMode;
        private IValueRegisterField interruptMask;
        private ulong ffeKickOffPeriod;

        private ushort[] interruptTimestamps;

        private readonly LimitTimer timerSoftware30Bit;
        private readonly LimitTimer timerFFEKickOffUpCounter;

        private const int NumberOfInterrupts = 8;

        private enum Registers
        {
            TimerConfiguration = 0x0,
            SleepMode = 0x004,
            ErrorCompensation40ms = 0x008,
            ErrorCompensation1s0 = 0x00C,
            ErrorCompensation1s1 = 0x010,
            ErrorCompensation1s2 = 0x014,
            ErrorCompensation1s3 = 0x018,
            ErrorCompensationRTC0 = 0x01C,
            ErrorCompensationRTC1 = 0x020,
            ErrorCompensationRTC2 = 0x024,
            ErrorCompensationRTC3 = 0x028,
            UpdateTimerValue = 0x02C,
            SpareBits = 0x030,
            TimerValue = 0x034,
            EventCounterValue = 0x038,
            MsecCounterValue = 0x03C
        }
    }
}
