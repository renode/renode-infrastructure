//
// Copyright (c) 2010-2023 Antmicro
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

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class NPCX_ITIM32 : BasicDoubleWordPeripheral, IKnownSize
    {
        public NPCX_ITIM32(IMachine machine, long lfclkFrequency = DefaultLFCLKFrequency, long apb2Frequency = DefaultAPB2Frequency) : base(machine)
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

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            timer.Reset();
            IRQ.Unset();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Registers.Prescaler.Define(this)
                .WithValueField(0, 8, name: "PRE_8 (Prescaler Value)",
                    changeCallback: (_, value) => timer.Divider = (int)value + 1)
                .WithReservedBits(8, 24);

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
                    valueProviderCallback: _ =>
                    {
                        if(timer.Enabled)
                        {
                            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                            return timer.Value;
                        }
                        else
                        {
                            return timer.Limit - 1;
                        }
                    },
                    writeCallback: (_, value) =>
                    {
                        timer.Limit = value + 1;
                        timer.ResetValue();
                    });
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(timeoutStatus.Value && (interruptEnabled.Value || wakeupEnabled.Value));
        }

        private IFlagRegisterField timeoutStatus;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField wakeupEnabled;

        private readonly LimitTimer timer;
        private readonly long lfclkFrequency;
        private readonly long apb2Frequency;

        private const long DefaultLFCLKFrequency = 32768;
        private const long DefaultAPB2Frequency = 10000000;

        private enum Registers
        {
            Prescaler           = 0x1, // ITPRE32n
            ControlAndStatus    = 0x4, // ITCTS32n
            Counter             = 0x8, // ITCNT32n
        }
    }
}
