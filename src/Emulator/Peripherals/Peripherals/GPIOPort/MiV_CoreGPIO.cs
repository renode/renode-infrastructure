//
// Copyright (c) 2010-2018 Antmicro
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
    public class MiV_CoreGPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public MiV_CoreGPIO(Machine machine) : base(machine, NumberOfInterrupts)
        {
            innerLock = new object();
            IRQ = new GPIO();

            irqManager = new GPIOInterruptManager(IRQ, State);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptClearRegister, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        lock(innerLock)
                        {
                            foreach(var i in BitHelper.GetSetBits(value))
                            {
                                irqManager.ClearInterrupt(i);
                            }
                        }
                    })},

                {(long)Registers.InputRegister, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        lock(innerLock)
                        {
                            return GetConnectedInputPinsState();
                        }
                    })},

                {(long)Registers.OutputRegister, new DoubleWordRegister(this).WithValueField(0, 32,
                    writeCallback: (_, value) =>
                    {
                        lock(innerLock)
                        {
                            var bits = BitHelper.GetBits(value);
                            for(var i = 0; i < bits.Length; i++)
                            {
                                if((irqManager.PinDirection[i] & GPIOInterruptManager.Direction.Output) != 0)
                                {
                                    Connections[i].Set(bits[i]);
                                }
                            }
                        }
                    }, valueProviderCallback: _ =>
                    {
                        lock(innerLock)
                        {
                            return GetConnectedOutputPinsState();
                        }
                    })}
            };

            var intTypeToVal = new TwoWayDictionary<GPIOInterruptManager.InterruptTrigger, uint>();
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.ActiveHigh, 0);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.ActiveLow, 1);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.RisingEdge, 2);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.FallingEdge, 3);
            intTypeToVal.Add(GPIOInterruptManager.InterruptTrigger.BothEdges, 4);

            for(var i = 0; i < NumberOfInterrupts; i++)
            {
                var j = i;
                registersMap.Add((long)Registers.ConfigurationRegisterBase + i * 0x4, new DoubleWordRegister(this)
                    .WithFlag(0,
                        writeCallback: (_, v) =>
                        {
                            if(v)
                            {
                                irqManager.PinDirection[j] |= GPIOInterruptManager.Direction.Output;
                            }
                            else
                            {
                                irqManager.PinDirection[j] &= ~GPIOInterruptManager.Direction.Output;
                            }
                        },
                        valueProviderCallback: _ =>
                        {
                            return (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Output) != 0;
                        }, name: "OUTREG")
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
                        valueProviderCallback: _ =>
                        {
                            return (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Input) != 0;
                        }, name: "INREG")
                    .WithTag("OUTBUFF", 2, 1)
                    .WithFlag(3, writeCallback: (_, v) => irqManager.InterruptEnable[j] = v, valueProviderCallback: _ => irqManager.InterruptEnable[j], name: "INTENABLE")
                    .WithReservedBits(4, 1)
                    .WithValueField(5, 3, writeCallback: (_, value) =>
                    {
                        if(!intTypeToVal.TryGetValue(value, out var type))
                        {
                            this.Log(LogLevel.Warning, "Invalid interrupt type for pin #{0}: {1}", j, value);
                            return;
                        }
                        irqManager.InterruptType[j] = type;
                    }, valueProviderCallback: _ =>
                    {
                        if(!intTypeToVal.TryGetValue(irqManager.InterruptType[j], out var value))
                        {
                            throw new ArgumentOutOfRangeException($"Unknown interrupt trigger type: {irqManager.InterruptType[j]}");
                        }
                        return value;
                    }, name: "INTTYPE"));
            }

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

        public override void OnGPIO(int number, bool value)
        {
            lock(innerLock)
            {
                base.OnGPIO(number, value);
                if((irqManager.PinDirection[number] & GPIOInterruptManager.Direction.Input) == 0)
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }
                irqManager.RefreshInterrupts();
            }
        }

        public override void Reset()
        {
            lock(innerLock)
            {
                base.Reset();
                irqManager.Reset();
                registers.Reset();
            }
        }

        public GPIO IRQ { get; private set; }

        public long Size => 0xA4;

        private uint GetConnectedInputPinsState()
        {
            var pins = irqManager.PinDirection.Select(x => (x & GPIOInterruptManager.Direction.Input) != 0);
            var result = pins.Zip(State, (pin, state) => pin && state);
            return BitHelper.GetValueFromBitsArray(result);
        }

        private uint GetConnectedOutputPinsState()
        {
            var pins = irqManager.PinDirection.Select(x => (x & GPIOInterruptManager.Direction.Output) != 0);
            var result = pins.Zip(Connections.Values, (pin, state) => pin && state.IsSet);
            return BitHelper.GetValueFromBitsArray(result);
        }

        private readonly GPIOInterruptManager irqManager;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object innerLock;

        private const int NumberOfInterrupts = 32;
        private enum Registers : long
        {
            ConfigurationRegisterBase = 0x0,
            InterruptClearRegister = 0x80,
            InputRegister = 0x90,
            OutputRegister = 0xA0
        }
    }
}
