//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K3XX_SoftwareWatchdogTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_SoftwareWatchdogTimer(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            // HACK: There is a bug in LimitTimer which requires to set limit argument to be bigger than any limit given later
            internalTimer = new LimitTimer(machine.ClockSource, 32000 , this, "wdt", eventEnabled: true);
            internalTimer.Limit = DefaultWatchdogTimeout;
            internalTimer.LimitReached += HandleTimeout;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            IRQ.Unset();

            previousServiceKeyWrite = 0;
            pendingReset = false;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(!registerSoftLock.Value)
            {
                base.WriteDoubleWord(offset, value);
                return;
            }

            switch((Registers)offset)
            {
                case Registers.Control:
                case Registers.Timeout:
                case Registers.Window:
                case Registers.ServiceKey:
                    this.Log(LogLevel.Warning, "Tried to write {0} while in soft lock", (Registers)offset);
                    return;
                default:
                    base.WriteDoubleWord(offset, value);
                    break;
            }
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }

        public ushort ServiceKey
        {
            get => (ushort)serviceKey.Value;
            private set => serviceKey.Value = value;
        }

        public ushort NextServiceKey => (ushort)(ServiceKey * 17 + 3);

        private void HandleTimeout()
        {
            if(!generateInterrupt.Value || pendingReset)
            {
                machine.RequestReset();
                return;
            }

            pendingReset = true;
            interruptPending.Value = true;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(interruptPending.Value);
        }

        private void HandleServiceKey(ushort value)
        {
            var keyGiven = new Tuple<ushort, ushort>(previousServiceKeyWrite, value);
            previousServiceKeyWrite = value;

            if(windowMode.Value && windowStartValue.Value > internalTimer.Value)
            {
                this.Log(LogLevel.Warning, "Tried to write service key while window mode is enabled and before window period");
                if(resetOnInvalidAccess.Value)
                {
                    machine.RequestReset();
                }
                return;
            }

            if(keyGiven.Equals(softLockSequenceServiceKey))
            {
                registerSoftLock.Value = false;
                // NOTE: Soft lock sequence can still be valid keyed sequence for servicing watchdog
                // thus we allow to pass-through
            }

            var resetCondition = false;
            switch(serviceMode.Value)
            {
                case ServiceMode.FixedSequence:
                    if(keyGiven.Equals(fixedSequenceServiceKey))
                    {
                        resetCondition = true;
                    }
                    break;

                case ServiceMode.KeyedSequence:
                    if(keyGiven.Equals(KeyedSequenceServiceKey))
                    {
                        resetCondition = true;
                    }
                    if(value == NextServiceKey)
                    {
                        ServiceKey = NextServiceKey;
                    }
                    break;
            }

            if(resetCondition)
            {
                internalTimer.Value = internalTimer.Limit;
                pendingReset = false;
            }
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0xFF00010A)
                .WithFlag(0, name: "WatchdogEnable",
                    valueProviderCallback: _ => internalTimer.Enabled,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            internalTimer.Value = internalTimer.Limit;
                        }
                        internalTimer.Enabled = value;
                    })
                .WithTaggedFlag("DebugModeControl", 1)
                .WithTaggedFlag("StopModeControl", 2)
                .WithReservedBits(3, 1)
                .WithFlag(4, out registerSoftLock, name: "SoftLock")
                .WithTaggedFlag("HardLock", 5)
                .WithFlag(6, out generateInterrupt, name: "InterruptThenResetRequest")
                .WithFlag(7, out windowMode, name: "WindowMode")
                .WithFlag(8, out resetOnInvalidAccess, name: "ResetOnInvalidAccess")
                .WithEnumField(9, 2, out serviceMode, name: "ServiceMode")
                .WithReservedBits(11, 13)
                .WithTaggedFlag("MAP7", 24)
                .WithTaggedFlag("MAP6", 25)
                .WithTaggedFlag("MAP5", 26)
                .WithTaggedFlag("MAP4", 27)
                .WithTaggedFlag("MAP3", 28)
                .WithTaggedFlag("MAP2", 29)
                .WithTaggedFlag("MAP1", 30)
                .WithTaggedFlag("MAP0", 31)
            ;

            Registers.Interrupt.Define(this)
                .WithFlag(0, out interruptPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "TimeoutInterruptFlag")
                .WithReservedBits(1, 31)
                .WithChangeCallback((_, __) => UpdateInterrupt())
            ;

            Registers.Timeout.Define(this, DefaultWatchdogTimeout)
                .WithValueField(0, 32, name: "WatchdogTimeout",
                    valueProviderCallback: _ => internalTimer.Limit,
                    writeCallback: (_, value) => internalTimer.Limit = Math.Max(MinimalWatchdogTimeout, value))
            ;

            Registers.Window.Define(this)
                .WithValueField(0, 32, out windowStartValue, name: "WindowStartValue")
            ;

            Registers.Service.Define(this)
                .WithValueField(0, 16, name: "WatchdogServiceCode",
                    valueProviderCallback: _ => 0,
                    writeCallback: (_, value) => HandleServiceKey((ushort)value))
                .WithReservedBits(16, 16)
            ;

            Registers.CounterOutput.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "WatchdogCount",
                    valueProviderCallback: _ => internalTimer.Value)
            ;

            Registers.ServiceKey.Define(this)
                .WithValueField(0, 16, out serviceKey, name: "ServiceKey")
                .WithReservedBits(16, 16)
            ;

            Registers.EventRequest.Define(this)
                .WithFlag(0, out requestInternalReset, name: "ResetRequestFlag")
                .WithReservedBits(1, 31)
            ;
        }

        private Tuple<ushort, ushort> KeyedSequenceServiceKey =>
            new Tuple<ushort, ushort>(ServiceKey, NextServiceKey);

        private readonly LimitTimer internalTimer;
        private readonly Tuple<ushort, ushort> fixedSequenceServiceKey =
            new Tuple<ushort, ushort>(0xA602, 0xB480);
        private readonly Tuple<ushort, ushort> softLockSequenceServiceKey =
            new Tuple<ushort, ushort>(0xC520, 0xD928);

        private const int MinimalWatchdogTimeout = 3;
        private const uint DefaultWatchdogTimeout = 0x320;

        private IFlagRegisterField generateInterrupt;
        private IFlagRegisterField windowMode;
        private IFlagRegisterField interruptPending;
        private IFlagRegisterField requestInternalReset;
        private IFlagRegisterField resetOnInvalidAccess;
        private IFlagRegisterField registerSoftLock;

        private IEnumRegisterField<ServiceMode> serviceMode;
        private IValueRegisterField serviceKey;
        private IValueRegisterField windowStartValue;

        private ushort previousServiceKeyWrite;
        private bool pendingReset;

        private enum ServiceMode
        {
            FixedSequence,
            KeyedSequence
        }

        private enum Registers
        {
            Control = 0x0, // CR
            Interrupt = 0x4, // IR
            Timeout = 0x8, // TO
            Window = 0xC, // WN
            Service = 0x10, // SR
            CounterOutput = 0x14, // CO
            ServiceKey = 0x18, // SK
            EventRequest = 0x1C // RRR
        }
    }
}
