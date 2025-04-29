//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NRF_Bellboard : IGPIOReceiver, INumberedGPIOOutput, IDoubleWordPeripheral, IKnownSize
    {
        public NRF_Bellboard(IMachine machine, int numberOfEvents = MaxEventCount)
        {
            this.machine = machine;
            if(numberOfEvents > MaxEventCount)
            {
                throw new ConstructionException($"Cannot create peripheral with {numberOfEvents} connections, maximum number is {MaxEventCount}.");
            }
            this.numberOfEvents = numberOfEvents;

            eventsTriggered = new IFlagRegisterField[numberOfEvents];
            eventsEnabled = new IFlagRegisterField[InterruptLines][];

            var innerConnections = new Dictionary<int, IGPIO>();
            for(int i = 0; i < InterruptLines; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            DefineRegisters();
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "Received event on #{0}, value: {1}", number, value);
            eventsTriggered[number].Value = value;
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get ;}

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            for(var i = 0; i < InterruptLines; i++)
            {
                var j = i;
                registersMap.Add((long)Register.InterruptEnableOrDisable0 + i * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, out eventsEnabled[j], name: $"INTEN[{i}]")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.InterruptEnable0 + i * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, name: $"INTENSET[{i}]",
                        writeCallback: (k, _, value) => eventsEnabled[j][k].Value |= value,
                        valueProviderCallback: (k, _) => eventsEnabled[j][k].Value)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.InterruptDisable0 + i * 0x10, new DoubleWordRegister(this)
                   .WithFlags(0, numberOfEvents, name: $"INTENCLR[{i}]",
                        writeCallback: (k, _, value) =>
                        {
                            eventsEnabled[j][k].Value &= !value;
                        },
                        valueProviderCallback: (k, _) => eventsEnabled[i][k].Value)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                );

                registersMap.Add((long)Register.InterruptPending0 + i * 0x10, new DoubleWordRegister(this)
                    .WithFlags(0, numberOfEvents, FieldMode.Read, name: $"INTPEND[{i}]",
                        valueProviderCallback: (k, _) => eventsEnabled[j][k].Value)
                );
            }

            for(var i = 0; i < numberOfEvents; i++)
            {
                registersMap.Add((long)Register.EventsTriggered0 + i * 0x4, new DoubleWordRegister(this)
                    .WithFlag(0, out eventsTriggered[i],  name: $"EVENTS_TRIGGERED[{i}]")
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts()));
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < InterruptLines; i++)
            {
                for(var j = 0; j < numberOfEvents; j++)
                {
                    var eventEnabledAndSet = eventsEnabled[i][j].Value && eventsTriggered[j].Value;
                    if(eventEnabledAndSet)
                    {
                        this.Log(LogLevel.Noisy, $"Event #{j} enabled and set, triggering interrupt #{i}");
                        Connections[i].Blink();
                    }
                }
            }
        }

        private DoubleWordRegisterCollection registers;

        private readonly IFlagRegisterField[][] eventsEnabled;
        private readonly IFlagRegisterField[] eventsTriggered;
        private readonly IMachine machine;
        private readonly int numberOfEvents;

        private const int MaxEventCount = 32;
        private const int InterruptLines = 4;

        private enum Register : long
        {
            TaskTrigger0 = 0x0,
            EventsTriggered0 = 0x100,
            EventsTriggered1 = 0x104,
            EventsTriggered2 = 0x108,
            EventsTriggered3 = 0x10C,
            EventsTriggered4 = 0x110,
            EventsTriggered5 = 0x114,
            EventsTriggered6 = 0x118,
            EventsTriggered7 = 0x11C,
            EventsTriggered8 = 0x120,
            EventsTriggered9 = 0x124,
            EventsTriggered10 = 0x128,
            EventsTriggered11 = 0x12C,
            EventsTriggered12 = 0x130,
            EventsTriggered13 = 0x134,
            EventsTriggered14 = 0x138,
            EventsTriggered15 = 0x13C,
            EventsTriggered16 = 0x140,
            EventsTriggered17 = 0x144,
            EventsTriggered18 = 0x148,
            EventsTriggered19 = 0x14C,
            EventsTriggered20 = 0x150,
            EventsTriggered21 = 0x154,
            EventsTriggered22 = 0x158,
            EventsTriggered23 = 0x15C,
            EventsTriggered24 = 0x160,
            EventsTriggered25 = 0x164,
            EventsTriggered26 = 0x168,
            EventsTriggered27 = 0x16C,
            EventsTriggered28 = 0x170,
            EventsTriggered29 = 0x174,
            EventsTriggered30 = 0x178,
            EventsTriggered31 = 0x17C,
            InterruptEnableOrDisable0 = 0x300,
            InterruptEnable0 = 0x304,
            InterruptDisable0 = 0x308,
            InterruptPending0 = 0x30C,
            InterruptEnableOrDisable1 = 0x310,
            InterruptEnable1 = 0x314,
            InterruptDisable1 = 0x318,
            InterruptPending1 = 0x31C,
            InterruptEnableOrDisable2 = 0x320,
            InterruptEnable2 = 0x324,
            InterruptDisable2 = 0x328,
            InterruptPending2 = 0x32C,
            InterruptEnableOrDisable3 = 0x330,
            InterruptEnable3 = 0x334,
            InterruptDisable3 = 0x338,
            InterruptPending3 = 0x33C,
        }
    }
}
