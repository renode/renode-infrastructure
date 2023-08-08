//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NRF52840_EGU : BasicDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_EGU(IMachine machine) : base(machine)
        {
            interruptManager = new InterruptManager<Events>(this, IRQ, "EGU_IRQ");

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            interruptManager.Reset();
            base.Reset();
        }

        private void DefineRegisters()
        {
            Registers.TasksTrigger0.DefineMany(this, NumberOfTasks, (reg, i) => 
            {
                reg.WithFlag(0, FieldMode.Write, writeCallback: (_, value) => 
                {
                    if(value)
                    {
                        interruptManager.SetInterrupt((Events)i);
                        events[i].Value = true;

                        this.NoisyLog("Triggering event {0}.", i);

                        // Triggering a task results in raising an interrupt and generating an event
                        EventTriggered?.Invoke((uint)Registers.EventsTriggered0 + (uint)i * EventsTriggerRegisterSize);
                    }
                })
                .WithReservedBits(1, 31);
            }, TasksTriggerRegisterSize, name: "TasksTrigger");

            // These registers are used to check if an event has been generated for a particular task (if a task has been triggered)
            // and to reset event state on write
            Registers.EventsTriggered0.DefineMany(this, NumberOfTasks, (reg, i) => 
            {
                reg.WithFlag(0, flagField: out events[i], writeCallback: (_, value) => 
                {
                    if(!value)
                    {
                        interruptManager.SetInterrupt((Events)i, false);
                    }
                    this.NoisyLog("Change event {0} to state {1}.", i, value);
                })
                .WithReservedBits(1, 31);
            }, EventsTriggerRegisterSize, name: "EventsTriggered");

            RegistersCollection.AddRegister((long)Registers.InterruptEnableDisable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptSet, interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptClear, interruptManager.GetInterruptEnableClearRegister<DoubleWordRegister>());
        }

        public long Size => 0x1000;

        public event Action<uint> EventTriggered;
        public GPIO IRQ { get; } = new GPIO();
        private readonly InterruptManager<Events> interruptManager;

        private const uint NumberOfTasks = 16;
        private const uint TasksTriggerRegisterSize = 0x4;
        private const uint EventsTriggerRegisterSize = 0x4;

        private readonly IFlagRegisterField[] events = new IFlagRegisterField[NumberOfTasks];

        private enum Events
        {
            Triggered0 = 0,
            Triggered1,
            Triggered2,
            Triggered3,
            Triggered4,
            Triggered5,
            Triggered6,
            Triggered7,
            Triggered8,
            Triggered9,
            Triggered10,
            Triggered11,
            Triggered12,
            Triggered13,
            Triggered14,
            Triggered15,
        }

        private enum Registers
        {
            TasksTrigger0 = 0x0,
            EventsTriggered0 = 0x100,
            InterruptEnableDisable = 0x300,
            InterruptSet = 0x304,
            InterruptClear = 0x308
        }
    }
}
