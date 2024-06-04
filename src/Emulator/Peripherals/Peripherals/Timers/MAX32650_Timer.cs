//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MAX32650_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_Timer(IMachine machine, MAX32650_GCR gcr) : base(machine.ClockSource, gcr.SysClk / 2, eventEnabled: true, direction: Direction.Ascending)
        {
            registers = new DoubleWordRegisterCollection(this, DefineRegisters());
            this.machine = machine;

            gcr.SysClkChanged += UpdateTimerFrequency;
            LimitReached += OnCompare;

            IRQ = new GPIO();
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public uint ReadDoubleWord(long address)
        {
            return registers.Read(address);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            IRQ.Unset();
            prescaler = 0;
        }

        public long Size => 0x400;

        public GPIO IRQ { get; }

        private void UpdateTimerFrequency(long newSysClkFrequency)
        {
            // Peripheral clock frequency is half of the System clock frequency
            Frequency = newSysClkFrequency / 2;
        }

        private void OnCompare()
        {
            Value = 1;
            interruptPending.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interruptPending.Value);
        }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            return new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "CNT.count",
                        valueProviderCallback: _ => (uint)Value,
                        changeCallback: (_, value) => Value = value)
                },
                {(long)Registers.Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "CMP.compare",
                        valueProviderCallback: _ => (uint)Limit,
                        changeCallback: (_, value) =>
                        {
                            Limit = value;
                            Value = 1;
                        })
                },
                {(long)Registers.Interrupt, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptPending, name: "INT.irq",
                        writeCallback: (_, __) => interruptPending.Value = false)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Control, new DoubleWordRegister(this, 0x1000)
                    .WithEnumField<DoubleWordRegister, TimerMode>(0, 3, name: "CN.tmode",
                        changeCallback: (_, newMode) =>
                        {
                            switch(newMode)
                            {
                                case TimerMode.OneShot:
                                    Mode = WorkMode.OneShot;
                                    break;
                                case TimerMode.Continuous:
                                    Mode = WorkMode.Periodic;
                                    break;
                                default:
                                    this.Log(LogLevel.Warning, "Timer mode set to an unsupported mode: {0}; ignoring", newMode);
                                    break;
                            }
                        })
                    .WithValueField(3, 3, name: "CN.pres",
                        writeCallback: (_, val) =>
                        {
                            prescaler = (prescaler & 0x8) | (uint)val;
                            Divider = 1 << (int)prescaler;
                        })
                    .WithTaggedFlag("CN.tpol", 6)
                    .WithFlag(7, name: "CN.ten",
                        valueProviderCallback: _ => Enabled,
                        changeCallback: (_, value) =>
                        {
                            Enabled = value;
                            Value = 1;
                        })
                    .WithFlag(8, name: "CN.pres3",
                        writeCallback: (_, val) =>
                        {
                            BitHelper.SetBit(ref prescaler, 3, val);
                            Divider = 1 << (int)prescaler;
                        })
                    .WithTaggedFlag("CN.pwmsync", 9)
                    .WithTaggedFlag("CN.nolhpol", 10)
                    .WithTaggedFlag("CN.nollpol", 11)
                    // We are using flag instead of tagged flag to hush down unnecessary log messages
                    .WithFlag(12, name: "CN.pwmckbd")
                    .WithReservedBits(13, 19)
                }
            };
        }

        private uint prescaler;

        private IFlagRegisterField interruptPending;

        private readonly IMachine machine;
        private readonly DoubleWordRegisterCollection registers;

        private enum TimerMode : byte
        {
            OneShot = 0x00,
            Continuous,
            Counter,
            PWM,
            Capture,
            Compare,
            Gated,
            CaptureCompare,
        }

        private enum Registers : long
        {
            Counter = 0x00,
            Compare = 0x04,
            PWM = 0x08,
            Interrupt = 0x0C,
            Control = 0x10,
            NonOverlappingCompare = 0x14,
        }
    }
}
