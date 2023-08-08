//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class STM32_GPIOPort : BaseGPIOPort, IDoubleWordPeripheral, ILocalGPIOReceiver
    {
        public STM32_GPIOPort(IMachine machine, uint modeResetValue = 0, uint outputSpeedResetValue = 0, uint pullUpPullDownResetValue = 0,
            uint numberOfAFs = 16) : base(machine, NumberOfPins)
        {
            if(numberOfAFs < 1 || numberOfAFs > 16)
            {
                throw new ConstructionException("Number of alternate functions can't be lower than 1 or higher than 16");
            }

            mode = new Mode[NumberOfPins];
            outputSpeed = new OutputSpeed[NumberOfPins];
            pullUpPullDown = new PullUpPullDown[NumberOfPins];

            this.modeResetValue = modeResetValue;
            this.outputSpeedResetValue = outputSpeedResetValue;
            this.pullUpPullDownResetValue = pullUpPullDownResetValue;
            this.numberOfAFs = numberOfAFs;

            alternateFunctionOutputs = new GPIOAlternateFunction[NumberOfPins];
            for(var i = 0; i < NumberOfPins; i++)
            {
                alternateFunctionOutputs[i] = new GPIOAlternateFunction(this, i);
            }

            registers = CreateRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();

            for(var i = 0; i < NumberOfPins; i++)
            {
                // Reset AF outputs before resetting pin modes as changing pin mode can affect whether AF is connected or not.
                alternateFunctionOutputs[i].Reset();
                ChangeMode(i, (Mode)BitHelper.GetValue(modeResetValue, 2 * i, 2));
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

        public IGPIOReceiver GetLocalReceiver(int pin)
        {
            if(pin < 0 || pin >= NumberOfPins)
            {
                throw new RecoverableException($"This peripheral supports GPIO inputs from 0 to {NumberOfPins - 1}, but {pin} was called.");
            }

            return alternateFunctionOutputs[pin];
        }

        private void WritePin(int number, bool value)
        {
            State[number] = value;
            Connections[number].Set(value);
        }

        private void WriteState(ushort value)
        {
            for(var i = 0; i < NumberOfPins; i++)
            {
                var state = ((value & 1u) == 1);
                WritePin(i, state);

                value >>= 1;
            }
        }

        private void ChangeMode(int number, Mode newMode)
        {
            mode[number] = newMode;
            alternateFunctionOutputs[number].IsConnected = newMode == Mode.AlternateFunction;
        }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Mode, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, Mode>(0, 2, NumberOfPins, name: "MODER",
                        valueProviderCallback: (idx, _) => mode[idx],
                        writeCallback: (idx, _, val) => ChangeMode(idx, val))
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
                    .WithValueFields(0, 4, 8, name: "AFSEL_LO",
                            writeCallback: (i, _, val) => alternateFunctionOutputs[i].ActiveFunction = val,
                            valueProviderCallback: (i, _) => alternateFunctionOutputs[i].ActiveFunction
                        )
                },
                {(long)Registers.AlternateFunctionHigh, new DoubleWordRegister(this)
                    .WithValueFields(0, 4, 8, name: "AFSEL_HI",
                            writeCallback: (i, _, val) => alternateFunctionOutputs[i + 8].ActiveFunction = val,
                            valueProviderCallback: (i, _) => alternateFunctionOutputs[i + 8].ActiveFunction
                        )
                },
                {(long)Registers.BitReset, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, 
                        writeCallback: (_, val) => { if(val != 0) WriteState((ushort)(BitHelper.GetValueFromBitsArray(State) & ~val)); },
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

        private readonly uint numberOfAFs;
        // NOTE: This array holds connections from AFs to specific GPIO pins.
        // From the PoV of this peripheral they're inputs,
        // however they represent the output pins of this peripheral hence the name.
        private readonly GPIOAlternateFunction[] alternateFunctionOutputs;
        // TODO: AF inputs

        private const int NumberOfPins = 16;

        private class GPIOAlternateFunction : IGPIOReceiver
        {
            public GPIOAlternateFunction(STM32_GPIOPort port, int pin)
            {
                this.port = port;
                this.pin = pin;

                Reset();
            }

            public void Reset()
            {
                IsConnected = false;
                ActiveFunction = 0;
            }

            public void OnGPIO(int number, bool value)
            {
                if(!CheckAFNumber(number) || !IsConnected || number != activeFunction)
                {
                    // Don't emit any log as it is valid to receive signals from AFs when they are not active.
                    // All alternate function sources are always connected and always sending signals.
                    // The GPIO configuration then decides whether those are connected
                    // to GPIO input/output or are simply ingored.
                    return;
                }

                port.WritePin(pin, value);
            }

            public bool IsConnected { get; set; }
            public ulong ActiveFunction
            {
                get => (ulong)activeFunction;
                set
                {
                    var val = (int)value;

                    if(CheckAFNumber(val))
                    {
                        activeFunction = val;
                    }
                }
            }

            private bool CheckAFNumber(int number)
            {
                if(number < 0 || number >= port.numberOfAFs)
                {
                    this.Log(LogLevel.Error, "Alternate function number must be between 0 and {0}, but {1} was given instead.", port.numberOfAFs - 1, number);
                    return false;
                }
                return true;
            }

            private readonly STM32_GPIOPort port;
            private readonly int pin;
            private int activeFunction;
        }

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
