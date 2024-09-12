//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.WordToByte)]
    public class SAMD21_Timer : IBytePeripheral, IDoubleWordPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public SAMD21_Timer(IMachine machine, long baseFrequency)
        {
            RegistersCollection = new ByteRegisterCollection(this);

            mainTimer = new LimitTimer(machine.ClockSource, baseFrequency, this, "clock", eventEnabled: true, direction: Direction.Ascending);
            captureTimer0 = new LimitTimer(machine.ClockSource, baseFrequency, this, "capture0", eventEnabled: true, workMode: WorkMode.OneShot, direction: Direction.Ascending);
            captureTimer1 = new LimitTimer(machine.ClockSource, baseFrequency, this, "capture1", eventEnabled: true, workMode: WorkMode.OneShot, direction: Direction.Ascending);

            mainTimer.LimitReached += () =>
            {
                interruptOverflowPending.Value = true;

                UpdateInterrupts();
                StartChannels();
            };

            captureTimer0.LimitReached += () =>
            {
                interruptChannel0Pending.Value = true;
                UpdateInterrupts();
            };

            captureTimer1.LimitReached +=  () =>
            {
                interruptChannel1Pending.Value = true;
                UpdateInterrupts();
            };

            DefineRegisters();
            Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < (long)Registers.Counter0)
            {
                this.WriteDoubleWordUsingByte(offset, value);
                return;
            }

            long width = CounterWidth;
            if(offset == (long)Registers.Counter0)
            {
                // NOTE: Counter
                mainTimer.Value = value;
                return;
            }

            if(offset == (long)Registers.ChannelCompareCaptureValue0_0)
            {
                // NOTE: Channel Compare Capture 0 value
                compare0Value = value;
                // NOTE: If correct mode is selected, we should update TOP value of the main timer
                mainTimer.Limit = CounterTopValue;
                StartChannels();
                return;
            }

            if(offset == (long)Registers.ChannelCompareCaptureValue0_0 + width)
            {
                // NOTE: Channel Compare Capture 1 value
                compare1Value = value;
                StartChannels();
                return;
            }

            this.Log(LogLevel.Warning, "Unhandled write on offset 0x{0:X}", offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset < (long)Registers.Counter0)
            {
                return this.ReadDoubleWordUsingByte(offset);
            }

            long width = CounterWidth;
            if(offset == (long)Registers.Counter0)
            {
                // NOTE: Counter
                return (uint)mainTimer.Value;
            }

            if(offset == (long)Registers.ChannelCompareCaptureValue0_0)
            {
                // NOTE: Channel Compare Capture 0 value
                return (uint)compare0Value;
            }

            if(offset == (long)Registers.ChannelCompareCaptureValue0_0 + width)
            {
                // NOTE: Channel Compare Capture 1 value
                return (uint)compare1Value;
            }

            this.Log(LogLevel.Warning, "Unhandled read on offset 0x{0:X}", offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void Reset()
        {
            RegistersCollection.Reset();

            interruptOverflowEnabled = false;
            interruptChannel0Enabled = false;
            interruptChannel1Enabled = false;

            UpdateInterrupts();

            Enabled = false;
            Divider = 1;

            compare0Value = 0;
            compare1Value = 0;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x20;

        public bool Enabled
        {
            get => mainTimer.Enabled;
            set
            {
                mainTimer.Enabled = value;
                captureTimer0.Enabled = value;
                captureTimer1.Enabled = value;
            }
        }

        public int Divider
        {
            get => mainTimer.Divider;
            set
            {
                mainTimer.Divider = value;
                captureTimer0.Divider = value;
                captureTimer1.Divider = value;
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = false;
            interrupt |= interruptOverflowEnabled && interruptOverflowPending.Value;
            interrupt |= interruptChannel0Enabled && interruptChannel0Pending.Value;
            interrupt |= interruptChannel1Enabled && interruptChannel1Pending.Value;

            this.Log(LogLevel.Debug, "Changed IRQ to {0}", IRQ.IsSet);
            IRQ.Set(interrupt);
        }

        private void ReconfigureCounter()
        {
            mainTimer.Limit = CounterTopValue;
            mainTimer.Value = mainTimer.Direction == Direction.Ascending ? 0 : CounterTopValue;
        }

        private void StartChannels()
        {
            captureTimer0.Value = mainTimer.Value;
            captureTimer1.Value = mainTimer.Value;

            if(mainTimer.Direction == Direction.Ascending)
            {
                captureTimer0.Limit = compare0Value;
                captureTimer1.Limit = compare1Value;
            }
            else
            {
                var topValue = CounterTopValue;

                captureTimer0.Limit = topValue - compare0Value;
                captureTimer1.Limit = topValue - compare1Value;
            }

            captureTimer0.Enabled = captureTimer0.Limit > 0 && captureTimer0.Value <= captureTimer0.Limit;
            captureTimer1.Enabled = captureTimer1.Limit > 0 && captureTimer1.Value <= captureTimer1.Limit;
        }

        private void DefineRegisters()
        {
            Registers.ControlA0.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "SWRST",
                    writeCallback: (_, value) => { if(value) Reset(); })
                .WithFlag(1, name: "ENABLE",
                    valueProviderCallback: _ => Enabled,
                    writeCallback: (_, value) => Enabled = value)
                .WithEnumField<ByteRegister, CounterMode>(2, 2, out counterMode, name: "MODE",
                    changeCallback: (_, __) =>
                    {
                        if(counterMode.Value == CounterMode.Reserved)
                        {
                            this.Log(LogLevel.Warning, "MODE field has been set to {0} (Reserved); this will be treated as default (0)", (uint)counterMode.Value);
                        }

                        ReconfigureCounter();
                    })
                .WithReservedBits(4, 1)
                .WithEnumField(5, 2, out waveformGenerationOperation, name: "WAVEGEN",
                    changeCallback: (_, __) => ReconfigureCounter())
                .WithReservedBits(7, 1)
            ;

            Registers.ControlA1.Define(this)
                .WithEnumField<ByteRegister, Prescaler>(0, 3, name: "PRESCALER",
                    writeCallback: (_, value) => Divider = GetPrescalerValue(value))
                .WithTaggedFlag("RUNSTDBY", 3)
                .WithTag("PRESCSYNC", 4, 2)
                .WithReservedBits(6, 2)
            ;

            Registers.ControlBSet.Define(this)
                .WithFlag(0, name: "DIR",
                    valueProviderCallback: _ => mainTimer.Direction == Direction.Descending,
                    writeCallback: (_, value) => { if(value) mainTimer.Direction = Direction.Descending; })
                .WithFlag(2, name: "ONESHOT",
                    valueProviderCallback: _ => mainTimer.Mode == WorkMode.OneShot,
                    writeCallback: (_, value) => { if(value) mainTimer.Mode = WorkMode.OneShot; })
                .WithReservedBits(3, 3)
                .WithEnumField<ByteRegister, Command>(6, 2, name: "CMD",
                    writeCallback: (_, value) =>
                    {
                        switch(value)
                        {
                            case Command.Retrigger:
                                Enabled = true;
                                ReconfigureCounter();
                                StartChannels();
                                break;
                            case Command.Stop:
                                Enabled = false;
                                break;
                            default:
                                break;
                        }
                    })
            ;

            Registers.ControlBClear.Define(this)
                .WithFlag(0, name: "DIR",
                    valueProviderCallback: _ => mainTimer.Direction == Direction.Descending,
                    writeCallback: (_, value) => { if(value) mainTimer.Direction = Direction.Ascending; })
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "ONESHOT",
                    valueProviderCallback: _ => mainTimer.Mode == WorkMode.OneShot,
                    writeCallback: (_, value) => { if(value) mainTimer.Mode = WorkMode.Periodic; })
                .WithReservedBits(3, 3)
                .WithTag("CMD", 6, 2)
            ;

            Registers.Status.Define(this)
                .WithReservedBits(0, 3)
                .WithFlag(3, FieldMode.Read, name: "STOP",
                    valueProviderCallback: _ => !Enabled)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("SYNCBUSY", 7)
            ;

            Registers.InterruptEnableClear.Define(this)
                .WithFlag(0, name: "OVF",
                    valueProviderCallback: _ => interruptOverflowEnabled,
                    writeCallback: (_, value) => { if(value) interruptOverflowEnabled = false; })
                .WithTaggedFlag("ERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SYNCRDY", 3)
                .WithFlag(4, name: "MC0",
                    valueProviderCallback: _ => interruptChannel0Enabled,
                    writeCallback: (_, value) => { if(value) interruptChannel0Enabled = false; })
                .WithFlag(5, name: "MC1",
                    valueProviderCallback: _ => interruptChannel1Enabled,
                    writeCallback: (_, value) => { if(value) interruptChannel1Enabled = false; })
                .WithReservedBits(6, 2)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnableSet.Define(this)
                .WithFlag(0, name: "OVF",
                    valueProviderCallback: _ => interruptOverflowEnabled,
                    writeCallback: (_, value) => { if(value) interruptOverflowEnabled = true; })
                .WithTaggedFlag("ERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SYNCRDY", 3)
                .WithFlag(4, name: "MC0",
                    valueProviderCallback: _ => interruptChannel0Enabled,
                    writeCallback: (_, value) => { if(value) interruptChannel0Enabled = true; })
                .WithFlag(5, name: "MC1",
                    valueProviderCallback: _ => interruptChannel1Enabled,
                    writeCallback: (_, value) => { if(value) interruptChannel1Enabled = true; })
                .WithReservedBits(6, 2)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptFlags.Define(this)
                .WithFlag(0, out interruptOverflowPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVF")
                .WithTaggedFlag("ERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SYNCRDY", 3)
                .WithFlag(4, out interruptChannel0Pending, FieldMode.Read | FieldMode.WriteOneToClear, name: "MC0")
                .WithFlag(5, out interruptChannel1Pending, FieldMode.Read | FieldMode.WriteOneToClear, name: "MC1")
                .WithReservedBits(6, 2)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
        }

        private int GetPrescalerValue(Prescaler prescaler)
        {
            switch(prescaler)
            {
                case Prescaler.Div1:
                    return 1;
                case Prescaler.Div2:
                    return 2;
                case Prescaler.Div4:
                    return 4;
                case Prescaler.Div8:
                    return 8;
                case Prescaler.Div16:
                    return 16;
                case Prescaler.Div64:
                    return 64;
                case Prescaler.Div256:
                    return 256;
                case Prescaler.Div1024:
                    return 1024;
                default:
                    throw new Exception("unreachable");
            }
        }

        private uint CounterWidth
        {
            get
            {
                switch(counterMode.Value)
                {
                    case CounterMode.Count8:
                        return 1;
                    case CounterMode.Count32:
                        return 4;
                    case CounterMode.Count16:
                    default:
                        return 2;
                }
            }
        }

        private uint CounterTopValue
        {
            get
            {
                if(compare0Value > 0 &&
                    (waveformGenerationOperation.Value == WaveformGenerationOperation.MatchFrequency ||
                     waveformGenerationOperation.Value == WaveformGenerationOperation.MatchPWM))
                {
                    return (uint)compare0Value;
                }

                switch(counterMode.Value)
                {
                    case CounterMode.Count8:
                        return 0xFF;
                    case CounterMode.Count32:
                        return 0xFFFFFFFF;
                    case CounterMode.Count16:
                    default:
                        return 0xFFFF;
                }
            }
        }

        private readonly LimitTimer mainTimer;
        private readonly LimitTimer captureTimer0;
        private readonly LimitTimer captureTimer1;

        private bool interruptOverflowEnabled;
        private bool interruptChannel0Enabled;
        private bool interruptChannel1Enabled;

        private ulong compare0Value;
        private ulong compare1Value;

        private IEnumRegisterField<CounterMode> counterMode;
        private IEnumRegisterField<WaveformGenerationOperation> waveformGenerationOperation;

        private IFlagRegisterField interruptOverflowPending;
        private IFlagRegisterField interruptChannel0Pending;
        private IFlagRegisterField interruptChannel1Pending;

        private enum Command
        {
            Nothing,
            Retrigger,
            Stop,
            Reserved,
        }

        private enum WaveformGenerationOperation
        {
            NormalFrequency,
            MatchFrequency,
            NormalPWM,
            MatchPWM,
        }

        private enum CounterMode
        {
            Count16,
            Count8,
            Count32,
            Reserved,
        }

        private enum Prescaler
        {
            Div1,
            Div2,
            Div4,
            Div8,
            Div16,
            Div64,
            Div256,
            Div1024
        }

        private enum Registers
        {
            ControlA0 = 0x00,
            ControlA1 = 0x01,
            ReadRequest0 = 0x02,
            ReadRequest1 = 0x03,
            ControlBClear = 0x04,
            ControlBSet = 0x05,
            ControlC = 0x06,

            Reserved0 = 0x07,

            DebugControl = 0x08,

            Reserved1 = 0x09,

            EventControl = 0x0A,
            InterruptEnableClear = 0x0C,
            InterruptEnableSet = 0x0D,
            InterruptFlags = 0x0E,
            Status = 0x0F,

            // NOTE: This is 8-bit, 16-bit or 32-bit register
            Counter0 = 0x10,
            Counter1 = 0x11,
            Counter2 = 0x12,
            Counter3 = 0x13,

            // NOTE: This is 8-bit, 16-bit or 32-bit register
            ChannelCompareCaptureValue0_0 = 0x18,
            ChannelCompareCaptureValue0_1 = 0x19,
            ChannelCompareCaptureValue0_2 = 0x1A,
            ChannelCompareCaptureValue0_3 = 0x1B,

            // NOTE: This is 8-bit, 16-bit or 32-bit register
            ChannelCompareCaptureValue1_0 = 0x1C,
            ChannelCompareCaptureValue1_1 = 0x1D,
            ChannelCompareCaptureValue1_2 = 0x1E,
            ChannelCompareCaptureValue1_3 = 0x1F,
        }
    }
}
