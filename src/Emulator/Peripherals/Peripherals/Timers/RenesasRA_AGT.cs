//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasRA_AGT : IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IWordPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, IKnownSize
    {
        public RenesasRA_AGT(IMachine machine, long lowSpeedOnChipOscillatorFrequency, long subClockOscillatorFrequency, long peripheralClockBFrequency)
        {
            this.lowSpeedOnChipOscillatorFrequency = lowSpeedOnChipOscillatorFrequency;
            this.subClockOscillatorFrequency = subClockOscillatorFrequency;
            this.peripheralClockBFrequency = peripheralClockBFrequency;
            this.machine = machine;
            IRQ = new GPIO();
            timer = new LimitTimer(machine.ClockSource, peripheralClockBFrequency, this, "agt", ushort.MaxValue, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += HandleLimitReached;

            channels = new CompareChannel[2];
            for(var i = 0; i < channels.Length; ++i)
            {
                channels[i] = new CompareChannel(machine, peripheralClockBFrequency, this, $"agt-channel{(char)((int)'A' + i)}");
            }

            ByteRegisterCollection = new ByteRegisterCollection(this);
            WordRegisterCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public void Reset()
        {
            timer.Reset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
            WordRegisterCollection.Reset();
            WordRegisterCollection.Reset();
        }

        public byte ReadByte(long offset)
        {
            return ByteRegisterCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            ByteRegisterCollection.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return WordRegisterCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WordRegisterCollection.Write(offset, value);
        }

        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => ByteRegisterCollection;
        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => WordRegisterCollection;

        public long Size => 0x100;
        public long PeripheralClockBFrequency
        {
            get => peripheralClockBFrequency;
            set
            {
                peripheralClockBFrequency = value;
                switch(countSource.Value)
                {
                case CountSource.PeripheralClockB_Div1:
                case CountSource.PeripheralClockB_Div8:
                case CountSource.PeripheralClockB_Div2:
                    Frequency = peripheralClockBFrequency;
                    break;
                default:
                    break;
                }
            }
        }

        public GPIO IRQ { get; }
        public GPIO CompareMatchA => channels[0].IRQ;
        public GPIO CompareMatchB => channels[1].IRQ;

        private void DefineRegisters()
        {
            IProvidesRegisterCollection<WordRegisterCollection> thisWithWordRegisters = this;
            IProvidesRegisterCollection<ByteRegisterCollection> thisWithByteRegisters = this;

            Registers.Counter.Define(thisWithWordRegisters, 0xFFFF)
                .WithValueField(0, 16, name: "Counter And Reload",
                    valueProviderCallback: _ => Value,
                    writeCallback: (_, value) => Limit = (ushort)value
                )
            ;

            Registers.CompareMatchA.Define(thisWithWordRegisters, 0xFFFF)
                .WithValueField(0, 16, name: "Compare Match A",
                    valueProviderCallback: _ => channels[0].CompareValue,
                    writeCallback: (_, value) => channels[0].CompareValue = (ushort)value
                )
            ;

            Registers.CompareMatchB.Define(thisWithWordRegisters, 0xFFFF)
                .WithValueField(0, 16, name: "Compare Match B",
                    valueProviderCallback: _ => channels[1].CompareValue,
                    writeCallback: (_, value) => channels[1].CompareValue = (ushort)value
                )
            ;

            Registers.Control.Define(thisWithByteRegisters)
                .WithFlag(0, name: "Count Start (TSTART)",
                    valueProviderCallback: _ => Enabled,
                    writeCallback: (_, value) =>
                    {
                        if(value && !Enabled)
                        {
                            Value = Limit;
                        }
                        Enabled = value;
                    }
                )
                .WithFlag(1, FieldMode.Read, name: "Count Status Flag (TCSTF)",
                    valueProviderCallback: _ => Enabled
                )
                .WithFlag(2, FieldMode.Write, name: "Count Forces Stop (TSTOP)",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            Enabled = false;
                            Value = ushort.MaxValue;
                        }
                    }
                )
                .WithReservedBits(3, 1)
                .WithTaggedFlag("Active Edge Judgment Flag (TEDGF)", 4)
                .WithFlag(5, out underflowFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Underflow Flag (TUNDF)")
                .WithFlag(6, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Compare Match A Flag (TCMAF)",
                    valueProviderCallback: _ => channels[0].MatchFlag,
                    writeCallback: (_, value) => channels[0].MatchFlag &= value
                )
                .WithFlag(7, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Compare Match B Flag (TCMBF)",
                    valueProviderCallback: _ => channels[1].MatchFlag,
                    writeCallback: (_, value) => channels[1].MatchFlag &= value
                )
            ;

            Registers.Mode1.Define(thisWithByteRegisters)
                .WithEnumField<ByteRegister, OperatingMode>(0, 3, out operatingMode, name: "Operating Mode (TMOD)",
                    writeCallback: (_, __) =>
                    {
                        switch(operatingMode.Value)
                        {
                        case OperatingMode.Timer:
                        case OperatingMode.PulseOutput:
                            Limit = ushort.MaxValue;
                            break;
                        case OperatingMode.EventCounter:
                        case OperatingMode.PuleWidthMeasurement:
                        case OperatingMode.PulsePeriodMeasurement:
                            this.Log(LogLevel.Error, "Unimplemented operating mode ({0}). Ignoring write.", operatingMode.Value);
                            break;
                        default:
                            this.Log(LogLevel.Error, "Illegal operating mode (0x{0:X}). Ignoring write.", operatingMode.Value);
                            break;
                        }
                    }
                )
                .WithTaggedFlag("Edge Polarity (TEDGPL)", 3)
                .WithEnumField<ByteRegister, CountSource>(4, 3, out countSource, name: "Count Source (TCK)",
                    writeCallback: (previousValue, _) =>
                    {
                        switch(countSource.Value)
                        {
                        case CountSource.PeripheralClockB_Div1:
                            Frequency = peripheralClockBFrequency;
                            Divider = 1;
                            break;
                        case CountSource.PeripheralClockB_Div8:
                            Frequency = peripheralClockBFrequency;
                            Divider = 8;
                            break;
                        case CountSource.PeripheralClockB_Div2:
                            Frequency = peripheralClockBFrequency;
                            Divider = 2;
                            break;
                        case CountSource.LowSpeedOnChipOscillator:
                            Frequency = lowSpeedOnChipOscillatorFrequency;
                            Divider = 1 << (int)divider.Value;
                            break;
                        case CountSource.UnderflowEventFromAGT:
                            this.Log(LogLevel.Error, "Unimplemented count source selected ({0}). Ignoring write.", countSource.Value);
                            countSource.Value = previousValue;
                            break;
                        case CountSource.SubClockOscillator:
                            Frequency = subClockOscillatorFrequency;
                            Divider = 1 << (int)divider.Value;
                            break;
                        default:
                            this.Log(LogLevel.Error, "Illegal count source selected (0x{0:X}). Ignoring write.", countSource.Value);
                            countSource.Value = previousValue;
                            break;
                        }
                    }
                )
                .WithReservedBits(7, 1)
            ;

            Registers.Mode2.Define(thisWithByteRegisters)
                .WithValueField(0, 3, out divider, name: "Source Clock Frequency Division Ratio (CKS)",
                    writeCallback: (_, __) =>
                    {
                        switch(countSource.Value)
                        {
                        case CountSource.LowSpeedOnChipOscillator:
                        case CountSource.SubClockOscillator:
                            Divider = 1 << (int)divider.Value;
                            break;
                        default:
                            return;
                        }
                    }
                )
                .WithReservedBits(3, 4)
                .WithTaggedFlag("Low Power Mode (LPM)", 7)
            ;

            Registers.IOControl.Define(thisWithByteRegisters)
                .WithTaggedFlag("I/O Polarity Switch (TEDGSEL)", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("AGTOn pin Output Enable (TOE)", 2)
                .WithReservedBits(3, 1)
                .WithTag("Input Filter (TIPF)", 4, 2)
                .WithTag("Count Control (TIOGT)", 6, 2)
            ;

            Registers.EventPinSelect.Define(thisWithByteRegisters)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("AGTEEn Polarity Selection (EEPS)", 2)
                .WithReservedBits(3, 5)
            ;

            Registers.CompareMatchFunctionSelect.Define(thisWithByteRegisters)
                .WithFlag(0, name: "Compare Match A Register Enable (TCMEA)",
                    valueProviderCallback: _ => channels[0].CompareMatchEnabled,
                    writeCallback: (_, value) => channels[0].CompareMatchEnabled = value
                )
                .WithTaggedFlag("AGTOAn Pin Output Enable (TOEA)", 1)
                .WithTaggedFlag("AGTOAn Pin Polarity Select (TOPOLA)", 2)
                .WithReservedBits(3, 1)
                .WithFlag(4, name: "Compare Match B Register Enable (TCMEB)",
                    valueProviderCallback: _ => channels[1].CompareMatchEnabled,
                    writeCallback: (_, value) => channels[1].CompareMatchEnabled = value
                )
                .WithTaggedFlag("AGTOBn Pin Output Enable (TOEB)", 5)
                .WithTaggedFlag("AGTOBn Pin Polarity Select (TOPOLB)", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.PinSelect.Define(thisWithByteRegisters)
                .WithTag("AGTIOn Pin Select (SEL)", 0, 2)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("AGTIOn Pin Input Enable (TIES)", 4)
                .WithReservedBits(5, 3)
            ;
        }

        private void HandleLimitReached()
        {
            foreach(var channel in channels)
            {
                channel.Restart();
            }
            underflowFlag.Value = true;
            IRQ.Blink();
            this.Log(LogLevel.Debug, "IRQ blinked");
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private ushort Limit
        {
            get => (ushort)timer.Limit;
            set
            {
                timer.Limit = value;
                foreach(var channel in channels)
                {
                    channel.Limit = value;
                }
            }
        }

        private ushort Value
        {
            get
            {
                TrySyncTime();
                return (ushort)timer.Value;
            }
            set
            {
                timer.Value = value;
                foreach(var channel in channels)
                {
                    channel.Value = value;
                }
            }
        }

        private bool Enabled
        {
            get => timer.Enabled;
            set
            {
                timer.Enabled = value;
                foreach(var channel in channels)
                {
                    channel.Enabled = value;
                }
            }
        }

        private long Frequency
        {
            set
            {
                timer.Frequency = value;
                foreach(var channel in channels)
                {
                    channel.Frequency = value;
                }
            }
        }

        private int Divider
        {
            set
            {
                timer.Divider = value;
                foreach(var channel in channels)
                {
                    channel.Divider = value;
                }
            }
        }

        private ByteRegisterCollection ByteRegisterCollection { get; }
        private WordRegisterCollection WordRegisterCollection { get; }

        private IFlagRegisterField underflowFlag;
        private IEnumRegisterField<OperatingMode> operatingMode;
        private IEnumRegisterField<CountSource> countSource;
        private IValueRegisterField divider;

        private long peripheralClockBFrequency;

        private readonly long lowSpeedOnChipOscillatorFrequency;
        private readonly long subClockOscillatorFrequency;

        private readonly IMachine machine;
        private readonly LimitTimer timer;
        private readonly CompareChannel[] channels;

        public enum Registers
        {
            Counter                     = 0x00,
            CompareMatchA               = 0x02,
            CompareMatchB               = 0x04,
            Control                     = 0x08,
            Mode1                       = 0x09,
            Mode2                       = 0x0A,
            IOControl                   = 0x0C,
            EventPinSelect              = 0x0D,
            CompareMatchFunctionSelect  = 0x0E,
            PinSelect                   = 0x0F,
        }

        private enum OperatingMode
        {
            Timer = 0b000,
            PulseOutput = 0b001,
            EventCounter = 0b010,
            PuleWidthMeasurement = 0b011,
            PulsePeriodMeasurement = 0b100,
        }

        private enum CountSource
        {
            PeripheralClockB_Div1 = 0b000,
            PeripheralClockB_Div8 = 0b001,
            PeripheralClockB_Div2 = 0b011,
            LowSpeedOnChipOscillator = 0b100,
            UnderflowEventFromAGT = 0b101,
            SubClockOscillator = 0b111,
        }

        private class CompareChannel
        {
            public CompareChannel(IMachine machine, long frequency, IPeripheral parent, string localName)
            {
                this.parent = parent;
                this.name = localName;
                IRQ = new GPIO();
                innerTimer = new LimitTimer(machine.ClockSource, frequency, parent, localName, ushort.MaxValue, workMode: WorkMode.OneShot, eventEnabled: true);
                innerTimer.LimitReached += () =>
                {
                    MatchFlag = true;
                    Running = false;
                    IRQ.Blink();
                    parent.Log(LogLevel.Debug, "{0}.IRQ blinked ", name);
                };
            }

            public void Reset()
            {
                innerTimer.Reset();
                compareMatchEnabled = false;
                enabled = false;
                running = false;
                compareValue = ushort.MaxValue;
                Limit = ushort.MaxValue;
            }

            public void Restart()
            {
                Running = Limit < CompareValue;
                if(!Running)
                {
                    return;
                }
                innerTimer.Limit = (ulong)(Limit - CompareValue);
                innerTimer.ResetValue();
            }

            public GPIO IRQ { get; }

            public ushort CompareValue
            {
                get => compareValue;
                set
                {
                    var counterValue = Value;
                    compareValue = value;
                    if(Limit < CompareValue)
                    {
                        Running = false;
                        return;
                    }
                    innerTimer.Limit = (ulong)(Limit - CompareValue);
                    if(CompareValue > counterValue)
                    {
                        Running = false;
                        return;
                    }
                    Value = counterValue;
                    Running = true;
                }
            }

            public ushort Limit
            {
                private get => limit;
                set
                {
                    limit = value;
                    if(Limit < CompareValue)
                    {
                        return;
                    }
                    innerTimer.Limit = (ulong)(Limit - CompareValue);
                }
            }

            public ushort Value
            {
                private get => (ushort)((ushort)innerTimer.Value + CompareValue);
                set
                {
                    Running = value >= CompareValue;
                    if(!Running)
                    {
                        return;
                    }
                    innerTimer.Value = (ulong)(value - CompareValue);
                }
            }

            public bool CompareMatchEnabled
            {
                get => compareMatchEnabled;
                set
                {
                    compareMatchEnabled = value;
                    Refresh();
                }
            }

            public bool Enabled
            {
                private get => enabled;
                set
                {
                    enabled = value;
                    Refresh();
                }
            }

            public long Frequency
            {
                set => innerTimer.Frequency = value;
            }

            public int Divider
            {
                set => innerTimer.Divider = value;
            }

            public bool MatchFlag { get; set; }

            private void Refresh()
            {
                innerTimer.Enabled = Enabled && Running && CompareMatchEnabled;
            }

            private bool Running
            {
                get => running;
                set
                {
                    running = value;
                    Refresh();
                }
            }

            private ushort compareValue;
            private ushort limit;
            private bool compareMatchEnabled;
            private bool enabled;
            private bool running;

            private readonly string name;

            private readonly IPeripheral parent;
            private readonly LimitTimer innerTimer;
        }
    }
}
