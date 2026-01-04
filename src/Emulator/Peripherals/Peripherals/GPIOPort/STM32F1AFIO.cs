//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32F1AFIO : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F1AFIO(Machine machine, STM32F1GPIOPort[] gpioPorts) : base(machine)
        {
            this.gpioPortA = gpioPorts[0];
            this.gpioPortB = gpioPorts[1];
            this.gpioPortC = gpioPorts[2];
            this.gpioPortD = gpioPorts[3];
            this.gpioPortE = gpioPorts[4];
            this.gpioPortF = gpioPorts[5];
            this.gpioPortG = gpioPorts[6];

            // TIM1 alternate function remapping (Table 46, page 179/1136 in RM0008 Rev 21)
            timerGpioRemap[1] = new[]
            {
                // TIM1_REMAP value equal to 0b00
                new GpioPinCollection {{gpioPortA, 8}, {gpioPortA, 9}, {gpioPortA, 10}, {gpioPortA, 11}, {gpioPortB, 13}, {gpioPortB, 14}, {gpioPortB, 15}},
                // TIM1_REMAP value equal to 0b01
                new GpioPinCollection {{gpioPortA, 8}, {gpioPortA, 9}, {gpioPortA, 10}, {gpioPortA, 11}, {gpioPortA, 7}, {gpioPortB, 0}, {gpioPortB, 1}},
                // TIM1_REMAP value equal to 0b10
                new GpioPinCollection {},
                // TIM1_REMAP value equal to 0b11
                new GpioPinCollection {{gpioPortE, 9}, {gpioPortE, 11}, {gpioPortE, 13}, {gpioPortE, 14}, {gpioPortE, 8}, {gpioPortB, 10}, {gpioPortB, 12}}
            };

            // TIM2 alternate function remapping (Table 45, page 179/1136 in RM0008 Rev 21)
            timerGpioRemap[2] = new[]
            {
                // Same pattern as above
                new GpioPinCollection {{gpioPortA, 0}, {gpioPortA, 1}, {gpioPortA, 2}, {gpioPortA, 3}},
                new GpioPinCollection {{gpioPortA, 15}, {gpioPortB, 3}, {gpioPortA, 2}, {gpioPortA, 3}},
                new GpioPinCollection {{gpioPortA, 0}, {gpioPortA, 1}, {gpioPortB, 10}, {gpioPortB, 11}},
                new GpioPinCollection {{gpioPortA, 15}, {gpioPortB, 3}, {gpioPortB, 10}, {gpioPortB, 11}},
            };

            // Table 44
            timerGpioRemap[3] = new[]
            {
                new GpioPinCollection {{gpioPortA, 6}, {gpioPortA, 7}, {gpioPortB, 0}, {gpioPortB, 1}},
                new GpioPinCollection {},
                new GpioPinCollection {{gpioPortB, 4}, {gpioPortB, 5}, {gpioPortB, 0}, {gpioPortB, 1}},
                new GpioPinCollection {{gpioPortC, 6}, {gpioPortC, 7}, {gpioPortC, 8}, {gpioPortC, 9}},
            };

            // Table 43
            timerGpioRemap[4] = new[]
            {
                new GpioPinCollection {{gpioPortB, 6}, {gpioPortB, 7}, {gpioPortB, 8}, {gpioPortB, 9}},
                new GpioPinCollection {{gpioPortD, 12}, {gpioPortD, 13}, {gpioPortD, 14}, {gpioPortD, 15}},
            };

            // Table 42
            timerGpioRemap[5] = new[]
            {
                new GpioPinCollection {{gpioPortA, 3}},
                new GpioPinCollection {},
            };

            // Table 47
            timerGpioRemap[9] = new[]
            {
                new GpioPinCollection {{gpioPortA, 2}, {gpioPortA, 3}},
                new GpioPinCollection {{gpioPortE, 5}, {gpioPortE, 6}}
            };

            // Table 48
            timerGpioRemap[10] = new[]
            {
                new GpioPinCollection {{gpioPortB, 8}},
                new GpioPinCollection {{gpioPortF, 6}}
            };

            // Table 49
            timerGpioRemap[11] = new[]
            {
                new GpioPinCollection {{gpioPortB, 9}},
                new GpioPinCollection {{gpioPortF, 7}}
            };

            // Table 50
            timerGpioRemap[13] = new[]
            {
                new GpioPinCollection {{gpioPortA, 6}},
                new GpioPinCollection {{gpioPortF, 8}}
            };

            // Table 51
            timerGpioRemap[14] = new[]
            {
                new GpioPinCollection {{gpioPortA, 7}},
                new GpioPinCollection {{gpioPortF, 9}}
            };

            RegistersCollection.AddRegister((long)Registers.EventControl, new DoubleWordRegister(this)
                .WithTag("PIN", 0, 3)
                .WithTag("Port", 3, 3)
                .WithTaggedFlag("EVOE", 6)
                .WithReservedBits(7, 25));
            RegistersCollection.AddRegister((long)Registers.Map1, new DoubleWordRegister(this)
                .WithTaggedFlag("SPI1_REMAP", 0)
                .WithTaggedFlag("I2C1_REMAP", 1)
                .WithTaggedFlag("USART1_REMAP", 2)
                .WithTaggedFlag("USART2_REMAP", 3)
                .WithTag("USART3_REMAP", 4, 2)
                .WithValueField(6, 2, name: "TIM1_REMAP", writeCallback: (_, value) => TimRemapCallback(1, value), valueProviderCallback: _ => timRemaps[1])
                .WithValueField(8, 2, name: "TIM2_REMAP", writeCallback: (_, value) => TimRemapCallback(2, value), valueProviderCallback: _ => timRemaps[2])
                .WithValueField(10, 2, name: "TIM3_REMAP", writeCallback: (_, value) => TimRemapCallback(3, value), valueProviderCallback: _ => timRemaps[3])
                .WithFlag(12, name: "TIM4_REMAP", writeCallback: (_, value) => TimRemapCallback(4, value ? 1ul : 0ul), valueProviderCallback: _ => timRemaps[4] != 0)
                .WithTag("CAN1_REMAP", 13, 2)
                .WithTaggedFlag("PD01_REMAP", 15)
                .WithTaggedFlag("TIM5CH4_IREMAP", 16)
                .WithTaggedFlag("ADC1_ETRGINJ_REMAP", 17)
                .WithTaggedFlag("ADC1_ETRGREG_REMAP", 18)
                .WithTaggedFlag("ADC2_ETRGINJ_REMAP", 19)
                .WithTaggedFlag("ADC2_ETRGREG_REMAP", 20)
                .WithTaggedFlag("ETH_REMAP", 21)
                .WithTaggedFlag("CAN2_REMAP", 22)
                .WithTaggedFlag("MII_RMII_SEL", 23)
                .WithTag("SWJ_CFG", 24, 3)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("SPI3_REMAP", 28)
                .WithTaggedFlag("TIM2ITR1_REMAP", 29)
                .WithTaggedFlag("PTP_PPS_REMAP", 30)
                .WithReservedBits(31, 1));
            RegistersCollection.AddRegister((long)Registers.ExternalInterrupt1, new DoubleWordRegister(this)
                .WithTag("EXTI0", 0, 4)
                .WithTag("EXTI1", 4, 4)
                .WithTag("EXTI2", 8, 4)
                .WithTag("EXTI3", 12, 4)
                .WithReservedBits(16, 16));
            RegistersCollection.AddRegister((long)Registers.ExternalInterrupt2, new DoubleWordRegister(this)
                .WithTag("EXTI4", 0, 4)
                .WithTag("EXTI5", 4, 4)
                .WithTag("EXTI6", 8, 4)
                .WithTag("EXTI7", 12, 4)
                .WithReservedBits(16, 16));
            RegistersCollection.AddRegister((long)Registers.ExternalInterrupt3, new DoubleWordRegister(this)
                .WithTag("EXTI8", 0, 4)
                .WithTag("EXTI9", 4, 4)
                .WithTag("EXTI10", 8, 4)
                .WithTag("EXTI11", 12, 4)
                .WithReservedBits(16, 16));
            RegistersCollection.AddRegister((long)Registers.ExternalInterrupt4, new DoubleWordRegister(this)
                .WithTag("EXTI12", 0, 4)
                .WithTag("EXTI13", 4, 4)
                .WithTag("EXTI14", 8, 4)
                .WithTag("EXTI15", 12, 4)
                .WithReservedBits(16, 16));
            RegistersCollection.AddRegister((long)Registers.Map2, new DoubleWordRegister(this)
                .WithFlag(0, name: "TIM15_REMAP", writeCallback: (_, value) => TimRemapCallback(15, value), valueProviderCallback: _ => timRemaps[15] != 0)
                .WithFlag(1, name: "TIM16_REMAP", writeCallback: (_, value) => TimRemapCallback(16, value), valueProviderCallback: _ => timRemaps[16] != 0)
                .WithFlag(2, name: "TIM17_REMAP", writeCallback: (_, value) => TimRemapCallback(17, value), valueProviderCallback: _ => timRemaps[17] != 0)
                .WithTaggedFlag("CEC_REMAP", 3)
                .WithTaggedFlag("TIM1_DMA_REMAP", 4)
                .WithFlag(5, name: "TIM9_REMAP", writeCallback: (_, value) => TimRemapCallback(9, value), valueProviderCallback: _ => timRemaps[9] != 0)
                .WithFlag(6, name: "TIM10_REMAP", writeCallback: (_, value) => TimRemapCallback(10, value), valueProviderCallback: _ => timRemaps[10] != 0)
                .WithFlag(7, name: "TIM11_REMAP", writeCallback: (_, value) => TimRemapCallback(11, value), valueProviderCallback: _ => timRemaps[11] != 0)
                .WithFlag(8, name: "TIM13_REMAP", writeCallback: (_, value) => TimRemapCallback(13, value), valueProviderCallback: _ => timRemaps[13] != 0)
                .WithFlag(9, name: "TIM14_REMAP", writeCallback: (_, value) => TimRemapCallback(14, value), valueProviderCallback: _ => timRemaps[14] != 0)
                .WithTaggedFlag("FSMC_NADV", 10)
                .WithTaggedFlag("TIM67_DAC_DMA_REMAP", 11)
                .WithFlag(12, name: "TIM12_REMAP", writeCallback: (_, value) => TimRemapCallback(12, value), valueProviderCallback: _ => timRemaps[12] != 0)
                .WithTaggedFlag("MISC_REMAP", 13)
                .WithReservedBits(14, 18));

            Reset();
        }

        public long Size => 0x400;

        private void TimRemapCallback(ulong timer, ulong newValue)
        {
            var oldValue = timRemaps[timer];
            timRemaps[timer] = newValue;

            var oldPins = timerGpioRemap[timer][oldValue];
            var newPins = timerGpioRemap[timer][newValue];

            foreach(var gpioPin in oldPins)
            {
                var gpio = gpioPin.Gpio;
                var pin = gpioPin.Pin;
                gpio.AlternateFunctionOutputs[pin].ActiveFunction = 0;
            }

            foreach(var gpioPin in newPins)
            {
                var gpio = gpioPin.Gpio;
                var pin = gpioPin.Pin;
                gpio.AlternateFunctionOutputs[pin].ActiveFunction = timer;
            }
        }

        private void TimRemapCallback(ulong timer, bool newValue)
        {
            TimRemapCallback(timer, newValue ? 1ul : 0ul);
        }

        // Timer GPIO remapping indexed by AFIO remap register values.
        // timerGpioRemap[timer_num][remap_value] = list of GPIO pins connected to that timer
        private readonly Dictionary<ulong, GpioPinCollection[]> timerGpioRemap =
            new Dictionary<ulong, GpioPinCollection[]>();

        private readonly STM32F1GPIOPort gpioPortA;
        private readonly STM32F1GPIOPort gpioPortB;
        private readonly STM32F1GPIOPort gpioPortC;
        private readonly STM32F1GPIOPort gpioPortD;
        private readonly STM32F1GPIOPort gpioPortE;
        private readonly STM32F1GPIOPort gpioPortF;
        private readonly STM32F1GPIOPort gpioPortG;
        private static readonly int NoTimers = 18;
        private readonly ulong[] timRemaps = new ulong[NoTimers];

        private class GpioPinCollection : List<GpioPin>
        {
            public void Add(STM32F1GPIOPort gpio, int pin)
            {
                Add(new GpioPin(gpio, pin));
            }
        }

        private struct GpioPin
        {
            public STM32F1GPIOPort Gpio { get; }

            public int Pin { get; }

            public GpioPin(STM32F1GPIOPort gpio, int pin)
            {
                Gpio = gpio;
                Pin = pin;
            }

            public void Deconstruct(out STM32F1GPIOPort gpio, out int pin)
            {
                gpio = Gpio;
                pin = Pin;
            }
        }

        private enum Registers
        {
            EventControl = 0x0,
            Map1 = 0x4,
            ExternalInterrupt1 = 0x8,
            ExternalInterrupt2 = 0xc,
            ExternalInterrupt3 = 0x10,
            ExternalInterrupt4 = 0x14,
            Map2 = 0x1c,
        }
    }
}