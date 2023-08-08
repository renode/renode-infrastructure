//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class OpenTitan_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_GPIO(IMachine machine) : base(machine, numberOfPins)
        {
            locker = new object();
            IRQ = new GPIO();
            FatalAlert = new GPIO();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            interruptRequest = new bool[numberOfPins];
            interruptEnabled = new bool[numberOfPins];
            directOutputValue = new bool[numberOfPins];
            maskedOutputValue = new bool[numberOfPins];
            directOutputEnabled = new bool[numberOfPins];
            maskedOutputEnabled = new bool[numberOfPins];
            interruptEnableRising = new bool[numberOfPins];
            interruptEnableFalling = new bool[numberOfPins];
            interruptEnableHigh = new bool[numberOfPins];
            interruptEnableLow = new bool[numberOfPins];
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                IRQ.Unset();
                FatalAlert.Unset();

                registers.Reset();
                for(var i = 0; i < numberOfPins; ++i)
                {
                    interruptRequest[i] = false;
                    interruptEnabled[i] = false;
                    directOutputValue[i] = false;
                    maskedOutputValue[i] = false;
                    directOutputEnabled[i] = false;
                    maskedOutputEnabled[i] = false;
                    interruptEnableRising[i] = false;
                    interruptEnableFalling[i] = false;
                    interruptEnableHigh[i] = false;
                    interruptEnableLow[i] = false;
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(locker)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(locker)
            {
                registers.Write(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            lock(locker)
            {
                var previousState = GetStateOnInput(number);
                base.OnGPIO(number, value);

                if(interruptEnabled[number])
                {
                    var currentState = GetStateOnInput(number);
                    if(interruptEnableRising[number])
                    {
                        interruptRequest[number] |= !previousState && currentState; 
                    }
                    if(interruptEnableFalling[number])
                    {
                        interruptRequest[number] |= previousState && !currentState; 
                    }
                }

                UpdateIRQ();
            }
        }

        public long Size => 0x3C;

        public GPIO IRQ { get; }
        public GPIO FatalAlert { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptState, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Read | FieldMode.WriteOneToClear, name: "INTR_STATE",
                        valueProviderCallback: (id, _) => interruptRequest[id],
                        writeCallback: (id, _, val) => { if(val) interruptRequest[id] = false; })
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INTR_ENABLE",
                        valueProviderCallback: (id, _) => interruptEnabled[id],
                        writeCallback: (id, _, val) => { interruptEnabled[id] = val; })
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.InterruptTest, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Write, name: "INTR_TEST",
                        writeCallback: (id, _, val) => { interruptRequest[id] |= val; })
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.AlertTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                    .WithIgnoredBits(1, 31)
                },
                {(long)Registers.Input, new DoubleWordRegister(this)
                    .WithFlags(0, 32, FieldMode.Read, name: "DATA_IN",
                        valueProviderCallback: (id, _) => GetStateOnInput(id))
                },
                {(long)Registers.Output, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "DIRECT_OUT",
                        valueProviderCallback: (id, _) => directOutputValue[id],
                        writeCallback: (id, _, val) => { directOutputValue[id] = val; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.OutputMaskedLower, new DoubleWordRegister(this)
                    .WithFlags(0, 16, name: "MASKED_OUT_LOWER.data",
                        valueProviderCallback: (id, _) => Connections[id].IsSet)
                    .WithFlags(16, 16, FieldMode.Write, name: "MASKED_OUT_LOWER.mask")
                    .WithWriteCallback((_, val) => SetOutputMasked(val, lower: true))
                },
                {(long)Registers.OutputMaskedUpper, new DoubleWordRegister(this)
                    .WithFlags(0, 16, name: "MASKED_OUT_UPPER.data",
                        valueProviderCallback: (id, _) => Connections[id + 16].IsSet)
                    .WithFlags(16, 16, FieldMode.Write, name: "MASKED_OUT_UPPER.mask")
                    .WithWriteCallback((_, val) => SetOutputMasked(val, lower: false))
                },
                {(long)Registers.OutputEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "DIRECT_OE",
                        valueProviderCallback: (id, _) => directOutputEnabled[id],
                        writeCallback: (id, _, val) => { directOutputEnabled[id] = val; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.OutputEnableMaskedLower, new DoubleWordRegister(this)
                    .WithFlags(0, 16, name: "MASKED_OE_LOWER.data",
                        valueProviderCallback: (id, _) => maskedOutputEnabled[id])
                    .WithFlags(16, 16, FieldMode.Write, name: "MASKED_OE_LOWER.mask")
                    .WithWriteCallback((_, val) => SetOutputEnableMasked(val, lower: true))
                },
                {(long)Registers.OutputEnableMaskedUpper, new DoubleWordRegister(this)
                    .WithFlags(0, 16, name: "MASKED_OE_UPPER.data",
                        valueProviderCallback: (id, _) => maskedOutputEnabled[id + 16])
                    .WithFlags(16, 16, FieldMode.Write, name: "MASKED_OE_UPPER.mask")
                    .WithWriteCallback((_, val) => SetOutputEnableMasked(val, lower: false))
                },
                {(long)Registers.InterruptEnableRising, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INTR_CTRL_EN_RISING",
                        writeCallback: (id, _, val) => { interruptEnableRising[id] = val; },
                        valueProviderCallback: (id, _) => interruptEnableRising[id])
                },
                {(long)Registers.InterruptEnableFalling, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INTR_CTRL_EN_FALLING",
                        writeCallback: (id, _, val) => { interruptEnableFalling[id] = val; },
                        valueProviderCallback: (id, _) => interruptEnableFalling[id])
                },
                {(long)Registers.InterruptEnableHigh, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INTR_CTRL_EN_LVLHIGH",
                        writeCallback: (id, _, val) => { interruptEnableHigh[id] = val; },
                        valueProviderCallback: (id, _) => interruptEnableHigh[id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.InterruptEnableLow, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INTR_CTRL_EN_LVLLOW",
                        writeCallback: (id, _, val) => { interruptEnableLow[id] = val; },
                        valueProviderCallback: (id, _) => interruptEnableLow[id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                }
            };
        }

        private void UpdateIRQ()
        {
            var flag = false;
            for(var i = 0; i < numberOfPins; ++i)
            {   
                if(!interruptEnabled[i])
                {
                    continue;
                }
                var state = GetStateOnInput(i);
                interruptRequest[i] |= state ? interruptEnableHigh[i] : interruptEnableLow[i];
                flag |= interruptRequest[i];
            }
            IRQ.Set(flag);
        }

        private void UpdateConnections()
        {
            for(var i = 0; i < numberOfPins; ++i)
            {
                if(directOutputEnabled[i])
                {
                    Connections[i].Set(directOutputValue[i]);
                }
                else if(maskedOutputEnabled[i])
                {
                    Connections[i].Set(maskedOutputValue[i]);
                }
                else
                {
                    Connections[i].Set(false);
                }
            }
            UpdateIRQ();
        }

        private void SetOutputMasked(uint value, bool lower)
        {
            var offset = lower ? 0 : 16;
            var data = BitHelper.GetBits(value & 0xFFFF);
            var mask = BitHelper.GetBits(value >> 16);
            for(var i = 0; i < 16; ++i)
            {
                if(mask[i])
                {
                    maskedOutputValue[i + offset] = data[i];
                }
            }
            UpdateConnections();
        }

        private void SetOutputEnableMasked(uint value, bool lower)
        {
            var offset = lower ? 0 : 16;
            var data = BitHelper.GetBits(value & 0xFFFF);
            var mask = BitHelper.GetBits(value >> 16);
            for(var i = 0; i < 16; ++i)
            {   
                if(mask[i])
                {
                    directOutputEnabled[i + offset] = data[i];
                }
            }
            UpdateConnections();
        }

        private bool GetStateOnInput(int i)
        {
            // Output and Input are physically connected 
            return State[i] || Connections[i].IsSet;
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;
        private bool[] interruptRequest;
        private bool[] interruptEnabled;
        private bool[] directOutputValue;
        private bool[] maskedOutputValue;
        private bool[] directOutputEnabled;
        private bool[] maskedOutputEnabled;
        private bool[] interruptEnableRising;
        private bool[] interruptEnableFalling;
        private bool[] interruptEnableHigh;
        private bool[] interruptEnableLow;

        private const int numberOfPins = 32;

        private enum Registers : long
        {
            InterruptState          = 0x0,
            InterruptEnable         = 0x4,
            InterruptTest           = 0x8,
            AlertTest               = 0xC,
            Input                   = 0x10,
            Output                  = 0x14,
            OutputMaskedLower       = 0x18,
            OutputMaskedUpper       = 0x1C,
            OutputEnable            = 0x20,
            OutputEnableMaskedLower = 0x24,
            OutputEnableMaskedUpper = 0x28,
            InterruptEnableRising   = 0x2C,
            InterruptEnableFalling  = 0x30,
            InterruptEnableHigh     = 0x34,
            InterruptEnableLow      = 0x38,
            InputFilter             = 0x3C
        }
    }
}
