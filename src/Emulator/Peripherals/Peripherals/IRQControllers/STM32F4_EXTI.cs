//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32F4_EXTI : BasicDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public STM32F4_EXTI(IMachine machine, int numberOfOutputLines = 14, int firstDirectLine = DefaultFirstDirectLine) : base(machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            // All lines lower than firstDirectLine are configurable so they should be masked
            // treatOutOfRangeLinesAsDirect is set to true to preserve backwards compatibility
            core = new STM32_EXTICore(this, BitHelper.CalculateQuadWordMask(firstDirectLine, 0), treatOutOfRangeLinesAsDirect: true, allowMaskingDirectLines: false);

            numberOfLinesMask = BitHelper.CalculateQuadWordMask((int)NumberOfLines, 0);

            DefineRegisters();
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= NumberOfLines)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0; {1})", number, NumberOfLines);
                return;
            }
            var lineNumber = (byte)number;

            if(core.CanSetInterruptValue(lineNumber, value, out var isLineConfigurable))
            {
                // Configurable lines can only be set in this place.
                value = isLineConfigurable ? true : value;
                core.UpdatePendingValue(lineNumber, value);
                Connections[number].Set(value);
            }
        }

        public override void Reset()
        {
            base.Reset();
            softwareInterrupt = 0;
            foreach(var gpio in Connections)
            {
                gpio.Value.Unset();
            }
        }

        public long Size => 0x400;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long NumberOfLines => Connections.Count;

        private void DefineRegisters()
        {
            Registers.InterruptMask.Define(this)
                .WithValueField(0, 32, out core.InterruptMask, name: "IMR");

            // Blank implementation to preserve backwards compatibility with the previous version of this model
            Registers.EventMask.Define(this)
                .WithValueField(0, 32, name: "EMR");

            Registers.RisingTriggerSelection.Define(this)
                .WithValueField(0, 32, out core.RisingEdgeMask, name: "RTSR");

            Registers.FallingTriggerSelection.Define(this)
                .WithValueField(0, 32, out core.FallingEdgeMask, name: "FTSR");

            Registers.SoftwareInterruptEvent.Define(this)
                .WithValueField(0, 32, name: "SWIER", valueProviderCallback: _ => softwareInterrupt,
                    writeCallback: (_, value) =>
                    {
                        value &= numberOfLinesMask;
                        BitHelper.ForeachActiveBit(value & core.InterruptMask.Value, x => Connections[x].Set());
                    });

            Registers.PendingRegister.Define(this)
                .WithValueField(0, 32, out core.PendingInterrupts, FieldMode.Read | FieldMode.WriteOneToClear, name: "PR",
                    writeCallback: (_, value) =>
                    {
                        softwareInterrupt &= ~value;
                        value &= numberOfLinesMask;
                        BitHelper.ForeachActiveBit(value, x => Connections[x].Unset());
                    });
        }

        // We treat lines above 23 as direct by default for backwards compatibility with
        // the old behavior of the EXTI model.
        protected const int DefaultFirstDirectLine = 23;

        private ulong softwareInterrupt;

        private readonly ulong numberOfLinesMask;
        private readonly STM32_EXTICore core;

        private enum Registers
        {
            InterruptMask = 0x0,
            EventMask = 0x4,
            RisingTriggerSelection = 0x8,
            FallingTriggerSelection = 0xC,
            SoftwareInterruptEvent = 0x10,
            PendingRegister = 0x14
        }
    }
}
