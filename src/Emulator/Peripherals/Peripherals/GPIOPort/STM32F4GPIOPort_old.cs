//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]

    public class STM32F4GPIOPort : BaseGPIOPort, IDoubleWordPeripheral
    {
        public STM32F4GPIOPort(IMachine machine, uint modeResetValue = 0, uint outputSpeedResetValue = 0, uint pullUpPullDownResetValue = 0) : base(machine, 16)
        {
            this.modeResetValue = modeResetValue;
            this.outputSpeedResetValue = outputSpeedResetValue;
            this.pullUpPullDownResetValue = pullUpPullDownResetValue;
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            uint returnValue = 0;
            switch((Registers)offset)
            {
            case Registers.GPIOx_MODE: // GPIO port mode register
                returnValue = gpiox_mode;
                break;
            case Registers.GPIOx_OTYPER: // GPIO port output type register
                returnValue = gpiox_otyper;
                break;
            case Registers.GPIOx_OSPEEDR: // GPIO port output speed register
                returnValue = gpiox_ospeedr;
                break;
            case Registers.GPIOx_PUPDR: // GPIO port pull-up/pull-down register register
                returnValue = gpiox_pupdr;
                break;
            case Registers.GPIOx_IDR: // GPIO port input data register
                var value = 0u;
                for(var i = 0; i < State.Length; i++)
                {
                    if(State[i])
                    {
                        value |= 1u << i;
                    }
                }
                returnValue = value;
                break;
            case Registers.GPIOx_ODR: // GPIO port output data register
                returnValue = gpiox_odr & 0xFFFF;
                break;
            case Registers.GPIOx_LCKR: // GPIO port lock register
                returnValue = gpiox_lckr & 0x1FFFF;
                break;
            case Registers.GPIOx_BSRR: // GPIO port bit set/reset register
                returnValue = gpiox_afrl;
                break;
            case Registers.GPIOx_AFRL: // GPIO alternate function low register
                returnValue = gpiox_afrh;
                break;
            case Registers.GPIOx_AFRH: // GPIO alternate function high register
                returnValue = 0;
                break;
            default:
                this.LogUnhandledRead(offset);
                returnValue = 0;
                break;
            }
            return returnValue;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.GPIOx_MODE: // GPIO port mode register
                gpiox_mode = value;
                break;
            case Registers.GPIOx_OTYPER: // GPIO port output type register
                gpiox_otyper = value;
                break;
            case Registers.GPIOx_OSPEEDR: // GPIO port output speed register
                gpiox_ospeedr = value;
                break;
            case Registers.GPIOx_PUPDR: // GPIO port pull-up/pull-down register register
                gpiox_pupdr = value;
                break;
            case Registers.GPIOx_IDR: // GPIO port input data register
                break;
            case Registers.GPIOx_ODR: // GPIO port output data register
                gpiox_odr = value;
                SetConnectionsStateUsingBits(gpiox_odr);
                break;
            case Registers.GPIOx_LCKR: // GPIO port lock register
                gpiox_lckr = value;
                break;
            case Registers.GPIOx_BSRR: // GPIO port bit set/reset register
                gpiox_bsrr = value;
                for(var i = 0; i < 16; ++i)
                {
                    if((gpiox_bsrr & 1u << i) != 0)
                    {
                        Connections[i].Set();
                        State[i] = true;
                    }
                }
                for(var i = 16; i < 32; ++i)
                {
                    if((gpiox_bsrr & 1u << i) != 0)
                    {
                        Connections[i - 16].Unset();
                        State[i - 16] = false;
                    }
                }
                break;
            case Registers.GPIOx_AFRL: // GPIO alternate function low register
                gpiox_afrl = value;
                break;
            case Registers.GPIOx_AFRH: // GPIO alternate function high register
                gpiox_afrh = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
            return;
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            Connections[number].Set(value);
        }

        public override void Reset()
        {
            base.Reset();
            gpiox_mode = modeResetValue;
            gpiox_otyper = 0;
            gpiox_ospeedr = outputSpeedResetValue;
            gpiox_pupdr = pullUpPullDownResetValue;
            gpiox_odr = 0;
            gpiox_bsrr = 0;
            gpiox_lckr = 0;
            gpiox_afrl = 0;
            gpiox_afrh = 0;

        }

        private uint gpiox_mode;
        private uint gpiox_otyper;
        private uint gpiox_ospeedr;
        private uint gpiox_pupdr;
        private uint gpiox_odr;
        private uint gpiox_bsrr;
        private uint gpiox_lckr;
        private uint gpiox_afrl;
        private uint gpiox_afrh;

        private readonly uint modeResetValue;
        private readonly uint outputSpeedResetValue;
        private readonly uint pullUpPullDownResetValue;

        // Source: Chapter 7.4 in RM0090 Cortex M4 Reference Manual (Doc ID 018909 Rev 4)
        // for STM32F40xxx, STM32F41xxx, STM32F42xxx, STM32F43xxx advanced ARM-based 32-bit MCUs
        private enum Registers
        {
            GPIOx_MODE      = 0x00, // GPIO port mode register - Read-Write
            GPIOx_OTYPER    = 0x04, // GPIO port output type register - Read-Write
            GPIOx_OSPEEDR   = 0x08, // GPIO port output speed register - Read-Write
            GPIOx_PUPDR     = 0x0C, // GPIO port pull-up/pull-down register - Read-Write
            GPIOx_IDR       = 0x10, // GPIO port input data register - Read-only
            GPIOx_ODR       = 0x14, // GPIO port output data register - Read-Write
            GPIOx_BSRR      = 0x18, // GPIO port bit set/reset register - Write-Only
            GPIOx_LCKR      = 0x1C, // GPIO port configuration lock register - Read-Write
            GPIOx_AFRL      = 0x20, // GPIO alternate function low register - Read-Write
            GPIOx_AFRH      = 0x24  // GPIO alternate function high register - Read-Write
        }
    }
}

