//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class OpenTitan_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_GPIO(Machine machine) : base(machine, numberOfPins)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            outputValue = new bool[numberOfPins];
            outputEnabled = new bool[numberOfPins];
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            for(var i = 0; i < numberOfPins; ++i)
            {
                outputValue[i] = false;
                outputEnabled[i] = false;
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

        public long Size => 0x3C;

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Output, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "DIRECT_OUT",
                        valueProviderCallback: (id, _) => outputValue[id],
                        writeCallback: (id, _, val) => { outputValue[id] = val; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.OutputEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "DIRECT_OE",
                        valueProviderCallback: (id, _) => outputEnabled[id],
                        writeCallback: (id, _, val) => { outputEnabled[id] = val; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                }
            };
        }

        private void UpdateConnections()
        {
            for(var i = 0; i < numberOfPins; ++i)
            {
                Connections[i].Set(outputEnabled[i] && outputValue[i]);
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private bool[] outputValue;
        private bool[] outputEnabled;

        private const int numberOfPins = 32;

        private enum Registers : long
        {
            InterruptState          = 0x0,
            InterruptEnable         = 0x4,
            InterruptTest           = 0x8,
            Input                   = 0xC,
            Output                  = 0x10,
            OutputMaskedLower       = 0x14,
            OutputMaskedUpper       = 0x18,
            OutputEnable            = 0x1C,
            OutputEnableMaskedLower = 0x20,
            OutputEnableMaskedUpper = 0x24,
            InterruptEnableRising   = 0x28,
            InterruptEnableFalling  = 0x2C,
            InterruptEnableHigh     = 0x30,
            InterruptEnableLow      = 0x34,
            InputFilter             = 0x38
        }
    }
}
