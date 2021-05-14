//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NRF52840_Timer : BasicDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_Timer(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            eventCompareEnabled = new IFlagRegisterField[NumberOfEvents];
            eventCompareInterruptEnabled = new IFlagRegisterField[NumberOfEvents];

            innerTimers = new ComparingTimer[NumberOfEvents];
            for(var i = 0u; i < innerTimers.Length; i++)
            {
                var j = i;
                innerTimers[j] = new ComparingTimer(machine.ClockSource, InitialFrequency, this, $"compare{j}", eventEnabled: true);
                innerTimers[j].CompareReached += () =>
                {
                    eventCompareEnabled[j].Value = true;
                    EventTriggered?.Invoke((uint)Register.Compare0EventPending + 0x4u * j);
                    UpdateInterrupts();
                };
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            foreach(var timer in innerTimers)
            {
                timer.Reset();
            }

            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;


        public event Action<uint> EventTriggered;

        private void DefineRegisters()
        {
            Register.Start.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_START", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        timerRunning = true;
                        if(isInCounterMode.Value == 0)
                        {
                            foreach(var timer in innerTimers)
                            {
                                timer.Enabled = true;
                            }
                        }
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Register.Stop.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_STOP", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        timerRunning = false;
                        foreach(var timer in innerTimers)
                        {
                            timer.Enabled = false;
                        }
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Register.Count.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASK_COUNT", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        if(!timerRunning)
                        {
                            this.Log(LogLevel.Warning, "Triggered TASK_COUNT before issuing TASK_START, ignoring...");
                            return;
                        }
                        if(isInCounterMode.Value == 0)
                        {
                            this.Log(LogLevel.Warning, "Triggered TASK_COUNT in TIMER mode, ignoring...");
                            return;
                        }
                        var i = 0;
                        foreach(var timer in innerTimers)
                        {
                            timer.Value++;
                            if(timer.Compare == timer.Value)
                            {
                                eventCompareEnabled[i].Value = true;
                                UpdateInterrupts();
                            }
                            i++;
                        }
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Register.Clear.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASK_CLEAR", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        foreach(var timer in innerTimers)
                        {
                            timer.Value = 0;
                        }
                    }
                })
                .WithReservedBits(1, 31)
            ;

            Register.Capture0.DefineMany(this, NumberOfEvents, setup: (register, idx) =>
            {
                register
                    .WithFlag(0, FieldMode.Write, name: "TASKS_CAPTURE", writeCallback: (_,__) =>
                    {
                        SetCompare(idx, innerTimers[idx].Value);
                    })
                    .WithReservedBits(1, 31);
            });

            Register.Compare0EventPending.DefineMany(this, NumberOfEvents, setup: (register, idx) =>
            {
                register
                    .WithFlag(0, out eventCompareEnabled[idx], name: $"EVENTS_COMPARE[{idx}]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31);
            });

            Register.InterruptEnableSet.Define(this)
                .WithReservedBits(0, 16)
                .WithFlag(16, out eventCompareInterruptEnabled[0], FieldMode.Set | FieldMode.Read, name: "COMPARE[0]")
                .WithFlag(17, out eventCompareInterruptEnabled[1], FieldMode.Set | FieldMode.Read, name: "COMPARE[1]")
                .WithFlag(18, out eventCompareInterruptEnabled[2], FieldMode.Set | FieldMode.Read, name: "COMPARE[2]")
                .WithFlag(19, out eventCompareInterruptEnabled[3], FieldMode.Set | FieldMode.Read, name: "COMPARE[3]")
                .WithFlag(20, out eventCompareInterruptEnabled[4], FieldMode.Set | FieldMode.Read, name: "COMPARE[4]")
                .WithFlag(21, out eventCompareInterruptEnabled[5], FieldMode.Set | FieldMode.Read, name: "COMPARE[5]")
                .WithReservedBits(22, 10)
                .WithChangeCallback((_, __) =>
                {
                    UpdateInterrupts();
                })
            ;

            Register.InterruptEnableClear.Define(this)
                .WithReservedBits(0, 16)
                .WithFlag(16, name: "COMPARE[0]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[0].Value = false; })
                .WithFlag(17, name: "COMPARE[1]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[1].Value = false; })
                .WithFlag(18, name: "COMPARE[2]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[2].Value = false; })
                .WithFlag(19, name: "COMPARE[3]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[3].Value = false; })
                .WithFlag(20, name: "COMPARE[4]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[4].Value = false; })
                .WithFlag(21, name: "COMPARE[5]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[5].Value = false; })
                .WithReservedBits(22, 10)
                .WithChangeCallback((_, __) =>
                {
                    UpdateInterrupts();
                })
            ;

            Register.Mode.Define(this)
                .WithValueField(0, 2, out isInCounterMode, name: "MODE", changeCallback: (_, value) =>
                {
                    // 0 means "timer mode", 1 is "counter (deprecated)", 2 is "low power counter"
                    if(value > 0 && innerTimers[0].Enabled)
                    {
                        this.Log(LogLevel.Error, "Switching timer to COUNTER mode while the timer is running");
                    }
                })
            ;

            Register.Prescaler.Define(this)
                .WithValueField(0, 4, out prescaler, name: "PRESCALER", writeCallback: (_, value) =>
                {
                    foreach(var timer in innerTimers)
                    {
                        timer.Divider = (uint)(1 << (int)value);
                    }
                })
                .WithReservedBits(12, 20)
            ;

            Register.Compare0.DefineMany(this, NumberOfEvents, setup: (register, idx) =>
            {
                register
                    .WithValueField(0, 32, name: "CAPTURE_COMPARE", writeCallback: (_, value) =>
                    {
                        SetCompare(idx, value);
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[idx].Compare;
                    });
            });
        }

        private void SetCompare(int idx, ulong value)
        {
            eventCompareEnabled[idx].Value = false;
            UpdateInterrupts();
            innerTimers[idx].Compare = value;
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            for(var i = 0; i < NumberOfEvents; i++)
            {
                flag |= eventCompareInterruptEnabled[i].Value && eventCompareEnabled[i].Value;
            }

            IRQ.Set(flag);
        }

        private IFlagRegisterField[] eventCompareEnabled;
        private IFlagRegisterField[] eventCompareInterruptEnabled;
        private IValueRegisterField prescaler;
        private IValueRegisterField isInCounterMode;
        private bool timerRunning;

        private readonly ComparingTimer[] innerTimers;

        private const int NumberOfEvents = 6;
        private const int InitialFrequency = 16000000;

        private enum Register : long
        {
            Start = 0x000,
            Stop = 0x004,
            Count = 0x008,
            Clear = 0x00C,
            Shutdown = 0x010,

            Capture0 = 0x040,
            Capture1 = 0x044,
            Capture2 = 0x048,
            Capture3 = 0x04C,
            Capture4 = 0x050,
            Capture5 = 0x054,

            Compare0EventPending = 0x140,
            Compare1EventPending = 0x144,
            Compare2EventPending = 0x148,
            Compare3EventPending = 0x14C,
            Compare4EventPending = 0x150,
            Compare5EventPending = 0x154,

            Shortcuts = 0x200,

            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,

            Mode = 0x504,
            BitMode = 0x508,
            Prescaler = 0x510,

            Compare0 = 0x540,
            Compare1 = 0x544,
            Compare2 = 0x548,
            Compare3 = 0x54C,
            Compare4 = 0x550,
            Compare5 = 0x554
        }
    }
}
