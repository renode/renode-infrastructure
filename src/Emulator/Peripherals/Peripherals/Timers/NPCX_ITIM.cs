//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class NPCX_ITIM : BasicDoubleWordPeripheral, IKnownSize
    {
        public NPCX_ITIM(IMachine machine, long lfclkFrequency = DefaultLFCLKFrequency, long apb2Frequency = DefaultAPB2Frequency, bool is64Bit = false) : base(machine)
        {
            this.lfclkFrequency = lfclkFrequency;
            this.apb2Frequency = apb2Frequency;

            timer = new LimitTimer(machine.ClockSource, apb2Frequency, this, "timer", direction: Direction.Descending, workMode: WorkMode.Periodic, divider: 1, eventEnabled: true);
            timer.LimitReached += () =>
            {
                timeoutStatus.Value = true;
                UpdateInterrupts();
            };
            IRQ = new GPIO();

            DefineRegisters(is64Bit);
        }

        public override void Reset()
        {
            base.Reset();
            timer.Reset();
            IRQ.Unset();
            timerLimitLow = 0;
            timerLimitHigh = 0;
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void DefineRegisters(bool is64Bit)
        {
            Registers.Prescaler.Define(this)
                .WithReservedBits(0, 8)
                .WithValueField(8, 8, name: "PRE_8 (Prescaler Value)",
                    changeCallback: (_, value) => timer.Divider = (int)value + 1)
                .WithReservedBits(16, 16);

            Registers.ControlAndStatus.Define(this)
                .WithFlag(0, out timeoutStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "TO_STS (Timeout Status)")
                .WithReservedBits(1, 1)
                .WithFlag(2, out interruptEnabled, name: "TO_IE (Timeout Interrupt Enable)",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithFlag(3, out wakeupEnabled, name: "TO_WUE (Timeout Wake-Up Enable)",
                    changeCallback: (_, __) => UpdateInterrupts())
                .WithFlag(4, name: "CKSEL (Input Clock Select)",
                    changeCallback: (_, value) => timer.Frequency = value ? lfclkFrequency : apb2Frequency)
                .WithReservedBits(5, 2)
                .WithFlag(7, name: "ITEN (ITIM32 Module Enable)",
                    changeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            timeoutStatus.Value = false;
                            timer.ResetValue();
                        }
                        UpdateInterrupts();
                        timer.Enabled = value;
                    })
                .WithReservedBits(8, 24);

            Registers.Counter.Define(this)
                .WithValueField(0, 32, name: "CNT_32 (32-Bit Counter Value)",
                    valueProviderCallback: _ => timer.Enabled ? GetTimerValue(true) : timerLimitLow,
                    writeCallback: (_, value) =>
                    {
                        timerLimitLow = (uint)value;
                        UpdateTimerLimit();
                    });
            
            if(is64Bit)
            {
                Registers.CounterHigh.Define(this)
                    .WithValueField(0, 32, name: "CNT_64H (64-Bit Counter High DWord Value)",
                        valueProviderCallback: _ => timer.Enabled ? GetTimerValue(false) >> 32 : timerLimitHigh,
                        writeCallback: (_, value) =>
                        {
                            timerLimitHigh = (uint)value;
                            UpdateTimerLimit();
                        });
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(timeoutStatus.Value && (interruptEnabled.Value || wakeupEnabled.Value));
        }

        private ulong GetTimerValue(bool syncTime)
        {
            if(syncTime && machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
            return timer.Value;
        }

        private void UpdateTimerLimit()
        {
            ulong limit = (ulong)timerLimitHigh << 32 | timerLimitLow;
            // To prevent overflow don't add 1 to the limit when a maximum value is requested
            timer.Limit = checked(limit == ulong.MaxValue ? limit : limit + 1);
            timer.ResetValue();
        }

        private IFlagRegisterField timeoutStatus;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField wakeupEnabled;

        private uint timerLimitLow;
        private uint timerLimitHigh;

        private readonly LimitTimer timer;
        private readonly long lfclkFrequency;
        private readonly long apb2Frequency;

        private const long DefaultLFCLKFrequency = 32768;
        private const long DefaultAPB2Frequency = 10000000;

        private enum Registers
        {
            // Prescaler register is defined at offset 0x1
            // Due to current lack of support for register collections
            // with multiple widths, this register will be implemented
            // as the second byte in a 4 byte register
            Prescaler           = 0x0, // ITPRE32n or ITPRE64
            ControlAndStatus    = 0x4, // ITCTS32n or ITCTS64
            Counter             = 0x8, // ITCNT32n or ITCNT64L
            CounterHigh         = 0xC, // ITCNT64H
        }
    }
}
