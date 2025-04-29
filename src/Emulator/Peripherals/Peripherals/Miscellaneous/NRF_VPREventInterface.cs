//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NRF_VPREventInterface : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public NRF_VPREventInterface(IMachine machine, int numberOfEvents = MaxEventCount) : base(machine, numberOfEvents)
        {
            if(numberOfEvents > MaxEventCount)
            {
                throw new ConstructionException($"Cannot create peripheral with {numberOfEvents} connections, maximum number is {MaxEventCount}.");
            }
            this.numberOfEvents = numberOfEvents;
            DefineRegisters();
        }

        public override void Reset()
        {
            registers.Reset();
            base.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            for(var i = 0; i < numberOfEvents; i++)
            {
                var j = i;
                registersMap.Add((long)Registers.TaskTrigger0 + j * 0x4, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Noisy, "Triggering task #{0}", j);
                            Connections[j].Blink();
                        },  name: $"TASKS_TRIGGER[{j}]")
                    .WithReservedBits(1, 31));

                registersMap.Add((long)Registers.SubscribeTrigger + j * 0x4, new DoubleWordRegister(this)
                    .WithReservedBits(0, 30)
                    .WithTaggedFlag($"SUBSCRIBE_TRIGGER[{j}]", 31));

                registersMap.Add((long)Registers.EventsTriggered + j * 0x4, new DoubleWordRegister(this)
                    .WithTaggedFlag($"EVENTS_TRIGGERED[{j}]", 0)
                    .WithReservedBits(1, 31));

                registersMap.Add((long)Registers.PublishTriggered + j * 0x4, new DoubleWordRegister(this)
                    .WithReservedBits(0, 30)
                    .WithTaggedFlag($"PUBLISH_TRIGGERED[{j}]", 31));
            }

            registersMap.Add((long)Registers.InterruptEnableOrDisable, new DoubleWordRegister(this)
                .WithTag($"INTEN", 0, 32));

            registersMap.Add((long)Registers.InterruptEnable, new DoubleWordRegister(this)
                .WithTag($"INTENSET", 0, 32));

            registersMap.Add((long)Registers.InterruptDisable, new DoubleWordRegister(this)
                .WithTag($"INTENCLR", 0, 32));

            registersMap.Add((long)Registers.InterruptPending, new DoubleWordRegister(this)
                .WithTag($"INTPEND", 0, 32));

            registersMap.Add((long)Registers.DebugIF, new DoubleWordRegister(this)
                .WithTag($"DEBUGIF", 0, 32));

            registersMap.Add((long)Registers.CPUStateAfterReset, new DoubleWordRegister(this)
                .WithTag($"CPURUN", 0, 32));

            registersMap.Add((long)Registers.InitPC, new DoubleWordRegister(this)
                .WithTag($"INITPC", 0, 32));

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private DoubleWordRegisterCollection registers;

        private readonly IBusController sysbus;
        private readonly int numberOfEvents;

        private const int MaxEventCount = 32;

        private enum Registers : long
        {
            TaskTrigger0 = 0x00,
            TaskTrigger1 = 0x04,
            TaskTrigger2 = 0x08,
            TaskTrigger3 = 0x0C,
            TaskTrigger4 = 0x10,
            TaskTrigger5 = 0x14,
            TaskTrigger6 = 0x18,
            TaskTrigger7 = 0x1C,
            TaskTrigger8 = 0x20,
            TaskTrigger9 = 0x24,
            TaskTrigger10 = 0x28,
            TaskTrigger11 = 0x2C,
            TaskTrigger12 = 0x30,
            TaskTrigger13 = 0x34,
            TaskTrigger14 = 0x38,
            TaskTrigger15 = 0x3C,
            TaskTrigger16 = 0x40,
            TaskTrigger17 = 0x44,
            TaskTrigger18 = 0x48,
            TaskTrigger19 = 0x4C,
            TaskTrigger20 = 0x50,
            TaskTrigger21 = 0x54,
            TaskTrigger22 = 0x58,
            TaskTrigger23 = 0x5C,
            TaskTrigger24 = 0x60,
            TaskTrigger25 = 0x64,
            TaskTrigger26 = 0x68,
            TaskTrigger27 = 0x6C,
            TaskTrigger28 = 0x70,
            TaskTrigger29 = 0x74,
            TaskTrigger30 = 0x78,
            TaskTrigger31 = 0x7C,
            SubscribeTrigger = 0x80,
            EventsTriggered = 0x100,
            PublishTriggered = 0x180,
            InterruptEnableOrDisable = 0x300,
            InterruptEnable = 0x304,
            InterruptDisable = 0x308,
            InterruptPending = 0x30C,
            DebugIF = 0x400,
            CPUStateAfterReset = 0x800,
            InitPC = 0x808
        }
    }
}
