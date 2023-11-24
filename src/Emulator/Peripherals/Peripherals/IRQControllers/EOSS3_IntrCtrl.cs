//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;
using System.Linq;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    /*
     * This peripherals serves a combined function of being both a
     * GPIO controller and a generic interrupt controller/router.
     * For most of the inputs it only forwards the interrupt.
     */
    public sealed class EOSS3_IntrCtrl : BaseGPIOPort, IIRQController, INumberedGPIOOutput, IKnownSize, IDoubleWordPeripheral
    {
        public EOSS3_IntrCtrl(IMachine machine) : base(machine, NumberOfGPIOs)
        {
            gpioManager = new GPIOInterruptManager(GPIOIrq, State);
            gpioManager.DeassertActiveInterruptTrigger = true;

            externalIrqConfig = new [] { SRAMIrq, UARTIrq, TimerIrq, WatchdogIrq,
                WatchdogResetIrq, BusTimeoutIrq, FPUIrq, PacketFIFOIrq, ReservedI2SIrq, ReservedAudioIrq,
                SPIMasterIrq, ConfigDMAIrq, PMUTimerIrq, ADCIrq, RTCIrq, ResetIrq, FFE0Irq, WatchdogFFEIrq,
                ApBootIrq, LDO30Irq, LDO50Irq, ReservedSRAMIrq, LPSDIrq, DMicIrq }.Select((x, i) => new InterruptConfig(x, this, i)).ToArray();

            DebugHelper.Assert(externalIrqConfig.Length == NumberOfOtherInterrupts);

            // SoftwareIrq2 is connected to nvic0, SoftwareIrq1 is connected to nvic1
            softwareInterrupt2Config = new InterruptConfig(SoftwareIrq2, this, 0);
            softwareInterrupt1Config = new InterruptConfig(SoftwareIrq1, this, 1);

            var gpioReg = new DoubleWordRegister(this);
            var gpioRawReg = new DoubleWordRegister(this);
            var gpioTypeReg = new DoubleWordRegister(this);
            var gpioPolarityReg = new DoubleWordRegister(this);
            var gpioEnableM4Reg = new DoubleWordRegister(this);

            for(var j = 0; j < NumberOfGPIOs; j++)
            {
                var i = j;
                gpioManager.PinDirection[i] = GPIOInterruptManager.Direction.Input | GPIOInterruptManager.Direction.Output;
                gpioReg.DefineFlagField(i, writeCallback: (_, value) => { if(value) { gpioManager.ClearInterrupt(i); }},
                    valueProviderCallback: _ => gpioManager.ActiveInterrupts.ElementAt(i),
                    name: $"GPIO_{i}_INTR");
                gpioRawReg.DefineFlagField(i, FieldMode.Read, valueProviderCallback: _ => State[i], name: $"GPIO_{i}_INTR_RAW");
                gpioTypeReg.DefineFlagField(i, writeCallback: (_, value) => gpioManager.InterruptType[i] = UpdateGPIOSettings(gpioManager.InterruptType[i], value, null),
                    valueProviderCallback: _ =>
                        gpioManager.InterruptType[i] == GPIOInterruptManager.InterruptTrigger.RisingEdge
                        || gpioManager.InterruptType[i] == GPIOInterruptManager.InterruptTrigger.FallingEdge,
                    name: $"GPIO_{i}_INTR_TYPE");
                gpioPolarityReg.DefineFlagField(i, writeCallback: (_, value) => gpioManager.InterruptType[i] = UpdateGPIOSettings(gpioManager.InterruptType[i], null, value),
                    valueProviderCallback: _ =>
                        gpioManager.InterruptType[i] == GPIOInterruptManager.InterruptTrigger.RisingEdge
                        || gpioManager.InterruptType[i] == GPIOInterruptManager.InterruptTrigger.ActiveHigh,
                    name: $"GPIO_{i}_INTR_POL");
                gpioEnableM4Reg.DefineFlagField(i, writeCallback: (_, value) => gpioManager.InterruptEnable[i] = value, valueProviderCallback: _ => gpioManager.InterruptEnable[i], name: $"GPIO_{i}_INTR_EN_M4");
            }

            var otherInterruptsReg = new DoubleWordRegister(this);
            var otherInterruptsEnabledM4Reg = new DoubleWordRegister(this);
            for(var j = 0; j < NumberOfOtherInterrupts; ++j)
            {
                var i = j;
                otherInterruptsReg.DefineFlagField(i, valueProviderCallback: _ => externalIrqConfig[i].Active,
                    writeCallback: (_, value) => { if(value) { externalIrqConfig[i].Active = false; } }, name: $"OTHER_INTR[{i}]");

                externalIrqConfig[i].EnabledField = otherInterruptsEnabledM4Reg.DefineFlagField(i, changeCallback: (_, __) => externalIrqConfig[i].Update(), name: $"OTHER_INTR_EN_M4[{i}]");
            }
            var regs = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.GPIOInterrupt, gpioReg},
                {(long)Registers.GPIOInterruptRaw, gpioRawReg},
                {(long)Registers.GPIOInterruptType, gpioTypeReg},
                {(long)Registers.GPIOInterruptPolarity, gpioPolarityReg},
                {(long)Registers.GPIOInterruptEnableM4, gpioEnableM4Reg},

                {(long)Registers.OtherInterrupts, otherInterruptsReg},
                {(long)Registers.OtherInterruptsEnableM4, otherInterruptsEnabledM4Reg},

                {(long)Registers.SoftwareInterrupt1, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, value) => softwareInterrupt1Config.Active = value,
                        valueProviderCallback: _ => softwareInterrupt1Config.Active,
                        name: "SW_INTR_1")},
                {(long)Registers.SoftwareInterrupt1EnableM4, new DoubleWordRegister(this)
                    .WithFlag(0, out var software1Enabled, changeCallback: (_, __) => softwareInterrupt1Config.Update(), name: "SW_INTR_1_EN_M4")
                },
                {(long)Registers.SoftwareInterrupt2, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, value) => softwareInterrupt2Config.Active = value,
                        valueProviderCallback: _ => softwareInterrupt2Config.Active,
                        name: "SW_INTR_2")
                },
                {(long)Registers.SoftwareInterrupt2EnableM4, new DoubleWordRegister(this)
                    .WithFlag(0, out var software2Enabled, changeCallback: (_, __) => softwareInterrupt2Config.Update(), name: "SW_INTR_2_EN_M4")
                },
            };
            softwareInterrupt1Config.EnabledField = software1Enabled;
            softwareInterrupt2Config.EnabledField = software2Enabled;
            registers = new DoubleWordRegisterCollection(this, regs);

            var miscRegs = new Dictionary<long, DoubleWordRegister>
            {
                {(long)MiscRegisters.IOInput, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State), name: "IO_IN")
                    .WithReservedBits(8, 24)
                },
                {(long)MiscRegisters.IOOutput, new DoubleWordRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Select(x => x.Value.IsSet)), writeCallback: (_, value) => UpdateOutputBits((byte)value), name: "IO_OUT")
                    .WithReservedBits(8, 24)
                },
            };
            miscRegisters = new DoubleWordRegisterCollection(this, miscRegs);
        }

        public override void OnGPIO(int number, bool value)
        {
            // Input mapping:
            // 0-5 - reserved
            // 6-29 - peripheral interrupts, ordered according to the externalIrqConfig collection
            // 30-37 - GPIOs 0-7
            this.Log(LogLevel.Noisy, "Received interrupt {0}, value {1}", number, value);
            //"Other", non-gpio interrupt
            if(number >= NumberOfOtherInterrupts + FirstExternalInterrupt + NumberOfGPIOs || number < FirstExternalInterrupt)
            {
                this.Log(LogLevel.Error, "Received an out-of-range interrupt/GPIO. Number: {0}, value: {1}. External interrupts must be connected to pins between {2} and {3}, inclusive.",
                    number, value, FirstExternalInterrupt, NumberOfOtherInterrupts + FirstExternalInterrupt + NumberOfGPIOs);
                return;
            }
            if(number >= NumberOfOtherInterrupts + FirstExternalInterrupt)
            {
                base.OnGPIO(number - NumberOfOtherInterrupts - FirstExternalInterrupt, value);
                gpioManager.RefreshInterrupts();
                return;
            }
            var config = externalIrqConfig[number - FirstExternalInterrupt];
            config.Detected = value;
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            miscRegisters.Reset();
            foreach(var interruptConfig in externalIrqConfig)
            {
                interruptConfig.Reset();
            }
            softwareInterrupt1Config.Reset();
            softwareInterrupt2Config.Reset();
            gpioManager.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        [ConnectionRegionAttribute("misc")]
        public uint ReadDoubleWordFromMisc(long offset)
        {
            return miscRegisters.Read(offset);
        }

        [ConnectionRegionAttribute("misc")]
        public void WriteDoubleWordToMisc(long offset, uint value)
        {
            miscRegisters.Write(offset, value);
        }

        [ConnectionRegionAttribute("iomux")]
        public uint ReadDoubleWordFromIOMux(long offset)
        {
            this.Log(LogLevel.Warning, "Read from unsupported iomux, offset 0x{0:X}", offset);
            return 0;
        }

        [ConnectionRegionAttribute("iomux")]
        public void WriteDoubleWordToIOMux(long offset, uint value)
        {
            this.Log(LogLevel.Warning, "Write to unsupported iomux, offset 0x{0:X} value 0x{1:X}", offset, value);
        }

        public long Size => 0x400;

        public GPIO SoftwareIrq2 { get; } = new GPIO();
        public GPIO SoftwareIrq1 { get; } = new GPIO();
        public GPIO FFE0MessageIrq { get; } = new GPIO();
        public GPIO FabricIrq { get; } = new GPIO();
        public GPIO GPIOIrq { get; } = new GPIO();
        public GPIO SRAMIrq { get; } = new GPIO();
        public GPIO UARTIrq { get; } = new GPIO();
        public GPIO TimerIrq { get; } = new GPIO();
        public GPIO WatchdogIrq { get; } = new GPIO();
        public GPIO WatchdogResetIrq { get; } = new GPIO();
        public GPIO BusTimeoutIrq { get; } = new GPIO();
        public GPIO FPUIrq { get; } = new GPIO();
        public GPIO PacketFIFOIrq { get; } = new GPIO();
        public GPIO ReservedI2SIrq { get; } = new GPIO();
        public GPIO ReservedAudioIrq { get; } = new GPIO();
        public GPIO SPIMasterIrq { get; } = new GPIO();
        public GPIO ConfigDMAIrq { get; } = new GPIO();
        public GPIO PMUTimerIrq { get; } = new GPIO();
        public GPIO ADCIrq { get; } = new GPIO();
        public GPIO RTCIrq { get; } = new GPIO();
        public GPIO ResetIrq { get; } = new GPIO();
        public GPIO FFE0Irq { get; } = new GPIO();
        public GPIO WatchdogFFEIrq { get; } = new GPIO();
        public GPIO ApBootIrq { get; } = new GPIO();
        public GPIO LDO30Irq { get; } = new GPIO();
        public GPIO LDO50Irq { get; } = new GPIO();
        public GPIO ReservedSRAMIrq { get; } = new GPIO();
        public GPIO LPSDIrq { get; } = new GPIO();
        public GPIO DMicIrq { get; } = new GPIO();

        private void UpdateOutputBits(byte value)
        {
            for(byte i = 0; i < NumberOfGPIOs; ++i)
            {
                Connections[i].Set(BitHelper.IsBitSet(value, i));
            }
        }

        private GPIOInterruptManager.InterruptTrigger UpdateGPIOSettings(GPIOInterruptManager.InterruptTrigger oldTrigger, bool? type, bool? polarity)
        {
            if(!(type.HasValue ^ polarity.HasValue))
            {
                throw new ArgumentException($"Either {nameof(type)} or {nameof(polarity)} must be null. The other must not be null.");
            }
            var isEdge = oldTrigger != GPIOInterruptManager.InterruptTrigger.ActiveHigh && oldTrigger != GPIOInterruptManager.InterruptTrigger.ActiveLow;
            var isHigh = oldTrigger != GPIOInterruptManager.InterruptTrigger.ActiveLow && oldTrigger != GPIOInterruptManager.InterruptTrigger.FallingEdge; //both edges is not an option in this controller

            if(type.HasValue)
            {
                if(isHigh)
                {
                    return type.Value ? GPIOInterruptManager.InterruptTrigger.RisingEdge : GPIOInterruptManager.InterruptTrigger.ActiveHigh;
                }
                else
                {
                    return type.Value ? GPIOInterruptManager.InterruptTrigger.FallingEdge : GPIOInterruptManager.InterruptTrigger.ActiveLow;
                }
            }
            else //polarity has value
            {
                if(isEdge)
                {
                    return polarity.Value ? GPIOInterruptManager.InterruptTrigger.RisingEdge : GPIOInterruptManager.InterruptTrigger.FallingEdge;
                }
                else
                {
                    return polarity.Value ? GPIOInterruptManager.InterruptTrigger.ActiveHigh : GPIOInterruptManager.InterruptTrigger.ActiveLow;
                }
            }
        }

        private readonly InterruptConfig[] externalIrqConfig;
        private readonly InterruptConfig softwareInterrupt1Config;
        private readonly InterruptConfig softwareInterrupt2Config;
        private readonly DoubleWordRegisterCollection registers;
        private readonly DoubleWordRegisterCollection miscRegisters;
        private readonly GPIOInterruptManager gpioManager;

        private const int NumberOfGPIOs = 8;
        private const int NumberOfOtherInterrupts = 24;
        private const int FirstExternalInterrupt = 6;

        private class InterruptConfig
        {
            public InterruptConfig(GPIO irq, EOSS3_IntrCtrl parent, int number)
            {
                Interrupt = irq;
                this.parent = parent;
                this.number = number;
            }

            public void Update()
            {
                Interrupt.Set(Active && EnabledField.Value);
                parent.Log(LogLevel.Noisy, "Interrupt {3}: active {0}, enabled {1}, interrupt set to {2}", Active, EnabledField.Value, Interrupt.IsSet, number);
            }

            public void Reset()
            {
                active = false;
                detected = false;
                Interrupt.Unset();
            }

            public GPIO Interrupt { get; }

            public bool Detected
            {
                get => detected;
                set
                {
                    parent.Log(LogLevel.Noisy, "Interrupt {1}: setting detected to {0}", value, number);
                    detected = value;
                    if(value)
                    {
                        Active = true;
                    }
                }
            }

            public bool Active
            {
                get => active;
                set
                {
                    parent.Log(LogLevel.Noisy, "Interrupt {1}: setting active to {0}", value, number);
                    if(!value && !Detected)
                    {
                        active = false;
                    }
                    else
                    {
                        active = true;
                    }
                    Update();
                }
            }

            public IFlagRegisterField EnabledField
            {
                private get; set;
            }
            private bool detected;
            private bool active;
            private readonly EOSS3_IntrCtrl parent;
            private readonly int number;
        }

        private enum Registers
        {
            GPIOInterrupt = 0x0,
            GPIOInterruptRaw = 0x4,
            GPIOInterruptType = 0x8,
            GPIOInterruptPolarity = 0xC,
            GPIOInterruptEnableAP = 0x10,
            GPIOInterruptEnableM4 = 0x14,
            GPIOInterruptEnableFFE0 = 0x18,
            GPIOInterruptEnableFFE1 = 0x1C,
            /* 0x20-0x2c not defined */
            OtherInterrupts = 0x30,
            OtherInterruptsEnableAP = 0x34,
            OtherInterruptsEnableM4 = 0x38,
            /* 0x3c not defined */
            SoftwareInterrupt1 = 0x40,
            SoftwareInterrupt1EnableAP = 0x44,
            SoftwareInterrupt1EnableM4 = 0x48,
            /* 0x4c not defined */
            SoftwareInterrupt2 = 0x50,
            SoftwareInterrupt2EnableAP = 0x54,
            SoftwareInterrupt2EnableM4 = 0x58,
            /* 0x5c not defined */
            FFEInterrupt = 0x60,
            FFEInterruptEnableAP = 0x64,
            FFEInterruptEnableM4 = 0x68,
            /* 0x6c-0x7c not defined */
            FabricInterrupt = 0x80,
            FabricInterruptRaw = 0x84,
            FabricInterruptType = 0x88,
            FabricInterruptPolarity = 0x8C,
            FabricInterruptEnableAP = 0x90,
            FabricInterruptEnableM4 = 0x94,
            /* 0x94-0x9C not defined */
            M4MemoryAlwaysOnInterrupt = 0xA0,
            M4MemoryAlwaysOnInterruptEnable = 0xA4,
        }

        private enum MiscRegisters
        {
            IOInput = 0,
            IOOutput = 4,
        }
    }
}
