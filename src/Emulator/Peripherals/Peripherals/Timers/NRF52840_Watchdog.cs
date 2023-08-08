//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NRF52840_Watchdog : LimitTimer, IDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_Watchdog(IMachine machine) : base(machine.ClockSource, InitialFrequency, eventEnabled: true)
        {
            IRQ = new GPIO();

            this.machine = machine;
            requestRegisterEnabled = new bool[NumberOfRegisters];
            requestRegisterStatus = new IFlagRegisterField[NumberOfRegisters];

            LimitReached += TriggerReset;

            DefineRegisters();
        }

        override public void Reset()
        {
            base.Reset();
            registers.Reset();
            IRQ.Unset();
            readyToReset = false;
            interruptEnabled = false;
            for(var i = 0; i < NumberOfRegisters; i++)
            {
                requestRegisterEnabled[i] = false;
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

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public event Action<uint> EventTriggered;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.Start, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASK_START",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                Value = Limit + 1;
                                Enabled = true;
                            }
                        })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Timeout, new DoubleWordRegister(this)
                    .WithFlag(0, out eventTimeoutEnabled, name: "EVENTS_TIMEOUT", writeCallback: (_, value) =>
                    {
                        if(value && interruptEnabled)
                        {
                            IRQ.Set(true);
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptSet, new DoubleWordRegister(this)
                    .WithFlag(0, name: "INTENSET",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                interruptEnabled = true;
                            }
                        },
                        valueProviderCallback: _ => interruptEnabled)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptClear, new DoubleWordRegister(this)
                    .WithFlag(0, name: "INTENCLR",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                interruptEnabled = false;
                            }
                        },
                        valueProviderCallback: _ => interruptEnabled)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.RunStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "RUNSTATUS",
                        valueProviderCallback: _ => Enabled)
                    .WithReservedBits(1, 31)
                },
                {(long)Register.RequestStatus, new DoubleWordRegister(this, 0x1)
                    .WithFlags(0, NumberOfRegisters, out requestRegisterStatus, FieldMode.Read, name: "REQSTATUS")
                    .WithReservedBits(NumberOfRegisters, 32 - NumberOfRegisters)
                },
                {(long)Register.CounterReloadValue, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, name: "CRV",
                        changeCallback: (_, value) =>
                        {
                            if(Enabled)
                            {
                                this.Log(LogLevel.Warning, "Tried to change CRV while watchdog is running, ignored");
                                return;
                            }
                            if(value < 0xf)
                            {
                                this.Log(LogLevel.Warning, $"Tried to set CRV to illegal value ({value} < 15)");
                                return;
                            }
                            Limit = value;
                        },
                        valueProviderCallback: _ => (uint)Limit)
                },
                {(long)Register.EnableReloadRequest, new DoubleWordRegister(this, 0x1)
                    .WithFlags(0, NumberOfRegisters, name: "RREN",
                        writeCallback: (j, _, value) =>
                        {
                            if(Enabled)
                            {
                                this.Log(LogLevel.Warning, $"Tried to write RREN while watchdog is running, ignored");
                                return;
                            }
                            requestRegisterEnabled[j] = value;
                            requestRegisterStatus[j].Value = value;
                        },
                        valueProviderCallback: (j, _) => requestRegisterEnabled[j])
                    .WithReservedBits(NumberOfRegisters, 32 - NumberOfRegisters)
                },
                {(long)Register.Config, new DoubleWordRegister(this)
                    .WithFlag(0, name: "SLEEP")
                    .WithReservedBits(1, 2)
                    .WithFlag(3, name: "HALT")
                    .WithReservedBits(4, 28)
                    .WithWriteCallback((_, value) => this.Log(LogLevel.Warning, $"Write to a dummy implementation of the Config register, value: 0x{value:X}"))
                    .WithReadCallback((_, value) => this.Log(LogLevel.Warning, $"Read from a dummy implementation of the Config register, returned: 0x{value:X}"))
                }
            };

            for(var i = 0; i < NumberOfRegisters; i++)
            {
                var j = i;
                registersMap.Add((long)Register.ReloadRequest1 + i * 0x4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: $"RR{i}", writeCallback: (_, value) =>
                    {
                        if(value == ReloadRegisterResetValue)
                        {
                            requestRegisterStatus[j].Value = false;
                            Reload();
                        }
                    })
                );
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void TriggerReset()
        {
            if(!interruptEnabled || readyToReset)
            {
                this.Log(LogLevel.Info, "Reseting machine");
                machine.RequestReset();
                return;
            };

            this.Log(LogLevel.Info, "Timeout triggered with interrupt enabled, waiting 2 cycles to reset");
            EventTriggered?.Invoke((uint)Register.Timeout);
            eventTimeoutEnabled.Value = true;
            IRQ.Set(true);

            readyToReset = true;
            Value = 2;
            Enabled = true;
        }

        private void Reload()
        {
            if(readyToReset)
            {
                // readyToReset flag is true, which means that we already timeouted, ignore reload
                this.Log(LogLevel.Warning, "Trying to reload after timeout, ignored");
                return;
            }

            var reload = true;
            for(var i = 0; i < NumberOfRegisters; i++)
            {
                if(requestRegisterEnabled[i])
                {
                    reload &= !requestRegisterStatus[i].Value;
                }
            }

            if(reload)
            {
                for(var i = 0; i < NumberOfRegisters; i++)
                {
                    requestRegisterStatus[i].Value = requestRegisterEnabled[i];
                }
                Value = Limit + 1;
                this.NoisyLog("Counter reloaded");
            }
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField eventTimeoutEnabled;
        private IFlagRegisterField[] requestRegisterStatus;
        private bool[] requestRegisterEnabled;
        private bool interruptEnabled;
        private bool readyToReset;

        private readonly IMachine machine;

        private const int InitialFrequency = 32768;
        private const int NumberOfRegisters = 8;
        private const ulong ReloadRegisterResetValue = 0x6E524635;

        private enum Register : long
        {
            Start = 0x000,
            Timeout = 0x100,

            InterruptSet = 0x304,
            InterruptClear = 0x308,

            RunStatus = 0x400,
            RequestStatus = 0x404,
            CounterReloadValue = 0x504,
            EnableReloadRequest = 0x508,
            Config = 0x50C,

            ReloadRequest1 = 0x600,
            ReloadRequest2 = 0x604,
            ReloadRequest3 = 0x608,
            ReloadRequest4 = 0x60C,
            ReloadRequest5 = 0x610,
            ReloadRequest6 = 0x614,
            ReloadRequest7 = 0x618,
            ReloadRequest8 = 0x61C
        }
    }
}
