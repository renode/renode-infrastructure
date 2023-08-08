//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    // This model currently does not support interrupts
    public class PULP_APB_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public PULP_APB_GPIO(IMachine machine) : base(machine, NumberOfGPIOs)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PadInput_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Read, name: "PADIN / Pad input value register",
                        valueProviderCallback: (idx, _) => ReadInputPin(idx)
                    )
                },
                {(long)Registers.PadInput_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Read, name: "PADIN / Pad input value register",
                        valueProviderCallback: (idx, _) => ReadInputPin(idx + 32)
                    )
                },
                {(long)Registers.PadOutput_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "PADOUT / Pad Output value register",
                        writeCallback: (idx, _, val) => WriteOutputPin(idx, val, false),
                        // Not logging on input pins, as it's not illegal to read this
                        valueProviderCallback: (idx, _) => gpioDirection[idx] == Direction.Out ? Connections[idx].IsSet : false
                    )
                },
                {(long)Registers.PadOutputSet_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Write, name: "PADOUTSET / Pad Output set register",
                        writeCallback: (idx, _, val) =>
                        {
                            if(val)
                            {
                                WriteOutputPin(idx, val, true);
                            }
                        }
                    )
                },
                {(long)Registers.PadOutputClear_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Write, name: "PADOUTCLR / Pad Output clear register",
                        writeCallback: (idx, _, val) =>
                        {
                            if(val)
                            {
                                WriteOutputPin(idx, !val, true);
                            }
                        }
                    )
                },
                {(long)Registers.PadOutput_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "PADOUT / Pad Output value register",
                        writeCallback: (idx, _, val) => WriteOutputPin(idx + 32, val, false),
                        // Not logging on input pins, as it's not illegal to read this
                        valueProviderCallback: (idx, _) => gpioDirection[idx + 32] == Direction.Out ? Connections[idx + 32].IsSet : false
                    )
                },
                {(long)Registers.PadOutputSet_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Write, name: "PADOUTSET / Pad Output set register",
                        writeCallback: (idx, _, val) =>
                        {
                            if(val)
                            {
                                WriteOutputPin(idx + 32, val, true);
                            }
                        }
                    )
                },
                {(long)Registers.PadOutputClear_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Write, name: "PADOUTCLR / Pad Output clear register",
                        writeCallback: (idx, _, val) =>
                        {
                            if(val)
                            {
                                WriteOutputPin(idx + 32, !val, true);
                            }
                        }
                    )
                },
                {(long)Registers.GpioEnable_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "GPIOEN / GPIO enable register",
                        writeCallback: (idx, _, val) => gpioClockEnabled[idx] = val,
                        valueProviderCallback: (idx, _) => gpioClockEnabled[idx]
                    )
                },
                {(long)Registers.GpioEnable_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "GPIOEN / GPIO enable register",
                        writeCallback: (idx, _, val) => gpioClockEnabled[idx + 32] = val,
                        valueProviderCallback: (idx, _) => gpioClockEnabled[idx + 32]
                    )
                },
                {(long)Registers.PadDirection_00_31, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "PADDIR / GPIO pad direction configuration register",
                        writeCallback: (idx, _, val) => gpioDirection[idx] = val ? Direction.Out : Direction.In,
                        valueProviderCallback: (idx, _) => gpioDirection[idx] == Direction.Out
                    )
                },
                {(long)Registers.PadDirection_32_63, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "PADDIR / GPIO pad direction configuration register",
                        writeCallback: (idx, _, val) => gpioDirection[idx + 32] = val ? Direction.Out : Direction.In,
                        valueProviderCallback: (idx, _) => gpioDirection[idx + 32] == Direction.Out
                    )
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            for(var i = 0; i < NumberOfGPIOs; ++i)
            {
                gpioClockEnabled[i] = false;
                gpioDirection[i] = Direction.In;
            }
        }

        private bool ReadInputPin(int pin)
        {
            if(gpioDirection[pin] != Direction.In)
            {
                // Trying to read pin that is not configured as input
                return false;
            }
            if(!gpioClockEnabled[pin])
            {
                // According to docs, clock gating is effective when you gate 4 pins at once. We don't simulate this here
                this.Log(LogLevel.Noisy, "Trying to read pin #{0} that has clock disabled", pin);
                return false;
            }

            return State[pin];
        }

        private void WriteOutputPin(int pin, bool val, bool isExplicit)
        {
            if(gpioDirection[pin] == Direction.Out)
            {
                Connections[pin].Set(val);
            }
            else if(isExplicit)
            {
                // Log only if the driver specifically affets this pin and it's not properly configured
                this.Log(LogLevel.Noisy, "Trying to write to pin #{pin} that is not configured as output");
            }
        }

        public long Size => 0x1000;

        private const int NumberOfGPIOs = 64;
        private bool[] gpioClockEnabled = new bool[NumberOfGPIOs];
        private Direction[] gpioDirection = new Direction[NumberOfGPIOs];

        private readonly DoubleWordRegisterCollection registers;

        public enum Direction
        {
            In,
            Out,
        };

        private enum Registers
        {
            PadDirection_00_31 = 0x0,
            GpioEnable_00_31 = 0x4,
            PadInput_00_31 = 0x8,
            PadOutput_00_31 = 0xC,
            PadOutputSet_00_31 = 0x10,
            PadOutputClear_00_31 = 0x14,
            InterruptEnable_00_31 = 0x18,
            InterruptType_00_15 = 0x1C,
            InterruptType_16_31 = 0x20,
            InterruptStatus_00_31 = 0x24,
            PadConfiguration_00_07 = 0x28,
            PadConfiguration_08_15 = 0x2C,
            PadConfiguration_16_23 = 0x30,
            PadConfiguration_24_31 = 0x34,
            PadDirection_32_63 = 0x38,
            GpioEnable_32_63 = 0x3c,
            PadInput_32_63 = 0x40,
            PadOutput_32_63 = 0x44,
            PadOutputSet_32_63 = 0x48,
            PadOutputClear_32_63 = 0x4c,
            InterruptEnable_32_63 = 0x50,
            InterruptType_32_47 = 0x54,
            InterruptType_48_63 = 0x58,
            InterruptStatus_32_63 = 0x5c,
            PadConfiguration_32_39 = 0x60,
            PadConfiguration_40_47 = 0x64,
            PadConfiguration_48_55 = 0x68,
            PadConfiguration_56_63 = 0x6c,
        }
    }
}

