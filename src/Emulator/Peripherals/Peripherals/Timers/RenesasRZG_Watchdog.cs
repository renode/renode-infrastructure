//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

using TimeDirection = Antmicro.Renode.Time.Direction;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasRZG_Watchdog : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRZG_Watchdog(IMachine machine, long clockFrequency)
        {
            this.machine = machine;

            IRQ = new GPIO();

            this.timer = new LimitTimer(machine.ClockSource, clockFrequency, this, "Watchdog Timer",
                DefaultCycle, TimeDirection.Ascending, workMode: WorkMode.Periodic, eventEnabled: true
            );
            this.timer.LimitReached += OnLimitReached;

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            IRQ.Unset();
            timer.Reset();
            forceStop = false;
            timerEnabled = false;
            SystemResetEnabled = false;
            if(!keepGeneratedResetValue)
            {
                generatedReset = false;
            }
            keepGeneratedResetValue = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public long Size => 0x400;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public bool ForceStop
        {
            get => forceStop;
            set
            {
                forceStop = value;
                UpdateTimerStatus();
            }
        }

        public bool SystemResetEnabled { get; set; } = false;

        public bool GeneratedReset
        {
            get => generatedReset;
            set
            {
                // Only allow to reset the flag, not set it
                if(!value)
                {
                    generatedReset = false;
                }
            }
        }

        private void OnLimitReached()
        {
            if(IRQ.IsSet && SystemResetEnabled)
            {
                keepGeneratedResetValue = true;
                generatedReset = true;
                machine.RequestReset();
            }
            else
            {
                IRQ.Set();
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "WDTEN",
                        valueProviderCallback: _ => timerEnabled,
                        writeCallback: (_, value) =>
                        {
                            timerEnabled = value;
                            UpdateTimerStatus();
                        }
                    )
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.PeriodSetting, new DoubleWordRegister(this)
                    .WithReservedBits(0, 20)
                    .WithValueField(20, 12, name: "WDTTIME",
                        valueProviderCallback: _ => (timer.Limit >> 20) - 1,
                        writeCallback: (_, value) =>
                        {
                            if(timer.Enabled)
                            {
                                this.Log(LogLevel.Warning, "Setting WDTTIME while the timer is running. Ignoring");
                                return;
                            }
                            timer.Limit = (value + 1) << 20;
                        }
                    )
                },
                {(long)Registers.ElapsedTime, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "CRTTIME",
                        valueProviderCallback: _ => timer.Value,
                        writeCallback: (_, value) =>
                        {
                            if(timer.Enabled)
                            {
                                this.Log(LogLevel.Warning, "Setting CRTTIME while the timer is running. Ignoring");
                                return;
                            }
                            timer.Value = value;
                        }
                    )
                },
                {(long)Registers.InterruptControl, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.WriteOneToClear | FieldMode.Read, name: "INTDISP",
                        valueProviderCallback: _ => IRQ.IsSet,
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                IRQ.Unset();
                            }
                        }
                    )
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.ParityErrorControl, new DoubleWordRegister(this)
                    .WithTaggedFlags("PECR", 0, 32)
                },
                {(long)Registers.ParityErrorForcedEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("PEEN", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.ParityStatus, new DoubleWordRegister(this)
                    .WithTaggedFlags("PESR", 0, 32)
                },
                {(long)Registers.ParityErrorEnable, new DoubleWordRegister(this)
                    .WithTaggedFlags("PEER", 0, 32)
                },
                {(long)Registers.ParityErrorPolarity, new DoubleWordRegister(this)
                    .WithTaggedFlags("PEPO", 0, 32)
                },
            };

            return registerMap;
        }

        private void UpdateTimerStatus()
        {
            timer.Enabled = timerEnabled && !forceStop;
        }

        private bool forceStop = false;
        private bool timerEnabled = false;
        private bool generatedReset = false;
        private bool keepGeneratedResetValue = false;

        private readonly IMachine machine;
        private readonly LimitTimer timer;

        private const ulong DefaultCycle = 1ul << 20;

        public enum Registers : long
        {
            Control = 0x00,
            PeriodSetting = 0x04,
            ElapsedTime = 0x08,
            InterruptControl = 0x0C,
            ParityErrorControl = 0x10,
            ParityErrorForcedEnable = 0x14,
            ParityStatus = 0x18,
            ParityErrorEnable = 0x1C,
            ParityErrorPolarity = 0x20,
        }
    }
}