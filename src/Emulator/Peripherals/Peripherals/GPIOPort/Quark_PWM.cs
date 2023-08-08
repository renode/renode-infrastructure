//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.X86
{
    public class Quark_PWM : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public Quark_PWM(IMachine machine) : base(machine, NumberOfInternalTimers)
        {
            IRQ = new GPIO();
            internalLock = new object();
            interruptStatus = new bool[NumberOfInternalTimers];
            timers = new LimitTimer[NumberOfInternalTimers];
            interruptMask = new bool[NumberOfInternalTimers];
            alternativeLoadCount = new uint[NumberOfInternalTimers];
            operationMode = new OperationMode[NumberOfInternalTimers];
            runningMode = new RunningMode[NumberOfInternalTimers];
            for(int i = 0; i < timers.Length; i++)
            {
                timers[i] = new LimitTimer(machine.ClockSource, 32000000, this, i.ToString(), eventEnabled: true);
                var j = i;
                timers[i].LimitReached += () => HandleLimitReached(j);
            }
            PrepareRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            for(int i = 0; i < NumberOfInternalTimers; i++)
            {
                alternativeLoadCount[i] = 0;
                operationMode[i] = OperationMode.Timer;
                runningMode[i] = RunningMode.Free;
                interruptMask[i] = false;
                interruptStatus[i] = false;
                timers[i].Reset();
            }
            registers.Reset();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; private set; }

        public long Size { get { return 0xC0;} }

        private void PrepareRegisters()
        {
            var dict = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TimersInterruptStatus, new DoubleWordRegister(this)
                                // we have to reverse it as: Bit position corresponds to PWM/Timer number
                                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(interruptStatus.Zip(interruptMask, (isSet, isMasked) => isSet && !isMasked).Reverse()))
                },
                {(long)Registers.TimersRawInterruptStatus, new DoubleWordRegister(this)
                                // we have to reverse it as: Bit position corresponds to PWM/Timer number
                                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(interruptStatus.Reverse()))
                },
                {(long)Registers.TimersEndOfInterrupt, new DoubleWordRegister(this)
                                .WithValueField(0, 4, FieldMode.Read, readCallback: (_, __) => HandleEndOfInterrupt())
                }
            };

            for(int i = 0; i < timers.Length; i++)
            {
                var j = i;
                var offset = 0x14 * i;
                dict[(long)Registers.Timer1LoadCount + offset] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => {
                        timers[j].Limit = val;
                        timers[j].ResetValue();
                    });

                dict[(long)Registers.Timer1CurrentValue + offset] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)timers[j].Value);
                
                dict[(long)Registers.Timer1Control + offset] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "Enable", writeCallback: (_, val) => timers[j].Enabled = val)
                    .WithFlag(1, name: "Timer Mode", writeCallback: (_, val) => runningMode[j] = val ? RunningMode.UserDefinedCount : RunningMode.Free)
                    .WithFlag(2, name: "Interrupt Mask", writeCallback: (_, val) => interruptMask[j] = val)
                    .WithFlag(3, name: "PWM/Timer Mode", writeCallback: (_, val) => operationMode[j] = val ? OperationMode.PWM : OperationMode.Timer);

                dict[(long)Registers.Timer1EndOfInterrupt + offset] = new DoubleWordRegister(this)
                    .WithValueField(0, 1, FieldMode.Read, readCallback: (_, __) => HandleEndOfInterrupt(j));

                dict[(long)Registers.Timer1InterruptStatus + offset] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => interruptStatus[j]);

                // here we have a different registers offset!
                dict[(long)Registers.Timer1LoadCount2 + 0x4 * i] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => alternativeLoadCount[j] = (uint)val);
            }

            registers = new DoubleWordRegisterCollection(this, dict);
        }

        private void HandleLimitReached(int timerId)
        {
            lock(internalLock)
            {
                interruptStatus[timerId] = true;
                if(!interruptMask[timerId])
                {
                    IRQ.Set();
                }

                if(operationMode[timerId] == OperationMode.Timer)
                {
                    if(runningMode[timerId] == RunningMode.Free)
                    {
                        // i'm not sure if it is correct as the documentation is quite confusing
                        timers[timerId].Limit = uint.MaxValue;
                        timers[timerId].ResetValue();
                    }
                }
                else
                {
                    var currentLimit = (uint)timers[timerId].Limit;
                    timers[timerId].Limit = alternativeLoadCount[timerId];
                    timers[timerId].ResetValue();
                    alternativeLoadCount[timerId] = currentLimit;
                    Connections[timerId].Toggle();
                }
            }
        }

        private void HandleEndOfInterrupt(int? timerId = null)
        {
            lock(internalLock)
            {
                if(timerId.HasValue)
                {
                    interruptStatus[timerId.Value] = false;
                    if(interruptStatus.All(x => !x))
                    {
                        IRQ.Unset();
                    }
                }
                else
                {
                    for(int i = 0; i < interruptStatus.Length; i++)
                    {
                        interruptStatus[i] = false;
                    }
                    IRQ.Unset();
                }
            }
        }

        private DoubleWordRegisterCollection registers;
        private LimitTimer[] timers;
        private bool[] interruptStatus;
        private bool[] interruptMask;
        private uint[] alternativeLoadCount;
        private OperationMode[] operationMode;
        private RunningMode[] runningMode;
        private readonly object internalLock;

        private const int NumberOfInternalTimers = 4;

        public enum OperationMode
        {
            PWM,
            Timer
        }

        public enum RunningMode
        {
            Free,
            UserDefinedCount
        }

        private enum Registers : long
        {
            Timer1LoadCount = 0x0,
            Timer1CurrentValue = 0x4,
            Timer1Control = 0x8,
            Timer1EndOfInterrupt = 0xC,
            Timer1InterruptStatus = 0x10,
            Timer2LoadCount = 0x14,
            Timer2CurrentValue = 0x18,
            Timer2Control = 0x1C,
            Timer2EndOfInterrupt = 0x20,
            Timer2InterruptStatus = 0x24,
            Timer3LoadCount = 0x28,
            Timer3CurrentValue = 0x2C,
            Timer3Control = 0x30,
            Timer3EndOfInterrupt = 0x34,
            Timer3InterruptStatus = 0x38,
            Timer4LoadCount = 0x3C,
            Timer4CurrentValue = 0x40,
            Timer4Control = 0x44,
            Timer4EndOfInterrupt = 0x48,
            Timer4InterruptStatus = 0x4C,
            TimersInterruptStatus = 0xA0,
            TimersEndOfInterrupt = 0xA4,
            TimersRawInterruptStatus = 0xA8,
            Timer1LoadCount2 = 0xB0,
            Timer2LoadCount2 = 0xB4,
            Timer3LoadCount2 = 0xB8,
            Timer4LoadCount2 = 0xBC,
        }
    }
}

