//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.ObjectModel;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32H7_EXTI : BasicDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public STM32H7_EXTI(IMachine machine) : base(machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < CoreCount * LinesPerCore; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            cores = new STM32_EXTICore[CoreCount];
            for(var i = 0; i < CoreCount; i++)
            {
                cores[i] = new STM32_EXTICore(this, LineConfigurations[i]);
            }

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var connection in Connections)
            {
                connection.Value.Unset();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= NumberOfLines)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0; {1})", number, NumberOfLines);
                return;
            }

            var numberInCore = (byte)(number % LinesPerCore);
            var core = cores[number / LinesPerCore];

            if(core.CanSetInterruptValue(numberInCore, value, out var isLineConfigurable))
            {
                if(isLineConfigurable)
                {
                    // Configurable line can only be set from this place
                    value = true;
                    core.UpdatePendingValue(numberInCore, true);
                }
                Connections[number].Set(value);
            }
        }

        public long Size => 0x400;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public long NumberOfLines => Connections.Count;

        private void DefineRegisters()
        {
            // We can't use DefineMany here as each of the InterruptMaskX registers has a different reset value
            for(var idx = 0; idx < CoreCount; idx++)
            {
                var core = cores[idx];

                RegistersCollection.DefineRegister((long)Registers.RisingTriggerSelection1 + idx * 0x20)
                    .WithValueField(0, 32, out core.RisingEdgeMask, name: "RTSR");

                RegistersCollection.DefineRegister((long)Registers.FallingTriggerSelection1 + idx * 0x20)
                    .WithValueField(0, 32, out core.FallingEdgeMask, name: "FTSR");

                RegistersCollection.DefineRegister((long)Registers.SoftwareInterruptEvent1 + idx * 0x20)
                    .WithValueField(0, 32, name: "SWIER", valueProviderCallback: _ => 0x0,
                        changeCallback: (_, value) =>
                        {
                            BitHelper.ForeachActiveBit(value & core.InterruptMask.Value, bit =>
                            {
                                var connectionNumber = LinesPerCore * idx + bit;
                                if(!Connections.TryGetValue(connectionNumber, out var irq))
                                {
                                    this.Log(LogLevel.Warning, "Cannot set software interrupt on line {0} as it does not exist", connectionNumber);
                                    return;
                                }
                                core.UpdatePendingValue(bit, true);
                                irq.Set();
                            });
                        });

                // All configurable lines are by default masked. So the reset value is the inverse of if the line is configurable
                RegistersCollection.DefineRegister((long)Registers.InterruptMask1 + idx * 0x10, ~LineConfigurations[idx])
                    .WithValueField(0, 32, out core.InterruptMask, name: "CPUIMR");

                RegistersCollection.DefineRegister((long)Registers.Pending1 + idx * 0x10)
                    .WithValueField(0, 32, out core.PendingInterrupts, FieldMode.Read | FieldMode.WriteOneToClear, name: "CPUPR",
                        writeCallback: (_, value) => BitHelper.ForeachActiveBit(value, bit => Connections[bit].Unset()));
            }
        }

        private readonly STM32_EXTICore[] cores;

        // This is for the STM32H7 configuration
        // Set bit means that this interrupt is configurable
        private static readonly uint[] LineConfigurations = new uint[CoreCount]
        {
            0x3FFFFF,
            0xA0000,
            0x740000,
        };

        private const int CoreCount = 3;
        private const int LinesPerCore = 32;

        private enum Registers
        {
            RisingTriggerSelection1 = 0x00,         // EXTI_RTSR1
            FallingTriggerSelection1 = 0x04,        // EXTI_FTSR1
            SoftwareInterruptEvent1 = 0x08,         // EXTI_SWIER1
            D3PendingMask1 = 0x0C,                  // EXTI_D3PMR1
            D3PendingClearSelection1Low = 0x10,     // EXTI_D3PCR1L
            D3PendingClearSelection1High = 0x14,    // EXTI_D3PCR1H
            // Intentional gap
            RisingTriggerSelection2 = 0x20,         // EXTI_RTSR2
            FallingTriggerSelection2 = 0x24,        // EXTI_FTSR2
            SoftwareInterruptEvent2 = 0x28,         // EXTI_SWIER2
            D3PendingMask2 = 0x2C,                  // EXTI_D3PMR2
            D3PendingClearSelection2Low = 0x30,     // EXTI_D3PCR2L
            D3PendingClearSelection2High = 0x34,    // EXTI_D3PCR2H
            // Intentional gap
            RisingTriggerSelection3 = 0x40,         // EXTI_RTSR3
            FallingTriggerSelection3 = 0x44,        // EXTI_FTSR3
            SoftwareInterruptEvent3 = 0x48,         // EXTI_SWIER3
            D3PendingMask3 = 0x4C,                  // EXTI_D3PMR3
            D3PendingClearSelection3Low = 0x50,     // EXTI_D3PCR3L
            D3PendingClearSelection3High = 0x54,    // EXTI_D3PCR3H
            // Intentional gap
            InterruptMask1 = 0x80,                  // EXTI_CPUIMR1
            EventMask1 = 0x84,                      // EXTI_CPUEMR1
            Pending1 = 0x88,                        // EXTI_CPUPR1
            // Intentional gap
            InterruptMask2 = 0x90,                  // EXTI_CPUIMR2
            EventMask2 = 0x94,                      // EXTI_CPUEMR2
            Pending2 = 0x98,                        // EXTI_CPUPR2
            // Intentional gap
            InterruptMask3 = 0xA0,                  // EXTI_CPUIMR3
            EventMask3 = 0xA4,                      // EXTI_CPUEMR3
            Pending3 = 0xA8,                        // EXTI_CPUPR3
        }
    }
}
