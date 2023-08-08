//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.X86;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class Quark_SystemControlSubsystem : IDoubleWordPeripheral
    {
        public Quark_SystemControlSubsystem(IMachine machine, Quark_GPIOController gpioPort)
        {
            this.gpioPort = gpioPort;
            this.alwaysOnCounter = new LimitTimer(machine.ClockSource, 32000, this, nameof(alwaysOnCounter), direction: Time.Direction.Ascending, enabled: true);

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.HybridOscillatorStatus1, new DoubleWordRegister(this, 3)}, //use only reset value - means that oscillators are enabled
                {(long)Registers.AlwaysOnCounter, new DoubleWordRegister(this, 0).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => (uint)alwaysOnCounter.Value)},

                // These registers map directly to GPIO port. Only 6 LSBits are important. Offsets in SCSS are generally the same as in the gpio port + 0xB00,
                // but we keep it here for clarity, logging purposes and ease of defining field modes.
                {(long)Registers.PortAGPIOAlwaysOn, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.PortAData)},
                {(long)Registers.PortAGPIOAlwaysOnDirection, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.PortADataDirection)},
                {(long)Registers.InterruptEnable, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptEnable)},
                {(long)Registers.InterruptMask, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptMask)},
                {(long)Registers.InterruptType, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptType)},
                {(long)Registers.InterruptPolarity, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptPolarity)},
                {(long)Registers.InterruptStatus, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptStatus, FieldMode.Read)},
                {(long)Registers.RawInterruptStatus, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.RawInterruptStatus, FieldMode.Read)},
                {(long)Registers.ClearInterrupt, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.ClearInterrupt)},
                {(long)Registers.PortAExternalPort, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.PortAExternalPort, FieldMode.Read)},
                {(long)Registers.InterruptBothEdgeType, CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers.InterruptBothEdgeType)},
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            alwaysOnCounter.Reset();
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        private DoubleWordRegister CreateAlwaysOnGPIORegister(Quark_GPIOController.Registers register, FieldMode fieldMode = FieldMode.Read | FieldMode.Write)
        {
            if(fieldMode.IsWritable())
            {
                return new DoubleWordRegister(this, 0)
                    .WithValueField(0, NumberOfGPIOs, fieldMode, writeCallback: (_, value) =>
                        {
                            var currentState = gpioPort.ReadDoubleWord((uint)register);
                            // no need to filter value with & AlwaysOnGPIOMask, as it is already filtered
                            gpioPort.WriteDoubleWord((uint)register, (currentState & ~AlwaysOnGPIOMask) | (uint)value);
                        }, valueProviderCallback: _ =>
                        {
                            return gpioPort.ReadDoubleWord((uint)register);
                        });
            }
            else
            {
                return new DoubleWordRegister(this, 0)
                    .WithValueField(0, NumberOfGPIOs, fieldMode, valueProviderCallback: _ =>
                        {
                            return gpioPort.ReadDoubleWord((uint)register);
                        });
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly LimitTimer alwaysOnCounter;
        private readonly Quark_GPIOController gpioPort;

        private const int NumberOfGPIOs = 6;
        private const uint AlwaysOnGPIOMask = (1 << NumberOfGPIOs) - 1;

        private enum Registers
        {
            HybridOscillatorStatus1 = 0x4,
            AlwaysOnCounter = 0x700,
            PortAGPIOAlwaysOn = 0xB00,
            PortAGPIOAlwaysOnDirection = 0xB04,
            InterruptEnable = 0xB30,
            InterruptMask = 0xB34,
            InterruptType = 0xB38,
            InterruptPolarity = 0xB3C,
            InterruptStatus = 0xB40,
            RawInterruptStatus = 0xB44,
            DebounceEnable = 0xB48,
            ClearInterrupt = 0xB4C,
            PortAExternalPort = 0xB50,
            SynchronizationLevel = 0xB60,
            InterruptBothEdgeType = 0xB68,
        }
    }
}
