//
// Copyright (c) 2010-2021 Antmicro
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
    public class IMXRT_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public IMXRT_GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            locker = new object();
            IRQ = new GPIO();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            data = new bool[NumberOfPins];
            directionOutNotIn = new bool[NumberOfPins];
            interruptEnabled = new bool[NumberOfPins];
            interruptRequest = new bool[NumberOfPins];
            edgeSelect = new bool[NumberOfPins];
            interruptConfig = new InterruptConfig[NumberOfPins];
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                IRQ.Unset();
                registers.Reset();
                for(var i = 0; i < NumberOfPins; ++i)
                {
                    data[i] = false;
                    directionOutNotIn[i] = false;
                    interruptEnabled[i] = false;
                    interruptRequest[i] = false;
                    edgeSelect[i] = false;
                    interruptConfig[i] = InterruptConfig.Low;
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

            if(directionOutNotIn[number])
            {
                this.Log(LogLevel.Warning, "gpio {0} is set to output, signal ignored.", number);
                return;
            }

            lock(locker)
            {
                var previousState = State[number];
                base.OnGPIO(number, value);

                UpdateSingleInterruptRequest(number, value, previousState != value);
                UpdateIRQ();
            }
        }

        public long Size => 0x90;

        public GPIO IRQ { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GR / GPIO data register",
                        writeCallback: (id, _, val) => { data[id] = val; },
                        valueProviderCallback: (id, _) =>
                        {
                            return (directionOutNotIn[id])
                                ? data[id]
                                : State[id];
                        })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.Direction, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GDIR / GPIO direction register",
                        writeCallback: (id, _, val) => { directionOutNotIn[id] = val; },
                        valueProviderCallback: (id, _) => directionOutNotIn[id])
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.PadStatus, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read, name: "PSR / GPIO pad status register",
                        valueProviderCallback: (id, _) => State[id])
                },
                {(long)Registers.Mask, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "IMR / GPIO interrupt mask register",
                        writeCallback: (id, _, val) => { interruptEnabled[id] = val; },
                        valueProviderCallback: (id, _) => interruptEnabled[id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read | FieldMode.WriteOneToClear, name: "ISR / GPIO interrupt status register",
                        writeCallback: (id, _, val) =>
                        {
                            if(val)
                            {
                                interruptRequest[id] = false;
                            }
                        },
                        valueProviderCallback: (id, _) => interruptRequest[id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.EdgeSelect, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "EDGE_SEL / GPIO edge select register",
                        writeCallback: (id, _, val) => { edgeSelect[id] = val; },
                        valueProviderCallback: (id, _) => edgeSelect[id])
                },
                {(long)Registers.DataSet, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Write, name: "DR_SET / GPIO data register SET",
                        writeCallback: (id, _, __)  => { data[id] = true; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.DataClear, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Write, name: "DR_CLEAR / GPIO data register CLEAR",
                        writeCallback: (id, _, __)  => { data[id] = false; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.DataToggle, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Write, name: "DR_TOGGLE / GPIO data register TOGGLE",
                        writeCallback: (id, _, __)  => { data[id] ^= true; })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
            };

            var config1 = new DoubleWordRegister(this);
            var config2 = new DoubleWordRegister(this);
            var half = NumberOfPins / 2;
            for(var i = 0; i < half; ++i)
            {
                var j = i;
                config1.WithEnumField<DoubleWordRegister, InterruptConfig>(j * 2, 2,
                    name: $"ICR{j} / Interrupt configuration {j}",
                    writeCallback: (_, val) => { interruptConfig[j] = val; },
                    valueProviderCallback: _ => interruptConfig[j]);
                config2.WithEnumField<DoubleWordRegister, InterruptConfig>(j * 2, 2,
                    name: $"ICR{half + j} / Interrupt configuration {half + j}",
                    writeCallback: (_, val) => { interruptConfig[half + j] = val; },
                    valueProviderCallback: _ => interruptConfig[half + j]);
            }
            config1.WithWriteCallback((_, __) => UpdateAllInterruptRequests());
            config2.WithWriteCallback((_, __) => UpdateAllInterruptRequests());
            registersDictionary.Add((long)Registers.Config1, config1);
            registersDictionary.Add((long)Registers.Config2, config2);
            return registersDictionary;
        }

        private void UpdateIRQ()
        {
            var flag = false;
            for(var i = 0; i < NumberOfPins; ++i)
            {   
                flag |= interruptEnabled[i] && interruptRequest[i];
            }
            IRQ.Set(flag);
        }

        private void UpdateConnections()
        {
            for(var i = 0; i < NumberOfPins; ++i)
            {
                Connections[i].Set(directionOutNotIn[i] && data[i]);
            }
            UpdateIRQ();
        }

        private void UpdateAllInterruptRequests()
        {
            for(var i = 0; i < NumberOfPins; ++i)
            {
                UpdateSingleInterruptRequest(i, State[i]);
            }
            UpdateIRQ();
        }

        private void UpdateSingleInterruptRequest(int i, bool currentState, bool stateChanged = false)
        {
            if(edgeSelect[i])
            {
                interruptRequest[i] |= stateChanged;
            }
            else
            {
                switch(interruptConfig[i])
                {
                    case InterruptConfig.Low:
                        interruptRequest[i] |= !currentState;
                        break;
                    case InterruptConfig.High:
                        interruptRequest[i] |= currentState;
                        break;
                    case InterruptConfig.Rising:
                        interruptRequest[i] |= stateChanged && currentState; 
                        break;
                    case InterruptConfig.Falling:
                        interruptRequest[i] |= stateChanged && !currentState; 
                        break;
                    default:
                        this.Log(LogLevel.Error, "Invalid state (interruptConfig[{0}]: 0x{1:X}).", i, interruptConfig[i]);
                        break;
                }
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;
        private readonly bool[] data;
        private readonly bool[] directionOutNotIn;
        private readonly bool[] interruptEnabled;
        private readonly bool[] interruptRequest;
        private readonly bool[] edgeSelect;
        private readonly InterruptConfig[] interruptConfig;

        private const int NumberOfPins = 32;

        private enum InterruptConfig
        {
            Low = 0b00,
            High = 0b01,
            Rising = 0b10,
            Falling = 0b11,
        }

        private enum Registers : long
        {
            Data = 0x0,
            Direction = 0x4,
            PadStatus = 0x8,
            Config1 = 0xc,
            Config2 = 0x10,
            Mask = 0x14,
            Status = 0x18,
            EdgeSelect = 0x1c,
            DataSet = 0x84,
            DataClear = 0x88,
            DataToggle = 0x8c,
        }
    }
}
