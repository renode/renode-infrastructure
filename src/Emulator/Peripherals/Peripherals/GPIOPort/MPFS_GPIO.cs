//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MPFS_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public MPFS_GPIO(IMachine machine) : base(machine, 32)
        {
            locker = new object();
            IRQ = new GPIO();
            irqManager = new GPIOInterruptManager(IRQ, State);
            irqManager.DeassertActiveInterruptTrigger = true;

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) =>
                        {
                            foreach(var i in BitHelper.GetSetBits(val))
                            {
                                irqManager.ClearInterrupt(i);
                                if((irqManager.PinDirection[i] & GPIOInterruptManager.Direction.Input) != 0)
                                {
                                    Connections[i].Set(false);
                                }
                            }
                        },
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(irqManager.ActiveInterrupts), name: "INTR")
                },

                {(long)Registers.InputRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: val =>
                        {
                            var pins = irqManager.PinDirection.Select(x => (x & GPIOInterruptManager.Direction.Input) != 0);
                            var result = pins.Zip(State, (pin, state) => pin && state);
                            return BitHelper.GetValueFromBitsArray(result);
                        }, name: "GPIN")
                },

                {(long)Registers.OutputRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: val =>
                        {
                            var pins = irqManager.PinDirection.Select(x => (x & GPIOInterruptManager.Direction.Output) != 0);
                            var result = pins.Zip(Connections.Values, (pin, state) => pin && state.IsSet);
                            return BitHelper.GetValueFromBitsArray(result);
                        },
                        writeCallback: (_, val) =>
                        {
                            // Potentially we should raise an exception, as GPIO is bidirectional,
                            // but we do not have such infrastructure.
                            var bits = BitHelper.GetBits((uint)val);
                            for(var i = 0; i < bits.Length; i++)
                            {
                                if((irqManager.PinDirection[i] & GPIOInterruptManager.Direction.Output) != 0)
                                {
                                    Connections[i].Set(bits[i]);
                                }
                            }
                        }, name: "GPOUT")
                },

                {(long)Registers.ClearRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => SetRegisterBits((uint)val, false), name: "CLEAR_BITS")
                },

                {(long)Registers.SetRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => SetRegisterBits((uint)val, true), name: "SET_BITS")
                },
            };

            var intTypeToVal = new TwoWayDictionary<GPIOInterruptManager.InterruptTrigger, uint>();
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.ActiveHigh, 0);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.ActiveLow, 1);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.RisingEdge, 2);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.FallingEdge, 3);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.BothEdges, 4);

            for(var i = 0; i < RegisterLength; i++)
            {
                var j = i;
                registersMap.Add(i * RegisterOffset, new DoubleWordRegister(this)
                    .WithFlag(0,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                irqManager.PinDirection[j] |= GPIOInterruptManager.Direction.Output;
                            }
                            else
                            {
                                irqManager.PinDirection[j] &= ~GPIOInterruptManager.Direction.Output;
                            }
                        },
                        valueProviderCallback: _ => (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Output) != 0, name: "OutputRegEnable")
                    .WithFlag(1,
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                irqManager.PinDirection[j] |= GPIOInterruptManager.Direction.Input;
                            }
                            else
                            {
                                irqManager.PinDirection[j] &= ~GPIOInterruptManager.Direction.Input;
                            }
                        },
                        valueProviderCallback: _ => (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Input) != 0, name: "InputRegEnable")
                    .WithTag("OutputBufferEnable", 2, 1)
                    .WithFlag(3, writeCallback: (_, v) => { irqManager.InterruptEnable[j] = v; }, valueProviderCallback: _ => irqManager.InterruptEnable[j], name: "InterruptEnable")
                    .WithReservedBits(4, 1)
                    .WithValueField(5, 3,
                        writeCallback: (_, value) =>
                        {
                            if(!intTypeToVal.TryGetValue((uint)value, out var type))
                            {
                                this.Log(LogLevel.Warning, "Invalid interrupt type for pin #{0}: {1}", j, value);
                                return;
                            }
                            irqManager.InterruptType[j] = type;
                        },
                        valueProviderCallback: _ => intTypeToVal[irqManager.InterruptType[j]], name: "InterruptType"));
            }
            registers = new DoubleWordRegisterCollection(this, registersMap);
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
            lock(locker)
            {
                var isInput = irqManager.PinDirection[number].HasFlag(GPIOInterruptManager.Direction.Input);
                var isOutput = irqManager.PinDirection[number].HasFlag(GPIOInterruptManager.Direction.Output);
                if(isOutput && !isInput)
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }
                base.OnGPIO(number, value);
                irqManager.RefreshInterrupts();

                // RefreshInterrupts will update the main IRQ, but it will not update the connection.
                // We have to do it manually, as connection reflects if there is an active interrupt for the given pin.
                var isIrqActive = irqManager.ActiveInterrupts.ElementAt(number);
                if(isInput)
                {
                    Connections[number].Set(isIrqActive);
                }
            }
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                irqManager.Reset();
                registers.Reset();
            }
        }

        public GPIO IRQ { get; private set; }

        public long Size => 0x1000;

        private void SetRegisterBits(uint regVal, bool state)
        {
            lock(locker)
            {
                var setBits = BitHelper.GetSetBits(regVal);
                foreach (var i in setBits)
                {
                    Connections[i].Set(state);
                }
            }
        }

        private readonly GPIOInterruptManager irqManager;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;

        private const int RegisterLength = 32;
        private const int RegisterOffset = 0x4;

        private enum Registers
        {
            InterruptRegister = 0x80,
            InputRegister = 0x84,
            OutputRegister = 0x88,
            ConfigurationRegister = 0x8c,
            ConfigurationRegisterByte0 = 0x90,
            ConfigurationRegisterByte1 = 0x94,
            ConfigurationRegisterByte2 = 0x98,
            ConfigurationRegisterByte3 = 0x9c,
            ClearRegister = 0xa0,
            SetRegister = 0xa4
        }
    }
}
