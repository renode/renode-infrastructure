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
            this.DebugLog($"In OnGPIO function with GPIO value {number} and value {value}");
            if(!CheckPinNumber(number))
            {
                return;
            }

            if(directionOutNotIn[number]) //We are driving this GPIO so we don't want something else driving it
            {
                this.Log(LogLevel.Warning, "gpio {0} is set to output, signal ignored.", number);
                return;
            }

            lock(locker)
            {
                var previousState = State[number];
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
                        writeCallback: (id, _, val) => {
                            this.DebugLog($"Write to GPDIR (direction) register with pin {NumberOfPins - 1 -id} and value {val}");
                            directionOutNotIn[NumberOfPins - 1 -id] = val; 
                        }, // /!\ Nothing is done when changing direction - data stays the same so input value might be driven out and vice versa
                        valueProviderCallback: (id, _) => directionOutNotIn[NumberOfPins - 1 -id])
                },
                {(long)Registers.OpenDrain, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name:"GPODR / GPIO Open Drain register",
                        writeCallback: (id,_,val) => {
                            this.DebugLog($"Write to GPODR (drain) register with pin {NumberOfPins - 1 -id} and value {val}");
                            openDrain[NumberOfPins - 1 -id] = val;
                        },
                        valueProviderCallback: (id, _) => openDrain[NumberOfPins - 1 -id]) 
                    .WithWriteCallback((_,_) => UpdateConnections())
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPDAT / GPIO data register",
                        writeCallback: (id, _, val) => { 
                            this.DebugLog($"Write to GPDAT register with pin {NumberOfPins - 1 -id} and value {val}");
                            data[NumberOfPins - 1 -id] = val; 
                        },
                        valueProviderCallback: (id, _) =>
                        {
                            return (directionOutNotIn[NumberOfPins - 1 -id])
                                ? data[NumberOfPins - 1 -id]
                                : State[NumberOfPins - 1 -id];
                        })
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)Registers.Event, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read | FieldMode.WriteOneToClear, name: "GPIER / GPIO interrupt event register",
                        writeCallback: (id, old, val) =>
                        {
                            this.DebugLog($"Write (clear) to GPIER register with pin {NumberOfPins - 1 -id} and value {val}");
                            if(val) //Write one to clear
                            {
                                interruptRequest[NumberOfPins - 1 -id] = false;
                                UpdateSingleInterruptRequest(NumberOfPins - 1 -id, old, val);
                            }
                        },
                        valueProviderCallback: (id, _) => interruptRequest[NumberOfPins - 1 -id])
                    .WithWriteCallback((_, __) => UpdateIRQ())
                },
                {(long)Registers.Mask, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPIMR / GPIO Interrupt mask register",
                    writeCallback: (id, _, val) => {
                            this.DebugLog($"Write to GPIMR (mask, 1 if interruptions are enabled) register with pin {NumberOfPins - 1 -id} and value {val}");
                            interruptEnabled[NumberOfPins - 1 -id] = val;
                    },
                    valueProviderCallback: (id,_) => interruptEnabled[NumberOfPins - 1 -id])
                    .WithWriteCallback((_,_) => UpdateIRQ())
                },
                {(long)Registers.InterruptControl, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, name: "GPICR / GPIO interrupt control register",
                    writeCallback: (id, _, val) => {
                            this.DebugLog($"Write to GPICR register with pin {NumberOfPins - 1 -id} and value {val}");
                            interruptControl[NumberOfPins - 1 -id] = val;
                    },
                    valueProviderCallback: (id, _) => interruptControl[NumberOfPins - 1 -id])
                }
            };
            return registersDictionary;
        }

        private void UpdateIRQ()
        {
            this.DebugLog($"In Update IRQ");
            var flag = false;
            for(var i = 0; i < NumberOfPins; ++i)
            {
                flag |= interruptEnabled[i] && interruptRequest[i];
                this.DebugLog($"Interrupt {i} is on: {interruptRequest[i]} and not masked: {interruptEnabled[i]}. As a result, flag is now {flag}");
            }
            IRQ.Set(flag);
        }

        private void UpdateConnections()
        {
            for(var i = 0; i < NumberOfPins; ++i)
            {
                if (!directionOutNotIn[i]) continue; // Do not set input signals
                if (openDrain[i] && data[i]) continue; // Do not set the line to true if it is open drain
                Connections[i].Set(data[i]);
            }
            UpdateIRQ();
        }

        private void UpdateSingleInterruptRequest(int i, bool oldState, bool currentState)
        {
            this.DebugLog($"In UpdateSingleInterruptRequest for line {i}, old value {oldState} -> new value {currentState}");
            if(interruptControl[i])
            {
                interruptRequest[i] |= (oldState && !currentState);
            }
            else
            {
                interruptRequest[i] = (oldState != currentState);
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