//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32WBA_EXTI : BasicDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public STM32WBA_EXTI(Machine machine, int numberOfOutputLines): base(machine)
        {
            this.numberOfLines = numberOfOutputLines;
            core = new STM32_EXTICore(this, lineConfigurableMask: 0x1FFFF, separateConfigs: true);
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            DefineRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= numberOfLines)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0])", number);
                return;
            }

            if(core.CanSetInterruptValue((byte)number, value, out var _))
            {
                core.UpdatePendingValue((byte)number, true);
                Connections[number].Set(true);
            }
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            RegistersCollection.DefineRegister((long)Registers.RaisingTriggerSelection)
                .WithValueField(0, 17, out core.RisingEdgeMask, name: "RT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerSelection)
                .WithValueField(0, 17, out core.FallingEdgeMask, name: "FT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.SoftwareInterruptEvent)
                .WithValueField(0, 32, name: "SWIER", changeCallback: (_, value) =>
                {
                    BitHelper.ForeachActiveBit(value & core.InterruptMask.Value, bit =>
                    {
                        Connections[bit].Set();
                    });
                });

            RegistersCollection.DefineRegister((long)Registers.RaisingTriggerPending)
                .WithValueField(0, numberOfLines, out core.PendingRaisingInterrupts,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()), name: "RPIF");

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerPending)
                .WithValueField(0, numberOfLines, out core.PendingFallingInterrupts,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()), name: "FPIF");

            RegistersCollection.DefineRegister((long)Registers.SecurityConfiguration);

            RegistersCollection.DefineRegister((long)Registers.PrivilegeConfiguration);

            // For now there is no way we can tell where did the signal came from - no way to implement this
            // However, we define the fields as value fields to handle software that expects to be able to
            // read back the last written value.
            for(var registerIndex = 0; registerIndex < InterruptSelectionRegistersCount; registerIndex++)
            {
                var firstOffset = registerIndex * 4;
                RegistersCollection.DefineRegister((long)Registers.ExternalInterruptSelection1 + firstOffset)
                    .WithValueField(0, 8, name: $"EXTI{firstOffset + 0}")
                    .WithValueField(8, 8, name: $"EXTI{firstOffset + 1}")
                    .WithValueField(16, 8, name: $"EXTI{firstOffset + 2}")
                    .WithValueField(24, 8, name: $"EXTI{firstOffset + 3}");
            }

            RegistersCollection.DefineRegister((long)Registers.Lock);

            RegistersCollection.DefineRegister((long)Registers.WakeUpInterruptMask);

            RegistersCollection.DefineRegister((long)Registers.WakeUpEventMask);
        }

        private readonly STM32_EXTICore core;
        private readonly int numberOfLines;

        private const uint InterruptSelectionRegistersCount = 4;

        private enum Registers
        {
            RaisingTriggerSelection     = 0x0,  // EXTI_RTSR1
            FallingTriggerSelection     = 0x4,  // EXTI_FTSR1
            SoftwareInterruptEvent      = 0x8,  // EXTI_SWIER1
            RaisingTriggerPending       = 0xc,  // EXTI_RPR1
            FallingTriggerPending       = 0x10, // EXTI_FPR1
            SecurityConfiguration       = 0x14, // EXTI_SECCFGR1
            PrivilegeConfiguration      = 0x18, // EXTI_PRIVCFGR1
            ExternalInterruptSelection1 = 0x60, // EXTI_EXTICR1
            ExternalInterruptSelection2 = 0x64, // EXTI_EXTICR2
            ExternalInterruptSelection3 = 0x68, // EXTI_EXTICR3
            ExternalInterruptSelection4 = 0x6c, // EXTI_EXTICR4
            Lock                        = 0x70, // EXTI_LOCKR
            WakeUpInterruptMask         = 0x80, // EXTI_IMR1
            WakeUpEventMask             = 0x84, // EXTI_EMR1
        }
    }
}
