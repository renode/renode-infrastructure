//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class QorIQ_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize, IEndiannessAware
    {
        public QorIQ_GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            locker = new object();
            IRQ = new GPIO();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            directionOutNotIn = new bool[NumberOfPins];
            openDrain = new bool[NumberOfPins];
            data = new bool[NumberOfPins];
            interruptRequest = new bool[NumberOfPins];
            interruptEnabled = new bool[NumberOfPins];
            interruptControl = new bool[NumberOfPins];
            Reset();
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
                    directionOutNotIn[i] = false;
                    openDrain[i] = false;
                    data[i] = false;
                    interruptRequest[i] = false;
                    interruptEnabled[i] = false;
                    interruptControl[i] = false;
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
            this.NoisyLog("OnGPIO - GPIO {0}, value {1}", number, value);
            if(!CheckPinNumber(number))
            {
                return;
            }

            if(directionOutNotIn[number]) //We are driving this GPIO so we don't want something else driving it
            {
                this.Log(LogLevel.Warning, "GPIO {0} is set to output, signal ignored", number);
                return;
            }

            lock(locker)
            {
                var previousState = data[NumberOfPins - 1 - number];
                data[NumberOfPins - 1 - number] = value; 

                base.OnGPIO(number, value);

                UpdateSingleInterruptRequest(number, previousState, value);
            }
        }

        public long Size => 0x10000; // It only really takes 0x1C but DTS specifies 0x10000 for LS1043

        public GPIO IRQ { get; }

        public Endianess Endianness => Endianess.BigEndian;

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Direction, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPDIR / GPIO direction register",
                        // When switching from in to out, value stored in data register is outputted per doc ; driver does a reset to 0 before switching
                        // When switching from out to in, new value is set by OnGPIO and will be reflected on GPDAT register ; at switch time, nothing happens
                        writeCallback: (id, _, val) => {
                            this.NoisyLog("Write to GPDIR (direction) register with pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            directionOutNotIn[NumberOfPins - 1 - id] = val;
                        },
                        valueProviderCallback: (id, _) => directionOutNotIn[NumberOfPins - 1 - id])
                },
                {(long)Registers.OpenDrain, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name:"GPODR / GPIO Open Drain register",
                        writeCallback: (id,_,val) => {
                            this.NoisyLog("Write to GPODR (drain) register with pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            openDrain[NumberOfPins - 1 - id] = val;
                        },
                        valueProviderCallback: (id, _) => openDrain[NumberOfPins - 1 - id])
                    .WithWriteCallback((_,_) => UpdateConnections())
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPDAT / GPIO data register", // Data register reflects the state of the line, whatever the mode
                        writeCallback: (id, _, val) => {
                            this.NoisyLog("Write to GPDAT register with pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            data[NumberOfPins - 1 - id] = val;
                        },
                        valueProviderCallback: (id, _) => data[NumberOfPins - 1 - id])
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.Event, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read | FieldMode.WriteOneToClear, name: "GPIER / GPIO interrupt event register",
                        writeCallback: (id, old, val) =>
                        {
                            this.NoisyLog("Write (clear) to GPIER register with pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            if(val) //Write one to clear
                            {
                                interruptRequest[NumberOfPins - 1 - id] = false;
                                UpdateSingleInterruptRequest(NumberOfPins - 1 - id, old, val);
                            }
                        },
                        valueProviderCallback: (id, _) => interruptRequest[NumberOfPins - 1 - id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.Mask, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPIMR / GPIO Interrupt mask register",
                    writeCallback: (id, _, val) => {
                            this.NoisyLog("Write to GPIMR (mask, 1 if interruptions are enabled) register wwith pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            interruptEnabled[NumberOfPins - 1 - id] = val;
                    },
                    valueProviderCallback: (id,_) => interruptEnabled[NumberOfPins - 1 - id])
                    .WithWriteCallback((_,_) => UpdateIRQ())
                },
                {(long)Registers.InterruptControl, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPICR / GPIO interrupt control register",
                    writeCallback: (id, _, val) => {
                            this.NoisyLog("Write to GPICR register with pin {0} and value {1}", NumberOfPins - 1 - id, val);
                            interruptControl[NumberOfPins - 1 - id] = val;
                    },
                    valueProviderCallback: (id, _) => interruptControl[NumberOfPins - 1 - id])
                }
            };
            return registersDictionary;
        }

        private void UpdateIRQ()
        {
            this.NoisyLog("Update IRQ");
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
                if(!directionOutNotIn[i]) continue; // Do not set input signals
                if(openDrain[i] && data[i]) continue; // Do not set the line to true if it is open drain
                Connections[i].Set(data[i]);
            }
            UpdateIRQ();
        }

        private void UpdateSingleInterruptRequest(int i, bool oldState, bool currentState)
        {
            this.NoisyLog("In UpdateSingleInterruptRequest for line {0}, old value {1} -> new value {2}", i, oldState, currentState);
            if(interruptControl[i])
            {
                interruptRequest[i] |= (oldState && !currentState);
            }
            else
            {
                interruptRequest[i] |= (oldState != currentState);
            }
            UpdateIRQ();
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;
        private readonly bool[] directionOutNotIn;
        private readonly bool[] openDrain;
        private readonly bool[] data;
        private readonly bool[] interruptRequest;
        private readonly bool[] interruptEnabled;
        private readonly bool[] interruptControl;

        private const int NumberOfPins = 32;

        private enum Registers : long
        {
            Direction = 0x0,
            OpenDrain = 0x4,
            Data = 0x8,
            Event = 0xc,
            Mask = 0x10,
            InterruptControl = 0x14,
        }
    }
}