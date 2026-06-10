//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using ICounter = Antmicro.Renode.Peripherals.Timers.ArmCorstone_SystemCounter.ICounter;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ArmCorstone_SystemTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public ArmCorstone_SystemTimer(IMachine machine, ArmCorstone_SystemCounter counter, bool autoIncrementFeatureEnabled = true) : base(machine)
        {
            this.autoIncrementFeatureEnabled = autoIncrementFeatureEnabled;
            counter.Counter.RegisterComparePoint(HandleCompareValueReached, this, "timer");
            this.counter = counter.Counter;
            this.counter.ValueLoaded += _ => UpdateInterrupts();
            this.counter.ValueOverflown += UpdateInterrupts;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            compareValue = 0;
            autoIncrementValue = 0;
            counter.DeactivateComparePoint(HandleCompareValueReached);
            UpdateInterrupts();
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.PhysicalCountLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CountValue",
                    valueProviderCallback: _ => counter.ValueLow
                )
            ;

            Registers.PhysicalCountHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CountValue",
                    valueProviderCallback: _ => counter.ValueHigh
                )
            ;

            Registers.CounterFrequency.Define(this)
                // Hardware does not interpret the value of this register
                .WithValueField(0, 32, name: "ClockFrequency")
            ;

            Registers.TimerCompareValueLow.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "CompareValue",
                    valueProviderCallback: _ => CompareValueLow,
                    writeCallback: (_, value) => CompareValueLow = (uint)value
                )
            ;

            Registers.TimerCompareValueHigh.Define(this, 0x00000000)
                .WithValueField(0, 32, name: "CompareValue",
                    valueProviderCallback: _ => CompareValueHigh,
                    writeCallback: (_, value) => CompareValueHigh = (uint)value
                )
            ;

            Registers.TimerValue.Define(this)
                .WithValueField(0, 32, name: "TimerValue",
                    valueProviderCallback: _ =>
                    {
                        if(!enabled.Value)
                        {
                            this.WarningLog("TimerValue is unknown when ENABLE is unset");
                        }
                        return CompareValue - counter.Value;
                    },
                    writeCallback: (_, value) =>
                    {
                        var signedValue = (int)value;
                        var absValue = (uint)Math.Abs(signedValue);
                        CompareValue = signedValue < 0 ? counter.Value - absValue : counter.Value + absValue;
                        UpdateInterrupts();
                    }
                )
            ;

            Registers.TimerControl.Define(this)
                .WithFlag(0, out enabled, name: "ENABLE",
                    changeCallback: (_, __) => UpdateComparePoint()
                )
                .WithFlag(1, out interruptMask, name: "IMASK")
                .WithFlag(2, FieldMode.Read, name: "ISTATUS",
                    valueProviderCallback: _ => Interrupt
                )
                .WithReservedBits(3, 29)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.AutoIncrementValueLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "AutoIncrValue timer value",
                    valueProviderCallback: _ => AutoIncrementValueLow
                )
            ;

            Registers.AutoIncrementValueHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "AutoIncrValue timer value",
                    valueProviderCallback: _ => AutoIncrementValueHigh
                )
            ;

            Registers.AutoIncrementValueReload.Define(this)
                .WithValueField(0, 32, out autoIncrementValueReload, name: "AutoIncrValue timer value",
                    changeCallback: (_, __) =>
                    {
                        if(!autoIncrementEnabled.Value)
                        {
                            return;
                        }

                        AutoIncrementValue = counter.Value + autoIncrementValueReload.Value;
                    }
                )
            ;

            Registers.AutoIncrementValueControl.Define(this)
                .WithFlag(0, out autoIncrementEnabled, FieldMode.Write, name: "EN",
                    changeCallback: (_, value) =>
                    {
                        if(!autoIncrementFeatureEnabled)
                        {
                            this.WarningLog("Attempted to enabled automatic increment, but the feature is disabled");
                            autoIncrementEnabled.Value = false;
                            return;
                        }

                        if(value)
                        {
                            AutoIncrementValue = autoIncrementValueReload.Value + counter.Value;
                        }

                        autoIncrementInterrupt.Value &= !value;
                    }
                )
                .WithFlag(1, out autoIncrementInterrupt, FieldMode.WriteZeroToClear, name: "CLR")
                .WithReservedBits(2, 30)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TimerConfiguration.Define(this, 0x1)
                .WithValueField(0, 4, name: "AIVAL",
                    valueProviderCallback: _ => autoIncrementFeatureEnabled ? 0b0001UL : 0b0000UL
                )
                .WithReservedBits(4, 28)
            ;

            Registers.PeripheralIdentification4.Define(this, 0x00000004)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralIdentification0.Define(this, 0x000000B7)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralIdentification1.Define(this, 0x000000B0)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralIdentification2.Define(this, 0x0000000B)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralIdentification3.Define(this, 0x00000000)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentIdentification0.Define(this, 0x0000000D)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentIdentification1.Define(this, 0x000000F0)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentIdentification2.Define(this, 0x00000005)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentIdentification3.Define(this, 0x000000B1)
                .WithReservedBits(0, 32)
            ;
        }

        private void HandleCompareValueReached()
        {
            if(autoIncrementEnabled.Value)
            {
                autoIncrementInterrupt.Value = true;
                AutoIncrementValue = autoIncrementValueReload.Value + counter.Value;
            }
            UpdateInterrupts();
        }

        private void UpdateComparePoint()
        {
            if(enabled.Value)
            {
                counter.SetComparePoint(HandleCompareValueReached, autoIncrementEnabled.Value ? AutoIncrementValue : CompareValue);
            }
            else
            {
                counter.DeactivateComparePoint(HandleCompareValueReached);
            }
        }

        private void UpdateInterrupts()
        {
            var was = IRQ.IsSet;
            IRQ.Set(Interrupt && !interruptMask.Value);
            if(was != IRQ.IsSet)
            {
                this.NoisyLog("IRQ: {0}set", was ? "un" : "");
            }
        }

        private bool CompareStatus => counter.Value >= CompareValue && enabled.Value;

        private bool Interrupt => autoIncrementEnabled.Value ? autoIncrementInterrupt.Value : CompareStatus;

        private ulong CompareValue
        {
            get => compareValue;
            set
            {
                if(compareValue == value)
                {
                    return;
                }

                compareValue = value;
                if(!autoIncrementEnabled.Value)
                {
                    UpdateComparePoint();
                }
            }
        }

        private uint CompareValueLow
        {
            get => (uint)compareValue;
            set => CompareValue = BitHelper.SetBitsFrom(compareValue, value, position: 0, width: 32);
        }

        private uint CompareValueHigh
        {
            get => (uint)(compareValue >> 32);
            set => CompareValue = BitHelper.SetBitsFrom(compareValue, value, position: 32, width: 32);
        }

        private ulong AutoIncrementValue
        {
            get => autoIncrementValue;
            set
            {
                if(autoIncrementValue == value)
                {
                    return;
                }

                autoIncrementValue = value;
                if(autoIncrementEnabled.Value)
                {
                    UpdateComparePoint();
                }
            }
        }

        private uint AutoIncrementValueLow
        {
            get => (uint)autoIncrementValue;
            set => autoIncrementValue = BitHelper.SetBitsFrom(autoIncrementValue, value, position: 0, width: 32);
        }

        private uint AutoIncrementValueHigh
        {
            get => (uint)(autoIncrementValue >> 32);
            set => autoIncrementValue = BitHelper.SetBitsFrom(autoIncrementValue, value, position: 32, width: 32);
        }

        private ulong compareValue;
        private ulong autoIncrementValue;

        private IFlagRegisterField enabled;
        private IFlagRegisterField interruptMask;
        private IFlagRegisterField autoIncrementEnabled;
        private IFlagRegisterField autoIncrementInterrupt;

        private IValueRegisterField autoIncrementValueReload;

        private readonly bool autoIncrementFeatureEnabled;

        private readonly ICounter counter;

        public enum Registers
        {
            PhysicalCountLow          = 0x000, // CNTPCT[31:0]
            PhysicalCountHigh         = 0x004, // CNTPCT[63:32]
            CounterFrequency          = 0x010, // CNTFRQ
            TimerCompareValueLow      = 0x020, // CNTP_CVAL[31:0]
            TimerCompareValueHigh     = 0x024, // CNTP_CVAL[63:32]
            TimerValue                = 0x028, // CNTP_TVAL
            TimerControl              = 0x02C, // CNTP_CTL
            AutoIncrementValueLow     = 0x040, // CNTP_AIVAL[31:0]
            AutoIncrementValueHigh    = 0x044, // CNTP_AIVAL[63:32]
            AutoIncrementValueReload  = 0x048, // CNTP_AIVAL_RELOAD
            AutoIncrementValueControl = 0x04C, // CNTP_AIVAL_CTL
            TimerConfiguration        = 0x050, // CNTP_CFG
            PeripheralIdentification4 = 0xFD0, // CNTP_PIDR4
            PeripheralIdentification0 = 0xFE0, // CNTP_PIDR0
            PeripheralIdentification1 = 0xFE4, // CNTP_PIDR1
            PeripheralIdentification2 = 0xFE8, // CNTP_PIDR2
            PeripheralIdentification3 = 0xFEC, // CNTP_PIDR3
            ComponentIdentification0  = 0xFF0, // CNTP_CIDR0
            ComponentIdentification1  = 0xFF4, // CNTP_CIDR1
            ComponentIdentification2  = 0xFF8, // CNTP_CIDR2
            ComponentIdentification3  = 0xFFC, // CNTP_CID3
        }
    }
}
