//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class STM32_GPIOPort : BaseGPIOPort, IDoubleWordPeripheral
    {
        public STM32_GPIOPort(Machine machine, uint modeResetValue = 0, uint outputSpeedResetValue = 0, uint pullUpPullDownResetValue = 0) : base(machine, NumberOfPins)
        {
            mode = new Mode[NumberOfPins];
            outputSpeed = new OutputSpeed[NumberOfPins];
            pullUpPullDown = new PullUpPullDown[NumberOfPins];

            this.modeResetValue = modeResetValue;
            this.outputSpeedResetValue = outputSpeedResetValue;
            this.pullUpPullDownResetValue = pullUpPullDownResetValue;

            registers = CreateRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();

            for(var i = 0; i < NumberOfPins; i++)
            {
                mode[i] = (Mode)BitHelper.GetValue(modeResetValue, 2 * i, 2);
                outputSpeed[i] = (OutputSpeed)BitHelper.GetValue(outputSpeedResetValue, 2 * i, 2);
                pullUpPullDown[i] = (PullUpPullDown)BitHelper.GetValue(pullUpPullDownResetValue, 2 * i, 2);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            Connections[number].Set(value);
        }

        private void WriteState(ushort value)
        {
            for(var i = 0; i < NumberOfPins; i++)
            {
                var state = ((value & 1u) == 1);

                State[i] = state;
                if(state)
                {
                    Connections[i].Set();
                }
                else
                {
                    Connections[i].Unset();
                }

                value >>= 1;
            }
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Mode, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, Mode>(0, 2, NumberOfPins, name: "MODER",
                        valueProviderCallback: (idx, _) => mode[idx],
                        writeCallback: (idx, _, val) => { mode[idx] = val; })
                },
                {(long)Registers.OutputType, new DoubleWordRegister(this)
                    .WithTaggedFlag("OT0", 0)
                    .WithTaggedFlag("OT1", 1)
                    .WithTaggedFlag("OT2", 2)
                    .WithTaggedFlag("OT3", 3)
                    .WithTaggedFlag("OT4", 4)
                    .WithTaggedFlag("OT5", 5)
                    .WithTaggedFlag("OT6", 6)
                    .WithTaggedFlag("OT7", 7)
                    .WithTaggedFlag("OT8", 8)
                    .WithTaggedFlag("OT9", 9)
                    .WithTaggedFlag("OT10", 10)
                    .WithTaggedFlag("OT11", 11)
                    .WithTaggedFlag("OT12", 12)
                    .WithTaggedFlag("OT13", 13)
                    .WithTaggedFlag("OT14", 14)
                    .WithTaggedFlag("OT15", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.OutputSpeed, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, OutputSpeed>(0, 2, NumberOfPins, name: "OSPEEDR",
                        valueProviderCallback: (idx, _) => outputSpeed[idx],  
                        writeCallback: (idx, _, val) => { outputSpeed[idx] = val; })
                },
                {(long)Registers.PullUpPullDown, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, PullUpPullDown>(0, 2, NumberOfPins, name: "PUPDR0",
                        valueProviderCallback: (idx, _) => pullUpPullDown[idx],
                        writeCallback: (idx, _, val) => { pullUpPullDown[idx] = val; })
                },
                {(long)Registers.InputData, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State), name: "IDR")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.OutputData, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, val) => WriteState((ushort)val), valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State), name: "ODR")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.BitSet, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, 
                        writeCallback: (_, val) => { if(val != 0) WriteState((ushort)(BitHelper.GetValueFromBitsArray(State) | val)); },
                        name: "GPIOx_BS")
                    .WithValueField(16, 16, FieldMode.Write,
                        writeCallback: (_, val) => { if(val != 0) WriteState((ushort)(BitHelper.GetValueFromBitsArray(State) & ~val)); },
                        name: "GPIOx_BR")
                },
                { (long)Registers.ConfigurationLock, new DoubleWordRegister(this)
                    .WithTaggedFlag("LCK0", 0)
                    .WithTaggedFlag("LCK1", 1)
                    .WithTaggedFlag("LCK2", 2)
                    .WithTaggedFlag("LCK3", 3)
                    .WithTaggedFlag("LCK4", 4)
                    .WithTaggedFlag("LCK5", 5)
                    .WithTaggedFlag("LCK6", 6)
                    .WithTaggedFlag("LCK7", 7)
                    .WithTaggedFlag("LCK8", 8)
                    .WithTaggedFlag("LCK9", 9)
                    .WithTaggedFlag("LCK10", 10)
                    .WithTaggedFlag("LCK11", 11)
                    .WithTaggedFlag("LCK12", 12)
                    .WithTaggedFlag("LCK13", 13)
                    .WithTaggedFlag("LCK14", 14)
                    .WithTaggedFlag("LCK15", 15)
                    .WithTaggedFlag("LCKK", 16)
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.AlternateFunctionLow, new DoubleWordRegister(this)
                    .WithTag("AFSEL0", 0, 4)
                    .WithTag("AFSEL1", 4, 4)
                    .WithTag("AFSEL2", 8, 4)
                    .WithTag("AFSEL3", 12, 4)
                    .WithTag("AFSEL4", 16, 4)
                    .WithTag("AFSEL5", 20, 4)
                    .WithTag("AFSEL6", 24, 4)
                    .WithTag("AFSEL7", 28, 4)
                },
                {(long)Registers.AlternateFunctionHigh, new DoubleWordRegister(this)
                    .WithTag("AFSEL8", 0, 4)
                    .WithTag("AFSEL9", 4, 4)
                    .WithTag("AFSEL10", 8, 4)
                    .WithTag("AFSEL11", 12, 4)
                    .WithTag("AFSEL12", 16, 4)
                    .WithTag("AFSEL13", 20, 4)
                    .WithTag("AFSEL14", 24, 4)
                    .WithTag("AFSEL15", 28, 4)
                },
                {(long)Registers.BitReset, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, 
                        writeCallback: (_, val) => { if(val != 0) WriteState((ushort)(BitHelper.GetValueFromBitsArray(State) ^ val)); },
                        name: "GPIOx_BRR")
                    .WithReservedBits(16, 16)
                },
            };
            return new DoubleWordRegisterCollection(this, registersMap);
        }

        private readonly Mode[] mode;
        private readonly OutputSpeed[] outputSpeed;
        private readonly PullUpPullDown[] pullUpPullDown;

        private readonly uint modeResetValue;
        private readonly uint outputSpeedResetValue;
        private readonly uint pullUpPullDownResetValue;

        private readonly DoubleWordRegisterCollection registers;

        private const int NumberOfPins = 16;

        private enum Mode
        {
            Input             = 0x0,
            Output            = 0x1,
            AlternateFunction = 0x2,
            AnalogMode        = 0x3,
        }

        private enum OutputSpeed
        {
            // The reference manual defines the 'Low' setting with 2 possible values
            Low1   = 0b00,
            Low2   = 0b10,
            Medium = 0b01,
            High   = 0b11,
        }

        private enum PullUpPullDown
        {
            No       = 0b00,
            Up       = 0b01,
            Down     = 0b10,
            Reserved = 0b11,
        }

        // Source: Chapter 7.4 in RM0090 Cortex M4 Reference Manual (Doc ID 018909 Rev 4)
        // for STM32F40xxx, STM32F41xxx, STM32F42xxx, STM32F43xxx advanced ARM-based 32-bit MCUs
        private enum Registers
        {
            Mode                  = 0x00, //GPIOx_MODE    Mode register
            OutputType            = 0x04, //GPIOx_OTYPER  Output type register
            OutputSpeed           = 0x08, //GPIOx_OSPEEDR Output speed register
            PullUpPullDown        = 0x0C, //GPIOx_PUPDR   Pull-up/pull-down register
            InputData             = 0x10, //GPIOx_IDR     Input data register
            OutputData            = 0x14, //GPIOx_ODR     Output data register
            BitSet                = 0x18, //GPIOx_BSRR    Bit set/reset register
            ConfigurationLock     = 0x1C, //GPIOx_LCKR    Configuration lock register
            AlternateFunctionLow  = 0x20, //GPIOx_AFRL    Alternate function low register
            AlternateFunctionHigh = 0x24, //GPIOx_AFRH    Alternate function high register
            BitReset              = 0x28, //GPIOx_BRR     Bit reset register
        }
    }
}
