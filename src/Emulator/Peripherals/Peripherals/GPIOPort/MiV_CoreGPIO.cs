//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
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
                {(long)Registers.OutputRegister, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        lock(innerLock)
                        {
                            var bits = BitHelper.GetBits(value);
                            for(var i = 0; i < bits.Length; i++)
                            {
                                Connections[i].Set(bits[i]);
                            }
                        }
                    })},

                {(long)Registers.InputRegister, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        lock(innerLock)
                        {
                            return BitHelper.GetValueFromBitsArray(State);
                        }
                    })},

                {(long)Registers.InterruptClearRegister, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                    {
                        lock(innerLock)
                        {
                            foreach(var i in BitHelper.GetSetBits(value))
                            {
                                irqManager.ClearInterrupt((uint)i);
                            }
                        }
                    }, valueProviderCallback: _ => {
                        lock(innerLock)
                        {
                            return BitHelper.GetValueFromBitsArray(irqManager.ActiveInterrupts);
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
                    .WithFlag(0, writeCallback: (_, v) =>
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
                        valueProviderCallback: _ => (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Output) != 0, name: "OUTREG")
                    .WithFlag(1, writeCallback: (_, value) =>
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
                        valueProviderCallback: _ => (irqManager.PinDirection[j] & GPIOInterruptManager.Direction.Input) != 0, name: "INREG")
                    .WithTag("OUTBUFF", 2, 1)
                    .WithFlag(3, writeCallback: (_, v) => { irqManager.InterruptEnable[j] = v; }, valueProviderCallback: _ => irqManager.InterruptEnable[j], name: "INTENABLE")
                    //bit 4 unused
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
