//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.WordToByte | AllowedTranslation.DoubleWordToByte)]
    public class ITE_IT_ETWD : BasicBytePeripheral, IKnownSize, INumberedGPIOOutput, IHasMappedRegisters
    {
        public ITE_IT_ETWD(IMachine machine, bool version2 = false) : base(machine)
        {
            this.version2 = version2;

            mapper = new RegisterMapper(typeof(Registers));
            mapper.RegisterEnumMapping(RegisterMapOf(version2));

            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfTimers; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            WdogReset = new GPIO();

            channels = new Channel[NumberOfTimers];
            for(var i = 0; i < NumberOfTimers; i++)
            {
                channels[i] = new Channel(machine, this, $"et{i + 1}", CounterWidths[i], Connections[i]);
            }
            watchdog = new Watchdog(machine, this, WdogReset);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
            watchdog.Reset();
            RefreshCombinedTimers();
        }

        public string OffsetToString(long offset) => mapper.ToString(offset);

        public long Size => 0x100;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public GPIO WdogReset { get; }

        protected override void DefineRegisters()
        {
            var registerMap = RegisterMapOf(version2);

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.Reserved0))).WithReservedBits(0, 8);

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer1WDTConfigurationRegister)))
                .WithFlag(0, out lockConfig, name: "Lock ETWCFG Register (LETWCFG)")
                .WithFlag(1, out lockT1Prescaler, name: "Lock ET1PS Register (LET1PS)")
                .WithFlag(2, out lockT1Counter, name: "Lock ET1CNTLx Registers (LET1CNTL)")
                .WithFlag(3, out lockWdtCounter, name: "Lock EWDCNTLx Register (LEWDCNTL)")
                .WithFlag(4, out watchdogClockFromPrescaler, name: "External WDT Clock Source (EWDSRC)")
                .WithFlag(5, out watchdogKeyEnabled, name: "External WDT Key Enabled (EWDKEYEN)")
                .WithReservedBits(6, 2);

            DefinePrescaler(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer1PrescalerRegister)), Timer1Index);
            DefineCounterByte(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer1CounterHighByteRegister)), Timer1Index, 1, startsTimer: false);
            DefineCounterByte(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer1CounterLowByteRegister)), Timer1Index, 0, startsTimer: true);

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimerWDTControlRegister)))
                .WithFlag(0, FieldMode.Write, name: "External Timer 1 Reset (ET1RST)",
                    writeCallback: (_, set) => { if(set) { StartTimer(Timer1Index); } })
                .WithFlag(1, FieldMode.Read, name: "External Timer 1 Terminal Count (ET1TC)", valueProviderCallback: _ => channels[Timer1Index].ReadTerminalCount())
                .WithFlag(2, FieldMode.Write, name: "External Timer 2 Reset (ET2RST)",
                    writeCallback: (_, set) => { if(set) { StartTimer(Timer2Index); } })
                .WithFlag(3, FieldMode.Read, name: "External Timer 2 Terminal Count (ET2TC)", valueProviderCallback: _ => channels[Timer2Index].ReadTerminalCount())
                .WithFlag(4, out watchdogStopModeSelect, name: "External WDT Stop Count Mode Select (EWDSCMS)")
                .WithFlag(5, out watchdogStopCounting, name: "External WDT Stop Count Enable (EWDSCEN)",
                    changeCallback: (_, __) => watchdog.Stopped = watchdogStopCounting.Value)
                .WithReservedBits(6, 2);

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalWDTCounterLowByteRegister)))
                .WithValueField(0, 8, name: "External WDT Counter Low Byte (EWDCNTLL)",
                    valueProviderCallback: _ => watchdog.GetCounterByte(0),
                    writeCallback: (_, value) =>
                    {
                        watchdog.SetClock(FrequencyOf(channels[Timer1Index].Prescaler));
                        watchdog.SetCounterByte(0, (byte)value);
                        watchdog.Touch();
                    });

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalWDTKeyRegister)))
                .WithValueField(0, 8, FieldMode.Write, name: "External WDT Key (EWDKEY)",
                    writeCallback: (_, value) =>
                    {
                        if((byte)value == WatchdogMagicByte)
                        {
                            watchdog.Touch();
                        }
                        else
                        {
                            watchdog.AssertReset();
                        }
                    });

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.Reserved8))).WithReservedBits(0, 8);

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalWDTCounterHighByteRegister)))
                .WithValueField(0, 8, name: "External WDT Counter High Byte (EWDCNTL)",
                    valueProviderCallback: _ => watchdog.GetCounterByte(1),
                    writeCallback: (_, value) => watchdog.SetCounterByte(1, (byte)value));

            DefinePrescaler(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer2PrescalerRegister)), Timer2Index);
            DefineCounterByte(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer2CounterHighByteRegister)), Timer2Index, 1, startsTimer: false);
            DefineCounterByte(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer2CounterLowByteRegister)), Timer2Index, 0, startsTimer: true);
            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ReservedD))).WithReservedBits(0, 8);
            DefineCounterByte(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalTimer2CounterHighByte2Register)), Timer2Index, 2, startsTimer: false);

            for(var byteIndex = 0; byteIndex < CounterWidths[Timer1Index]; byteIndex++)
            {
                DefineObservationByte(OffsetOf(registerMap, ObservationByteName(1, byteIndex)), Timer1Index, byteIndex);
            }
            for(var byteIndex = 0; byteIndex < CounterWidths[Timer2Index]; byteIndex++)
            {
                DefineObservationByte(OffsetOf(registerMap, ObservationByteName(2, byteIndex)), Timer2Index, byteIndex);
            }

            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalWDTCounterObservationLowByteRegister)))
                .WithValueField(0, 8, FieldMode.Read, name: "External WDT Counter Observation Low Byte (EWDCNTOL)", valueProviderCallback: _ => watchdog.GetObservationByte(0));
            RegistersCollection.DefineRegister(OffsetOf(registerMap, nameof(RegistersVersion1.ExternalWDTCounterObservationHighByteRegister)))
                .WithValueField(0, 8, FieldMode.Read, name: "External WDT Counter Observation High Byte (EWDCNTOH)", valueProviderCallback: _ => watchdog.GetObservationByte(1));

            for(var channel = Timer3Index; channel < NumberOfTimers; channel++)
            {
                var timer = channel + 1;
                DefineTimerControl(OffsetOf(typeof(Registers), $"ExternalTimer{timer}ControlRegister"), channel);
                DefinePrescaler(OffsetOf(typeof(Registers), $"ExternalTimer{timer}PrescalerRegister"), channel);
                for(var byteIndex = 0; byteIndex < CounterWidths[channel]; byteIndex++)
                {
                    DefineCounterByte(OffsetOf(typeof(Registers), CounterByteName(timer, byteIndex)), channel, byteIndex, startsTimer: false);
                    DefineObservationByte(OffsetOf(typeof(Registers), ObservationByteName(timer, byteIndex)), channel, byteIndex);
                }
            }
        }

        private static Type RegisterMapOf(bool version2)
        {
            return version2 ? typeof(RegistersVersion2) : typeof(RegistersVersion1);
        }

        private static long OffsetOf(Type registerMap, string name)
        {
            if(!Enum.TryParse(registerMap, name, out var register))
            {
                throw new ConstructionException($"{registerMap.Name} is missing {name}");
            }
            return Convert.ToInt64(register);
        }

        private static string CounterByteName(int timer, int byteIndex)
        {
            return $"ExternalTimer{timer}{CounterByteNames[byteIndex]}";
        }

        private static string ObservationByteName(int timer, int byteIndex)
        {
            return $"ExternalTimer{timer}{ObservationByteNames[byteIndex]}";
        }

        private static ulong FrequencyOf(Prescaler source)
        {
            switch(source)
            {
            case Prescaler.Clock32768Hz:
                return 32768;
            case Prescaler.Clock1024Hz:
                return 1024;
            case Prescaler.Clock32Hz:
                return 32;
            case Prescaler.EcClock:
                return EcClockFrequency;
            default:
                return DefaultFrequency;
            }
        }

        private void RefreshCombinedTimers()
        {
            for(var low = Timer3Index; low + 1 < NumberOfTimers; low += 2)
            {
                var high = low + 1;
                var combined = channels[low].Combine;

                channels[high].CombinedHighTimer = combined;
                channels[low].CombineSink = combined ? channels[high] : null;
            }
        }

        private void DefinePrescaler(long offset, int channel)
        {
            var timer = channel + 1;
            RegistersCollection.DefineRegister(offset)
                .WithValueField(0, 2, name: $"External Timer {timer} Prescaler Select (ET{timer}PS)",
                    valueProviderCallback: _ => (ulong)channels[channel].Prescaler,
                    writeCallback: (_, value) => channels[channel].Prescaler = (Prescaler)value)
                .WithReservedBits(2, 6);
        }

        private void DefineCounterByte(long offset, int channel, int byteIndex, bool startsTimer)
        {
            var timer = channel + 1;
            RegistersCollection.DefineRegister(offset)
                .WithValueField(0, 8, name: $"External Timer {timer} Counter {CounterByteFullNames[byteIndex]} (ET{timer}CNTL{CounterByteAbbreviations[byteIndex]})",
                    valueProviderCallback: _ => channels[channel].GetCounterByte(byteIndex),
                    writeCallback: (_, value) =>
                    {
                        channels[channel].SetCounterByte(byteIndex, (byte)value);
                        if(startsTimer)
                        {
                            StartTimer(channel);
                        }
                    });
        }

        private void DefineObservationByte(long offset, int channel, int byteIndex)
        {
            var timer = channel + 1;
            RegistersCollection.DefineRegister(offset)
                .WithValueField(0, 8, FieldMode.Read, name: $"External Timer {timer} Counter Observation {CounterByteFullNames[byteIndex]} (ET{timer}CNTO{CounterByteAbbreviations[byteIndex]})",
                    valueProviderCallback: _ => channels[channel].GetObservationByte(byteIndex));
        }

        private void DefineTimerControl(long offset, int channel)
        {
            var timer = channel + 1;
            RegistersCollection.DefineRegister(offset)
                .WithValueField(0, 8, name: $"External Timer {timer} Control Register (ET{timer}CTRL)",
                    valueProviderCallback: _ =>
                    {
                        byte value = 0;
                        if(channels[channel].Enabled)
                        {
                            value |= TimerEnableBit;
                        }
                        if(channels[channel].ReadTerminalCount())
                        {
                            value |= TimerTerminalCountBit;
                        }
                        if(channels[channel].Combine)
                        {
                            value |= TimerCombineBit;
                        }
                        return value;
                    },
                    writeCallback: (_, value) =>
                    {
                        channels[channel].Combine = ((byte)value & TimerCombineBit) != 0;
                        var enable = ((byte)value & TimerEnableBit) != 0;
                        var reset = ((byte)value & TimerResetBit) != 0;
                        if(reset || (enable && !channels[channel].Enabled))
                        {
                            channels[channel].Reload();
                        }
                        channels[channel].Enabled = enable;
                    })
                .WithWriteCallback((_, __) => RefreshCombinedTimers());
        }

        private void StartTimer(int channel)
        {
            channels[channel].Reload();
            channels[channel].Enabled = true;
        }

        private IFlagRegisterField lockConfig;
        private IFlagRegisterField lockT1Prescaler;
        private IFlagRegisterField lockT1Counter;
        private IFlagRegisterField lockWdtCounter;
        private IFlagRegisterField watchdogClockFromPrescaler;
        private IFlagRegisterField watchdogKeyEnabled;
        private IFlagRegisterField watchdogStopModeSelect;
        private IFlagRegisterField watchdogStopCounting;

        private readonly bool version2;
        private readonly RegisterMapper mapper;
        private readonly Channel[] channels;
        private readonly Watchdog watchdog;

        private static readonly int[] CounterWidths = { 2, 3, 3, 4, 3, 4, 3, 4 };
        private static readonly string[] CounterByteNames = { "CounterLowByteRegister", "CounterHighByteRegister", "CounterHighByte2Register", "CounterHighByte3Register" };
        private static readonly string[] ObservationByteNames = { "CounterObservationLowByteRegister", "CounterObservationHighByteRegister", "CounterObservationHighByte2Register", "CounterObservationHighByte3Register" };
        private static readonly string[] CounterByteFullNames = { "Low Byte", "High Byte", "High Byte 2", "High Byte 3" };
        private static readonly string[] CounterByteAbbreviations = { "L", "H", "H2", "H3" };

        private const int NumberOfTimers = 8;
        private const int Timer1Index = 0;
        private const int Timer2Index = 1;
        private const int Timer3Index = 2;
        private const int CounterByteCount = 4;
        private const ulong DefaultFrequency = 32768;
        private const ulong EcClockFrequency = 24000000;
        private const byte WatchdogMagicByte = 0x5c;
        private const byte TimerEnableBit = 1 << 0;
        private const byte TimerResetBit = 1 << 1;
        private const byte TimerTerminalCountBit = 1 << 2;
        private const byte TimerCombineBit = 1 << 3;

        private class Channel
        {
            public Channel(IMachine machine, IPeripheral owner, string name, int counterWidth, IGPIO irqLine)
            {
                this.counterWidth = counterWidth;
                this.irqLine = irqLine;
                reloadBytes = new byte[CounterByteCount];
                timer = new LimitTimer(machine.ClockSource, DefaultFrequency, owner, name,
                    limit: uint.MaxValue, direction: Direction.Descending, workMode: WorkMode.Periodic, eventEnabled: true);
                timer.LimitReached += OnExpired;
            }

            public void Reset()
            {
                timer.Reset();
                for(var i = 0; i < reloadBytes.Length; i++)
                {
                    reloadBytes[i] = 0;
                }
                Prescaler = Prescaler.Clock32768Hz;
                Combine = false;
                CombineSink = null;
                combinedHighTimer = false;
                enabled = false;
                terminalCount = false;
                irqLine.Unset();
            }

            public void SetCounterByte(int index, byte value) => reloadBytes[index] = value;

            public byte GetCounterByte(int index) => reloadBytes[index];

            public byte GetObservationByte(int index)
            {
                var value = combinedHighTimer ? ~timer.Value : timer.Value;
                return (byte)(value >> (index * 8));
            }

            public bool ReadTerminalCount()
            {
                var value = terminalCount;
                terminalCount = false;
                return value;
            }

            public void Reload()
            {
                ulong count = 0;
                for(var i = 0; i < counterWidth; i++)
                {
                    count |= (ulong)reloadBytes[i] << (i * 8);
                }
                timer.Limit = count == 0 ? 1 : count;
                timer.Value = timer.Limit;
                terminalCount = false;
            }

            public void Tick()
            {
                if(!enabled)
                {
                    return;
                }

                if(timer.Value > 0)
                {
                    timer.Value -= 1;
                }
                if(timer.Value == 0)
                {
                    Reload();
                    Expire();
                }
            }

            public Prescaler Prescaler
            {
                get => prescaler;
                set { prescaler = value; timer.Frequency = FrequencyOf(value); }
            }

            public bool Combine { get; set; }

            public Channel CombineSink { get; set; }

            public bool CombinedHighTimer
            {
                get => combinedHighTimer;
                set
                {
                    if(combinedHighTimer == value)
                    {
                        return;
                    }
                    combinedHighTimer = value;
                    UpdateTimerEnabled();
                }
            }

            public bool Enabled
            {
                get => enabled;
                set
                {
                    enabled = value;
                    UpdateTimerEnabled();
                }
            }

            private void OnExpired()
            {
                Expire();
                CombineSink?.Tick();
            }

            private void Expire()
            {
                terminalCount = true;
                irqLine.Blink();
            }

            private void UpdateTimerEnabled() => timer.Enabled = enabled && !combinedHighTimer;

            private Prescaler prescaler;
            private bool terminalCount;
            private bool enabled;
            private bool combinedHighTimer;

            private readonly LimitTimer timer;
            private readonly IGPIO irqLine;
            private readonly int counterWidth;
            private readonly byte[] reloadBytes;
        }

        private class Watchdog
        {
            public Watchdog(IMachine machine, IPeripheral owner, IGPIO resetLine)
            {
                this.resetLine = resetLine;
                reloadBytes = new byte[2];
                timer = new LimitTimer(machine.ClockSource, DefaultFrequency, owner, "wdt",
                    limit: uint.MaxValue, direction: Direction.Descending, workMode: WorkMode.OneShot, eventEnabled: true);
                timer.LimitReached += OnExpired;
            }

            public void Touch()
            {
                var count = ((ulong)reloadBytes[1] << 8) | reloadBytes[0];
                if(count == 0 || stopped)
                {
                    timer.Enabled = false;
                    return;
                }
                timer.Limit = count;
                timer.Value = count;
                timer.Enabled = true;
            }

            public void AssertReset() => resetLine.Set(true);

            public void Reset()
            {
                timer.Reset();
                reloadBytes[0] = 0;
                reloadBytes[1] = 0;
                stopped = false;
                resetLine.Unset();
            }

            public void SetClock(ulong frequency) => timer.Frequency = frequency;

            public void SetCounterByte(int index, byte value) => reloadBytes[index] = value;

            public byte GetCounterByte(int index) => reloadBytes[index];

            public byte GetObservationByte(int index) => (byte)(timer.Value >> (index * 8));

            public bool Stopped
            {
                get => stopped;
                set { stopped = value; if(stopped) { timer.Enabled = false; } }
            }

            private void OnExpired() => AssertReset();

            private bool stopped;

            private readonly LimitTimer timer;
            private readonly IGPIO resetLine;
            private readonly byte[] reloadBytes;
        }

        private enum Prescaler
        {
            Clock32768Hz = 0,
            Clock1024Hz = 1,
            Clock32Hz = 2,
            EcClock = 3,
        }

        private enum Registers : long
        {
            ExternalTimer3ControlRegister                     = 0x10,
            ExternalTimer3PrescalerRegister                   = 0x11,
            ExternalTimer3CounterLowByteRegister              = 0x14,
            ExternalTimer3CounterHighByteRegister             = 0x15,
            ExternalTimer3CounterHighByte2Register            = 0x16,
            ExternalTimer4ControlRegister                     = 0x18,
            ExternalTimer4PrescalerRegister                   = 0x19,
            ExternalTimer4CounterLowByteRegister              = 0x1C,
            ExternalTimer4CounterHighByteRegister             = 0x1D,
            ExternalTimer4CounterHighByte2Register            = 0x1E,
            ExternalTimer4CounterHighByte3Register            = 0x1F,
            ExternalTimer5ControlRegister                     = 0x20,
            ExternalTimer5PrescalerRegister                   = 0x21,
            ExternalTimer5CounterLowByteRegister              = 0x24,
            ExternalTimer5CounterHighByteRegister             = 0x25,
            ExternalTimer5CounterHighByte2Register            = 0x26,
            ExternalTimer6ControlRegister                     = 0x28,
            ExternalTimer6PrescalerRegister                   = 0x29,
            ExternalTimer6CounterLowByteRegister              = 0x2C,
            ExternalTimer6CounterHighByteRegister             = 0x2D,
            ExternalTimer6CounterHighByte2Register            = 0x2E,
            ExternalTimer6CounterHighByte3Register            = 0x2F,
            ExternalTimer7ControlRegister                     = 0x30,
            ExternalTimer7PrescalerRegister                   = 0x31,
            ExternalTimer7CounterLowByteRegister              = 0x34,
            ExternalTimer7CounterHighByteRegister             = 0x35,
            ExternalTimer7CounterHighByte2Register            = 0x36,
            ExternalTimer8ControlRegister                     = 0x38,
            ExternalTimer8PrescalerRegister                   = 0x39,
            ExternalTimer8CounterLowByteRegister              = 0x3C,
            ExternalTimer8CounterHighByteRegister             = 0x3D,
            ExternalTimer8CounterHighByte2Register            = 0x3E,
            ExternalTimer8CounterHighByte3Register            = 0x3F,
            ExternalTimer3CounterObservationLowByteRegister   = 0x48,
            ExternalTimer3CounterObservationHighByteRegister  = 0x49,
            ExternalTimer3CounterObservationHighByte2Register = 0x4A,
            ExternalTimer4CounterObservationLowByteRegister   = 0x4C,
            ExternalTimer4CounterObservationHighByteRegister  = 0x4D,
            ExternalTimer4CounterObservationHighByte2Register = 0x4E,
            ExternalTimer4CounterObservationHighByte3Register = 0x4F,
            ExternalTimer5CounterObservationLowByteRegister   = 0x50,
            ExternalTimer5CounterObservationHighByteRegister  = 0x51,
            ExternalTimer5CounterObservationHighByte2Register = 0x52,
            ExternalTimer6CounterObservationLowByteRegister   = 0x54,
            ExternalTimer6CounterObservationHighByteRegister  = 0x55,
            ExternalTimer6CounterObservationHighByte2Register = 0x56,
            ExternalTimer6CounterObservationHighByte3Register = 0x57,
            ExternalTimer7CounterObservationLowByteRegister   = 0x58,
            ExternalTimer7CounterObservationHighByteRegister  = 0x59,
            ExternalTimer7CounterObservationHighByte2Register = 0x5A,
            ExternalTimer8CounterObservationLowByteRegister   = 0x5C,
            ExternalTimer8CounterObservationHighByteRegister  = 0x5D,
            ExternalTimer8CounterObservationHighByte2Register = 0x5E,
            ExternalTimer8CounterObservationHighByte3Register = 0x5F,
        }

        private enum RegistersVersion1 : long
        {
            Reserved0                                         = 0x00,
            ExternalTimer1WDTConfigurationRegister            = 0x01,
            ExternalTimer1PrescalerRegister                   = 0x02,
            ExternalTimer1CounterHighByteRegister             = 0x03,
            ExternalTimer1CounterLowByteRegister              = 0x04,
            ExternalTimerWDTControlRegister                   = 0x05,
            ExternalWDTCounterLowByteRegister                 = 0x06,
            ExternalWDTKeyRegister                            = 0x07,
            Reserved8                                         = 0x08,
            ExternalWDTCounterHighByteRegister                = 0x09,
            ExternalTimer2PrescalerRegister                   = 0x0A,
            ExternalTimer2CounterHighByteRegister             = 0x0B,
            ExternalTimer2CounterLowByteRegister              = 0x0C,
            ReservedD                                         = 0x0D,
            ExternalTimer2CounterHighByte2Register            = 0x0E,
            ExternalTimer1CounterObservationLowByteRegister   = 0x40,
            ExternalTimer1CounterObservationHighByteRegister  = 0x41,
            ExternalTimer2CounterObservationLowByteRegister   = 0x44,
            ExternalTimer2CounterObservationHighByteRegister  = 0x45,
            ExternalTimer2CounterObservationHighByte2Register = 0x46,
            ExternalWDTCounterObservationLowByteRegister      = 0x60,
            ExternalWDTCounterObservationHighByteRegister     = 0x61,
        }

        private enum RegistersVersion2 : long
        {
            Reserved0                                         = 0x80,
            ExternalTimer1WDTConfigurationRegister            = 0x81,
            ExternalTimer1PrescalerRegister                   = 0x82,
            ExternalTimer1CounterHighByteRegister             = 0x83,
            ExternalTimer1CounterLowByteRegister              = 0x84,
            ExternalTimerWDTControlRegister                   = 0x85,
            ExternalWDTCounterLowByteRegister                 = 0x86,
            ExternalWDTKeyRegister                            = 0x87,
            Reserved8                                         = 0x88,
            ExternalWDTCounterHighByteRegister                = 0x89,
            ExternalTimer2PrescalerRegister                   = 0x8A,
            ExternalTimer2CounterHighByteRegister             = 0x8B,
            ExternalTimer2CounterLowByteRegister              = 0x8C,
            ReservedD                                         = 0x8D,
            ExternalTimer2CounterHighByte2Register            = 0x8E,
            ExternalTimer1CounterObservationLowByteRegister   = 0x90,
            ExternalTimer1CounterObservationHighByteRegister  = 0x91,
            ExternalTimer2CounterObservationLowByteRegister   = 0x94,
            ExternalTimer2CounterObservationHighByteRegister  = 0x95,
            ExternalTimer2CounterObservationHighByte2Register = 0x96,
            ExternalWDTCounterObservationLowByteRegister      = 0x98,
            ExternalWDTCounterObservationHighByteRegister     = 0x99,
        }
    }
}
