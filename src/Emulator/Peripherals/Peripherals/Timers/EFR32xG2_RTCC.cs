//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32xG2_RTCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_RTCC(IMachine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();
            interruptManager = new InterruptManager<Interrupt>(this);

            innerTimer = new EFR32_RTCCCounter(machine, frequency, this, "rtcc", CounterWidth, PreCounterWidth, NumberOfCaptureCompareChannels);
            innerTimer.LimitReached += delegate
            {
                interruptManager.SetInterrupt(Interrupt.Overflow);
            };
            innerTimer.CounterTicked += delegate
            {
                interruptManager.SetInterrupt(Interrupt.CounterTick);
            };

            for(var idx = 0; idx < NumberOfCaptureCompareChannels; ++idx)
            {
                var i = idx;
                innerTimer.Channels[i].CompareReached += delegate
                {
                    interruptManager.SetInterrupt(InterruptForChannel(i));
                };
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            interruptManager.Reset();
        }

        [ConnectionRegionAttribute("set")]
        public uint ReadDoubleWordSet(long offset)
        {
            return ReadDoubleWord(offset);
        }

        [ConnectionRegionAttribute("set")]
        public void WriteDoubleWordSet(long offset, uint mask)
        {
            var value = ReadDoubleWord(offset);
            value &= ~(uint)mask;
            WriteDoubleWord(offset, value);
        }

        [ConnectionRegionAttribute("clear")]
        public uint ReadDoubleWordClear(long offset)
        {
            return ReadDoubleWord(offset);
        }

        [ConnectionRegionAttribute("clear")]
        public void WriteDoubleWordClear(long offset, uint mask)
        {
            var value = ReadDoubleWord(offset);
            value &= ~(uint)mask;
            WriteDoubleWord(offset, value);
        }

        [ConnectionRegionAttribute("toggle")]
        public uint ReadDoubleWordToggle(long offset)
        {
            return ReadDoubleWord(offset);
        }

        [ConnectionRegionAttribute("toggle")]
        public void WriteDoubleWordToggle(long offset, uint mask)
        {
            var value = ReadDoubleWord(offset);
            value ^= (uint)mask;
            WriteDoubleWord(offset, value);
        }

        public long Size => 0x1000;

        [IrqProvider]
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.IpVersion.Define(this)
                .WithTag("IPVERSION", 0, 32);

            Register.Enable.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 31);

            Register.Configuration.Define(this)
                .WithTaggedFlag("DEBUGRUN", 0)
                .WithTaggedFlag("PRECNTCCV0TOP", 1)
                .WithTaggedFlag("CNTCCV1TOP", 2)
                .WithTaggedFlag("CNTTICK", 3)
                .WithValueField(4, 4,
                    writeCallback: (_, value) => innerTimer.Prescaler = (int)Math.Pow(2, value),
                    valueProviderCallback: _ => (uint)Math.Log(innerTimer.Prescaler, 2),
                    name: "CNTPRESC")
                .WithReservedBits(8, 24);

            Register.Command.Define(this)
                .WithEnumField<DoubleWordRegister, Command>(0, 2, FieldMode.Write, writeCallback: (_, value) =>
                {
                    switch(value)
                    {
                        case Command.None:
                            break;
                        case Command.Start:
                            innerTimer.Enabled = true;
                            break;
                        case Command.Stop:
                            innerTimer.Enabled = false;
                            break;
                        default:
                            this.Log(LogLevel.Warning, "Unsupported command combination: {0}", value);
                            break;
                    }
                }, name: "START/STOP")
                .WithReservedBits(2, 30);

            Register.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => innerTimer.Enabled, name: "RUNNING")
                .WithTaggedFlag("RTCCLOCKSTATUS", 1)
                .WithReservedBits(2, 30);

            RegistersCollection.AddRegister((long)Register.InterruptFlags, interruptManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => interruptManager.IsSet(irq),
                writeCallback: (irq, _, newValue) => interruptManager.SetInterrupt(irq, newValue)));
            RegistersCollection.AddRegister((long)Register.InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());

            Register.PreCounter.Define(this)
                .WithValueField(0, 15,
                    writeCallback: (_, value) => innerTimer.PreCounter = value,
                    valueProviderCallback: _ => innerTimer.PreCounter,
                    name: "PRECNT")
                .WithReservedBits(15, 17);

            Register.CounterValue.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) => innerTimer.Counter = value,
                    valueProviderCallback: _ => innerTimer.Counter,
                    name: "CNT");

            Register.CombinedPreCounterValue.Define(this)
                .WithValueField(0, 15, FieldMode.Read,
                    valueProviderCallback: _ => innerTimer.PreCounter,
                    name: "PRECNT")
                .WithValueField(15, 17, FieldMode.Read,
                    valueProviderCallback: _ => innerTimer.Counter,
                    name: "CNTLSB");

            Register.SyncBusy.Define(this)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("CMD", 5)
                .WithReservedBits(6, 26);

            Register.Lock.Define(this)
                .WithTag("LOCK", 0, 15)
                .WithReservedBits(15, 17);

            Register.ChanelControlC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithEnumField<DoubleWordRegister, EFR32_RTCCCounter.CCChannelMode>(0, 2,
                        writeCallback: (_, value) => innerTimer.Channels[idx].Mode = value,
                        valueProviderCallback: _ => innerTimer.Channels[idx].Mode,
                        name: "MODE")
                    .WithTag("CMOA", 2, 2)
                    .WithEnumField<DoubleWordRegister, EFR32_RTCCCounter.CCChannelComparisonBase>(4, 1,
                        writeCallback: (_, value) => innerTimer.Channels[idx].ComparisonBase = value,
                        valueProviderCallback: _ => innerTimer.Channels[idx].ComparisonBase,
                        name: "COMPBASE")
                    .WithTag("ICEDGE", 5, 2)
                    .WithReservedBits(7, 25)
                , stepInBytes: StepBetweenCaptureCompareRegisters);

            Register.OutputCompareValueC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => innerTimer.Channels[idx].CompareValue = value,
                        valueProviderCallback: _ => (uint)innerTimer.Channels[idx].CompareValue,
                        name: $"OC[{idx}]")
                , stepInBytes: StepBetweenCaptureCompareRegisters);

            Register.InputCaptureValueC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithTag($"IC[{idx}]", 0, 32)
                , stepInBytes: StepBetweenCaptureCompareRegisters);
        }

        private Interrupt InterruptForChannel(int channel)
        {
            switch(channel)
            {
                case 0: return Interrupt.Channel0;
                case 1: return Interrupt.Channel1;
                case 2: return Interrupt.Channel2;
                default: throw new System.NotImplementedException($"Channel {channel} does not have an IRQ set up");
            }
        }

        private readonly InterruptManager<Interrupt> interruptManager;
        private readonly EFR32_RTCCCounter innerTimer;
        private const int NumberOfCaptureCompareChannels = 3;
        private const int StepBetweenCaptureCompareRegisters = Register.ChanelControlC1 - Register.ChanelControlC0;
        private const int CounterWidth = 32;
        private const int PreCounterWidth = 15;

        private enum Interrupt
        {
            Overflow,
            CounterTick,
            [NotSettable]
            Reserved0,
            [NotSettable]
            Reserved1,
            Channel0,
            [NotSettable]
            Reserved2,
            Channel1,
            [NotSettable]
            Reserved3,
            Channel2,
        }

        private enum Command
        {
            None = 0b00,
            Start = 0b01,
            Stop = 0b10,
        }

        private enum Register
        {
            IpVersion = 0x0,
            Enable = 0x4,
            Configuration = 0x8,
            Command = 0xC,
            Status = 0x10,
            InterruptFlags = 0x14,
            InterruptEnable = 0x18,
            PreCounter = 0x1C,
            CounterValue = 0x20,
            CombinedPreCounterValue = 0x24,
            SyncBusy = 0x28,
            Lock = 0x2C,
            ChanelControlC0 = 0x30,
            OutputCompareValueC0 = 0x34,
            InputCaptureValueC0 = 0x38,
            ChanelControlC1 = 0x3C,
            OutputCompareValueC1 = 0x40,
            InputCaptureValueC1 = 0x44,
            ChanelControlC2 = 0x48,
            OutputCompareValueC2 = 0x4C,
            InputCaptureValueC3 = 0x50,
        }
    }
}
