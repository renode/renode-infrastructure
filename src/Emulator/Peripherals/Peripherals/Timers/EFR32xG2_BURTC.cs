//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32xG2_BURTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_BURTC(Machine machine, long frequency = 32768) : base(machine)
        {
            IRQ = new GPIO();
            interruptManager = new InterruptManager<Interrupt>(this);

            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "burtc", limit: uint.MaxValue, compare: uint.MaxValue, workMode: WorkMode.Periodic, eventEnabled: true);

            // TODO: add support for the overflow exception
            innerTimer.CompareReached += delegate
            {
                interruptManager.SetInterrupt(Interrupt.CompareMatch);
            };

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            interruptManager.Reset();
        }

        public long Size => 0x3034;

        [IrqProvider]
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.IpVersion.Define(this)
                .WithTag("IPVERSION", 0, 32);

            Register.ModuleEnable.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 31);

            Register.Configuration.Define(this)
                .WithTaggedFlag("DEBUGRUN", 0)
                .WithTaggedFlag("COMPTOP", 1)
                .WithReservedBits(2, 2)
                .WithValueField(4, 4,
                    writeCallback: (_, value) => innerTimer.Divider = (uint)Math.Pow(2, value),
                    valueProviderCallback: _ => (uint)Math.Log(innerTimer.Divider, 2),
                    name: "CNTPRESC")
                .WithReservedBits(8, 24);

            Register.Command.Define(this)
                .WithEnumField<DoubleWordRegister, Command>(0, 2, FieldMode.Write, writeCallback: (_, value) =>
                {
                    switch(value)
                    {
                        case Command.None:
                            break;
                        case Command.Start:
                            innerTimer.Enabled = true;
                            break;
                        case Command.Stop:
                            innerTimer.Enabled = false;
                            break;
                        default:
                            this.Log(LogLevel.Warning, "Unsupported command combination: {0}", value);
                            break;
                    }
                }, name: "START/STOP")
                .WithReservedBits(2, 30);

            Register.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => innerTimer.Enabled, name: "RUNNING")
                .WithTaggedFlag("LOCK", 1)
                .WithReservedBits(2, 30);

            RegistersCollection.AddRegister((long)Register.InterruptFlags, interruptManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => interruptManager.IsSet(irq),
                writeCallback: (irq, _, newValue) => interruptManager.SetInterrupt(irq, newValue)));

            RegistersCollection.AddRegister((long)Register.InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());

            Register.CounterValue.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) => innerTimer.Value = value,
                    valueProviderCallback: _ => innerTimer.Value,
                    name: "CNT");

            Register.SynchronizationBusy.Define(this)
                .WithTaggedFlag("START", 0)
                .WithTaggedFlag("STOP", 1)
                .WithTaggedFlag("PRECNT", 2)
                .WithTaggedFlag("CNT", 3)
                .WithTaggedFlag("COMP", 4)
                .WithTaggedFlag("EN", 5)
                .WithReservedBits(6, 26);

            Register.ConfigurationLock.Define(this)
                .WithTag("LOCK", 0, 15)
                .WithReservedBits(15, 17);

            Register.CompareValue.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) => innerTimer.Compare = value,
                    valueProviderCallback: _ => (uint)innerTimer.Compare,
                    name: "COMP");

            RegistersCollection.AddRegister((long)Register.InterruptEnableSet, interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());

            RegistersCollection.AddRegister((long)Register.InterruptFlagsClear, interruptManager.GetInterruptClearRegister<DoubleWordRegister>());
        }

        private readonly InterruptManager<Interrupt> interruptManager;
        private readonly ComparingTimer innerTimer;

        private enum Interrupt
        {
            Overflow,
            CompareMatch,
        }

        private enum Command
        {
            None = 0b00,
            Start = 0b01,
            Stop = 0b10,
        }

        private enum Register
        {
            IpVersion = 0x0,
            ModuleEnable = 0x4,
            Configuration = 0x8,
            Command = 0xC,
            Status = 0x10,
            InterruptFlags = 0x14,
            InterruptEnable = 0x18,
            PreCounterValue = 0x1C,
            CounterValue = 0x20,
            EM4WakeupRequest = 0x24,
            SynchronizationBusy = 0x28,
            ConfigurationLock = 0x2C,
            CompareValue = 0x30,

            IpVersionSet = 0x1000,
            ModuleEnableSet = 0x1004,
            ConfigurationSet = 0x1008,
            CommandSet = 0x100C,
            StatusSet = 0x1010,
            InterruptFlagsSet = 0x1014,
            InterruptEnableSet = 0x1018,
            PreCounterValueSet = 0x101C,
            CounterValueSet = 0x1020,
            EM4WakeupRequestSet = 0x1024,
            SynchronizationBusySet = 0x1028,
            ConfigurationLockSet = 0x102C,
            CompareValueSet = 0x1030,

            IpVersionClear = 0x2000,
            ModuleEnableClear = 0x2004,
            ConfigurationClear = 0x2008,
            CommandClear = 0x200C,
            StatusClear = 0x2010,
            InterruptFlagsClear = 0x2014,
            InterruptEnableClear = 0x2018,
            PreCounterValueClear = 0x201C,
            CounterValueClear = 0x2020,
            EM4WakeupRequestClear = 0x2024,
            SynchronizationBusyClear = 0x2028,
            ConfigurationLockClear = 0x202C,
            CompareValueClear = 0x2030,

            IpVersionToggle = 0x3000,
            ModuleEnableToggle = 0x3004,
            ConfigurationToggle = 0x3008,
            CommandToggle = 0x300C,
            StatusToggle = 0x3010,
            InterruptFlagsToggle = 0x3014,
            InterruptEnableToggle = 0x3018,
            PreCounterValueToggle = 0x301C,
            CounterValueToggle = 0x3020,
            EM4WakeupRequestToggle = 0x3024,
            SynchronizationBusyToggle = 0x3028,
            ConfigurationLockToggle = 0x302C,
            CompareValueToggle = 0x3030
        }
    }
}
