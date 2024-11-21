//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MSP430_Timer : BasicWordPeripheral
    {
        public MSP430_Timer(IMachine machine, long baseFrequency = 32768, int captureCompareCount = 7) : base(machine)
        {
            if(captureCompareCount <= 0 || captureCompareCount > 7)
            {
                throw new ConstructionException("captureCompareCount should be between 1 and 7");
            }

            TimersCount = captureCompareCount;

            mainTimer = new LimitTimer(machine.ClockSource, baseFrequency, this, "clk", limit: ushort.MaxValue, workMode: WorkMode.OneShot, eventEnabled: true);
            internalTimers = Enumerable.Range(0, TimersCount)
                .Select(idx =>
                {
                    var timer = new LimitTimer(machine.ClockSource, baseFrequency, this, $"compare {idx}", limit: ushort.MaxValue, workMode: WorkMode.OneShot, eventEnabled: true, direction: Direction.Ascending);
                    var index = idx;
                    timer.LimitReached += delegate
                    {
                        timerInterruptPending[index].Value = true;
                        UpdateInterrupts();
                    };
                    return timer;
                })
                .ToArray()
            ;

            timerInterruptEnabled = new IFlagRegisterField[TimersCount];
            timerInterruptPending = new IFlagRegisterField[TimersCount];
            timerCompare = new IValueRegisterField[TimersCount];

            mainTimer.LimitReached += LimitReached;

            DefineRegisters();
        }

        [ConnectionRegionAttribute("interruptVector")]
        public void WriteWordToInterruptVector(long offset, ushort value)
        {
            if(offset != 0)
            {
                this.Log(LogLevel.Warning, "Illegal write access at non-zero offset (0x{0:X}) to interruptVector region", offset);
            }
            // NOTE: This region is single word wide, so we are ignoring offset argument
            WriteWord((long)Registers.InterruptVector, value);
        }

        [ConnectionRegionAttribute("interruptVector")]
        public ushort ReadWordFromInterruptVector(long offset)
        {
            if(offset != 0)
            {
                this.Log(LogLevel.Warning, "Illegal read access at non-zero offset (0x{0:X}) to interruptVector region", offset);
            }
            // NOTE: This region is single word wide, so we are ignoring offset argument
            return ReadWord((long)Registers.InterruptVector);
        }

        // TODO: This function is used in resc files to route finished interrupt from CPU to Timer
        public void OnInterruptAcknowledged()
        {
            timerInterruptPending[0].Value = false;
            UpdateInterrupts();
        }

        public int TimersCount { get; }

        public GPIO IRQ0 { get; } = new GPIO();
        public GPIO IRQ_IV { get; } = new GPIO();

        private void LimitReached()
        {
            if(timerMode.Value == Mode.UpDown)
            {
                mainTimer.Direction = mainTimer.Direction == Direction.Ascending ? Direction.Descending : Direction.Ascending;
                mainTimer.Value = mainTimer.Direction == Direction.Ascending ? 0 : mainTimer.Limit;
            }

            if(timerMode.Value != Mode.Stop)
            {
                mainTimer.Enabled = true;
                RecalculateCompareTimers();
            }

            interruptOverflowPending.Value |= timerMode.Value == Mode.Up;
            UpdateInterrupts();
        }

        private void RecalculateCompareTimers()
        {
            var currentCount = mainTimer.Direction == Direction.Ascending ? mainTimer.Value : mainTimer.Limit - mainTimer.Value;
            foreach(var entry in internalTimers.Select((Timer, Index) => new { Timer, Index }))
            {
                var newLimit = mainTimer.Direction == Direction.Ascending ? timerCompare[entry.Index].Value : mainTimer.Limit - timerCompare[entry.Index].Value;
                entry.Timer.Value = currentCount;
                entry.Timer.Limit = newLimit;
                entry.Timer.Enabled |= mainTimer.Enabled && entry.Timer.Value <= entry.Timer.Limit;
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = timerInterruptPending[0].Value && timerInterruptEnabled[0].Value;
            this.Log(LogLevel.Debug, "IRQ0: {0}", interrupt);
            IRQ0.Set(interrupt);

            var interruptVectorIndex = Enumerable
                .Range(1, internalTimers.Length - 1)
                .FirstOrDefault(index => timerInterruptPending[index].Value && timerInterruptEnabled[index].Value);

            var interruptVector = interruptVectorIndex > 0;
            interruptVector |= interruptOverflowEnabled.Value && interruptOverflowPending.Value;
            this.Log(LogLevel.Debug, "IRQ_IV: {0}", interruptVector);
            IRQ_IV.Set(interruptVector);
        }

        private void UpdateDivider()
        {
            Divider = (1 << (int)clockDivider.Value) * ((int)clockDividerExtended.Value + 1);
        }

        private void UpdateMode()
        {
            switch(timerMode.Value)
            {
                case Mode.Stop:
                    Enabled = false;
                    return;
                case Mode.Up:
                    mainTimer.Direction = Direction.Ascending;
                    mainTimer.Limit = timerCompare[0].Value;
                    break;
                case Mode.Continuous:
                    mainTimer.Direction = Direction.Ascending;
                    var bits = timerWidth.Value == 0 ? 16 : 16 - 2 * ((int)timerWidth.Value + 1);
                    mainTimer.Limit = (1UL << bits) - 1;
                    break;
                case Mode.UpDown:
                    mainTimer.Limit = timerCompare[0].Value;
                    break;
                default:
                    throw new Exception("unreachable");
            }

            mainTimer.Enabled = true;
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                // NOTE: Interrupt flag
                .WithFlag(0, out interruptOverflowPending, name: "TBIFG")
                // NOTE: Interrupt enable
                .WithFlag(1, out interruptOverflowEnabled, name: "TBIE")
                // NOTE: Interrupt clear
                .WithFlag(2, FieldMode.WriteOneToClear, name: "TBCLR",
                    writeCallback: (_, value) =>
                    {
                        clockDivider.Value = 0;
                        UpdateDivider();

                        if(timerMode.Value == Mode.UpDown)
                        {
                            mainTimer.Direction = Direction.Ascending;
                        }
                        mainTimer.Value = mainTimer.Direction == Direction.Ascending ? 0 : mainTimer.Limit;
                    })
                .WithReservedBits(3, 1)
                // NOTE: Mode
                .WithEnumField(4, 2, out timerMode, name: "MC",
                    changeCallback: (_, __) => UpdateMode())
                // NOTE: Divider, (1 << value)
                .WithValueField(6, 2, out clockDivider, name: "ID",
                    changeCallback: (_, __) => UpdateDivider())
                // NOTE: Clock select
                .WithTag("TBSSEL", 8, 2)
                .WithReservedBits(10, 1)
                // NOTE: Counter length,
                //       00b=16, 01b=12,
                //       10b=10, 11b= 8
                .WithValueField(11, 2, out timerWidth, name: "CNTL",
                    changeCallback: (_, __) => UpdateMode())
                // NOTE: Group select
                .WithTag("TBCLGRP", 13, 2)
                .WithReservedBits(15, 1)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.CaptureCompareControl0.DefineMany(this, (uint)TimersCount, (register, index) =>
            {
                register
                    .WithFlag(0, out timerInterruptPending[index], name: "CCIFG")
                    .WithTaggedFlag("COV", 1)
                    .WithTaggedFlag("OUT", 2)
                    .WithTaggedFlag("CCI", 3)
                    .WithFlag(4, out timerInterruptEnabled[index], name: "CCIE")
                    .WithTag("OUTMOD", 5, 3)
                    .WithTaggedFlag("CAP", 8)
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("SCCI", 10)
                    .WithTaggedFlag("SCS", 11)
                    .WithTag("CCIS", 12, 2)
                    .WithTag("CM", 14, 2)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                ;
            });

            Registers.Counter.Define(this)
                .WithValueField(0, 16, name: "TxAR",
                    valueProviderCallback: _ => mainTimer.Value,
                    writeCallback: (_, value) => mainTimer.Value = value)
                .WithWriteCallback((_, __) =>
                {
                    RecalculateCompareTimers();
                    UpdateInterrupts();
                })
            ;

            Registers.CaptureCompare0.DefineMany(this, (uint)TimersCount, (register, index) =>
            {
                register
                    .WithValueField(0, 16, out timerCompare[index], name: $"TAxCCR{index}",
                        changeCallback: (_, __) =>
                        {
                            UpdateMode();
                            RecalculateCompareTimers();
                        })
                ;
            });

            Registers.InterruptVector.Define(this)
                .WithValueField(0, 16, name: "TAIV",
                    valueProviderCallback: _ =>
                    {
                        int? index = Enumerable.Range(0, internalTimers.Length - 1).FirstOrDefault(idx => timerInterruptPending[idx].Value);
                        if(!index.HasValue && interruptOverflowPending.Value)
                        {
                            index = internalTimers.Length + 1;
                            interruptOverflowPending.Value = false;
                        }
                        else
                        {
                            timerInterruptPending[index.Value].Value = false;
                        }

                        UpdateInterrupts();
                        return (ulong)(index << 1);
                    },
                    writeCallback: (_, value) =>
                    {
                        if(value != 0)
                        {
                            // NOTE: Writes other than zero are no-op
                            return;
                        }

                        int? firstIndex = Enumerable.Range(0, internalTimers.Length - 1).FirstOrDefault(index => timerInterruptPending[index].Value);
                        if(firstIndex.HasValue)
                        {
                            timerInterruptPending[firstIndex.Value].Value = false;
                        }
                        else
                        {
                            interruptOverflowEnabled.Value = false;
                        }
                        UpdateInterrupts();
                    })
            ;

            Registers.Expansion0.Define(this)
                .WithValueField(0, 3, out clockDividerExtended, name: "TAIDEX",
                    changeCallback: (_, __) => UpdateDivider())
                .WithReservedBits(3, 13)
            ;
        }

        private bool Enabled
        {
            get => mainTimer.Enabled;
            set
            {
                mainTimer.Enabled = value;
                foreach(var timer in internalTimers)
                {
                    timer.Enabled = value;
                }
            }
        }

        private long Frequency
        {
            get => mainTimer.Frequency;
            set
            {
                mainTimer.Frequency = value;
                foreach(var timer in internalTimers)
                {
                    timer.Frequency = value;
                }
            }
        }

        private int Divider
        {
            get => mainTimer.Divider;
            set
            {
                mainTimer.Divider = value;
                foreach(var timer in internalTimers)
                {
                    timer.Divider = value;
                }
            }
        }

        private IFlagRegisterField interruptOverflowEnabled;
        private IFlagRegisterField interruptOverflowPending;

        private IFlagRegisterField[] timerInterruptEnabled;
        private IFlagRegisterField[] timerInterruptPending;
        private IValueRegisterField[] timerCompare;

        private IValueRegisterField clockDivider;
        private IValueRegisterField clockDividerExtended;

        private IEnumRegisterField<Mode> timerMode;
        private IValueRegisterField timerWidth;

        private readonly LimitTimer mainTimer;
        private readonly LimitTimer[] internalTimers;

        private enum Mode
        {
            Stop,
            Up,
            Continuous,
            UpDown,
        }

        private enum Registers
        {
            Control = 0x00,
            CaptureCompareControl0 = 0x02,
            Counter = 0x10,
            CaptureCompare0 = 0x12,
            InterruptVector = 0x2E,
            Expansion0 = 0x20
        }
    }
}
