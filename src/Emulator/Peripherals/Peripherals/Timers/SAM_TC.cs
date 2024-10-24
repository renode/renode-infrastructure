//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class SAM_TC : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize
    {
        public SAM_TC(IMachine machine, long masterClockFrequency = 20000000) : base(machine)
        {
            var connections = new Dictionary<int, IGPIO>();
            channels = new Channel[NumberOfChannels];
            for(int i = 0; i < NumberOfChannels; i++)
            {
                channels[i] = new Channel(machine.ClockSource, masterClockFrequency, this, i);
                connections[i] = channels[i].IRQ;
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(connections);

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            for(int i = 0; i < NumberOfChannels; i++)
            {
                channels[i].Reset();
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            // channel mode changes with write to channel mode register so we need to set condition before write
            var channel = offset / ChannelSize;
            if(channel < NumberOfChannels && (offset % ChannelSize) == (long)Registers.ChannelMode0)
            {
                channels[channel].WaveformMode = BitHelper.IsBitSet(value, 15);
            }

            base.WriteDoubleWord(offset, value);
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.ChannelControl0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) channels[id].Enable(); }, name: "CLKEN")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if(value) channels[id].Disable(); }, name: "CLKDIS")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if(value) channels[id].SoftwareTrigger(); }, name: "SWTRG")
                .WithReservedBits(3, 29)
            );

            Registers.ChannelMode0.DefineManyConditional(this, NumberOfChannels, id => !channels[id].WaveformMode, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithEnumField<DoubleWordRegister, ClockSelection>(0, 3, writeCallback: (_, value) => channels[id].ClockSelected = value, valueProviderCallback: _ => channels[id].ClockSelected, name: "TCCLKS")
                .WithTaggedFlag("CLKI", 3)
                .WithTag("BURST", 4, 2)
                .WithTaggedFlag("LDBSTOP", 6)
                .WithTaggedFlag("LDBDIS", 7)
                .WithTag("ETRGEDG", 8, 2)
                .WithTaggedFlag("ABETRG", 10)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("CPCTRG", 14)
                .WithFlag(15, valueProviderCallback: _ => channels[id].WaveformMode, name: "WAVE")
                .WithTag("LDRA", 16, 2)
                .WithTag("LDRB", 18, 2)
                .WithReservedBits(20, 12)
            );

            Registers.ChannelMode0.DefineManyConditional(this, NumberOfChannels, id => channels[id].WaveformMode, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithEnumField<DoubleWordRegister, ClockSelection>(0, 3, writeCallback: (_, value) => channels[id].ClockSelected = value, valueProviderCallback: _ => channels[id].ClockSelected, name: "TCCLKS")
                .WithTaggedFlag("CLKI", 3)
                .WithTag("BURST", 4, 2)
                .WithFlag(6, writeCallback: (_, value) => channels[id].StopOnC = value, valueProviderCallback: _ => channels[id].StopOnC, name: "CPCSTOP")
                .WithTaggedFlag("CPCDIS", 7)
                .WithTag("EEVTEDG", 8, 2)
                .WithTag("EEVT", 10, 2)
                .WithTaggedFlag("ENETRG", 12)
                .WithEnumField<DoubleWordRegister, WaveSelection>(13, 2, writeCallback: (_, value) => channels[id].WaveformSelected = value, valueProviderCallback: _ => channels[id].WaveformSelected, name: "WAVSEL")
                .WithFlag(15, valueProviderCallback: _ => channels[id].WaveformMode, name: "WAVE")
                .WithTag("ACPA", 16, 2)
                .WithTag("ACPC", 18, 2)
                .WithTag("AEEVT", 20, 2)
                .WithTag("ASWTRG", 22, 2)
                .WithTag("BCPB", 24, 2)
                .WithTag("BCPC", 26, 2)
                .WithTag("BEEVT", 28, 2)
                .WithTag("BSWTRG", 30, 2)
            );

            Registers.StepperMotorMode0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithTaggedFlag("GCEN", 0)
                .WithTaggedFlag("DOWN", 1)
                .WithReservedBits(2, 30)
            );

            Registers.CounterValue0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                    {
                        cpu.SyncTime();
                    }
                    return channels[id].Value;
                }, name: "CV")
            );

            Registers.A0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithValueField(0, 32, writeCallback: (_, value) => channels[id].A = value, valueProviderCallback: _ => channels[id].A, name: "RA")
            );

            Registers.B0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithValueField(0, 32, writeCallback: (_, value) => channels[id].B = value, valueProviderCallback: _ => channels[id].B, name: "RB")
            );

            Registers.C0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithValueField(0, 32, writeCallback: (_, value) => channels[id].C = value, valueProviderCallback: _ => channels[id].C, name: "RC")
            );

            Registers.Status0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithFlag(0, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].Overflow, name: "COVFS")
                .WithTaggedFlag("LOVRS", 1)
                .WithFlag(2, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].CompareAInterrupt, name: "CPAS")
                .WithFlag(3, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].CompareBInterrupt, name: "CPBS")
                .WithFlag(4, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].CompareCInterrupt, name: "CPCS")
                .WithFlag(5, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].LoadingAInterrupt, name: "LDRAS")
                .WithFlag(6, FieldMode.ReadToClear, valueProviderCallback: _ => channels[id].LoadingBInterrupt, name: "LDRBS")
                .WithTaggedFlag("ETRGS", 7)
                .WithReservedBits(8, 8)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => channels[id].Enabled, name: "CLKSTA")
                .WithTaggedFlag("MTIOA", 17)
                .WithTaggedFlag("MTIOB", 18)
                .WithReservedBits(19, 13)
                .WithReadCallback((_, __) => channels[id].UpdateInterrupts())
            );

            Registers.InterruptEnable0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].OverflowInterruptEnable = true; }, name: "COVFS")
                .WithTaggedFlag("LOVRS", 1)
                .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].CompareAInterruptEnable = true; }, name: "CPAS")
                .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].CompareBInterruptEnable = true; }, name: "CPBS")
                .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].CompareCInterruptEnable = true; }, name: "CPCS")
                .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].LoadingAInterruptEnable = true; }, name: "LDRAS")
                .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if(value) channels[id].LoadingBInterruptEnable = true; }, name: "LDRBS")
                .WithTaggedFlag("ETRGS", 7)
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => channels[id].UpdateInterrupts())
            );

            Registers.InterruptDisable0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].OverflowInterruptEnable = false; }, name: "COVFS")
                .WithTaggedFlag("LOVRS", 1)
                .WithFlag(2, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].CompareAInterruptEnable = false; }, name: "CPAS")
                .WithFlag(3, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].CompareBInterruptEnable = false; }, name: "CPBS")
                .WithFlag(4, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].CompareCInterruptEnable = false; }, name: "CPCS")
                .WithFlag(5, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].LoadingAInterruptEnable = false; }, name: "LDRAS")
                .WithFlag(6, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) channels[id].LoadingBInterruptEnable = false; }, name: "LDRBS")
                .WithTaggedFlag("ETRGS", 7)
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => channels[id].UpdateInterrupts())
            );

            Registers.InterruptMask0.DefineMany(this, NumberOfChannels, stepInBytes: ChannelSize, setup: (reg, id) => reg
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => channels[id].OverflowInterruptEnable, name: "COVFS")
                .WithTaggedFlag("LOVRS", 1)
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => channels[id].CompareAInterruptEnable, name: "CPAS")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => channels[id].CompareBInterruptEnable, name: "CPBS")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => channels[id].CompareCInterruptEnable, name: "CPCS")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => channels[id].LoadingAInterruptEnable, name: "LDRAS")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => channels[id].LoadingBInterruptEnable, name: "LDRBS")
                .WithTaggedFlag("ETRGS", 7)
                .WithReservedBits(8, 24)
            );

            Registers.BlockControl.Define(this)
                .WithFlag(0, FieldMode.Set, writeCallback: (_, value) =>
                {
                    if(!value)
                    {
                        return;
                    }
                    for(int i = 0; i < NumberOfChannels; i++)
                    {
                        channels[i].SoftwareTrigger();
                    }
                }, name: "SYNC")
                .WithReservedBits(1, 31)
            ;

            Registers.BlockMode.Define(this)
                .WithTag("TC0XC0S", 0, 2)
                .WithTag("TC1XC1S", 2, 2)
                .WithTag("TC2XC2S", 4, 2)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("QDEN", 8)
                .WithTaggedFlag("POSEN", 9)
                .WithTaggedFlag("SPEEDEN", 10)
                .WithTaggedFlag("QDTRANS", 11)
                .WithTaggedFlag("EDGPHA", 12)
                .WithTaggedFlag("INVA", 13)
                .WithTaggedFlag("INVB", 14)
                .WithTaggedFlag("INVIDX", 15)
                .WithTaggedFlag("SWAP", 16)
                .WithTaggedFlag("IDXPHB", 17)
                .WithReservedBits(18, 2)
                .WithTag("MAXFILT", 20, 6)
                .WithReservedBits(26, 6)
            ;

            Registers.QdecInterruptEnable.Define(this)
                .WithTaggedFlag("IDX", 0)
                .WithTaggedFlag("DIRCHG", 1)
                .WithTaggedFlag("QERR", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.QdecInterruptDisable.Define(this)
                .WithTaggedFlag("IDX", 0)
                .WithTaggedFlag("DIRCHG", 1)
                .WithTaggedFlag("QERR", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.QdecInterruptMask.Define(this)
                .WithTaggedFlag("IDX", 0)
                .WithTaggedFlag("DIRCHG", 1)
                .WithTaggedFlag("QERR", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.QdecInterruptStatus.Define(this)
                .WithTaggedFlag("IDX", 0)
                .WithTaggedFlag("DIRCHG", 1)
                .WithTaggedFlag("QERR", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("DIR", 8)
                .WithReservedBits(9, 23)
            ;

            Registers.FaultMode.Define(this)
                .WithTaggedFlag("ENCF0", 0)
                .WithTaggedFlag("ENCF1", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.WriteProtectionMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;
        }

        private readonly Channel[] channels;
        private const int NumberOfChannels = 3;
        private const int ChannelSize = 0x40;

        public enum Registers
        {
            ChannelControl0 = 0x00, // TC_CCR WO 0x00 + channel * 0x40 + 0x00
            ChannelMode0 = 0x04, // TC_CMR RW 0x00 + channel * 0x40 + 0x04
            StepperMotorMode0 = 0x08, // TC_SMMR RW 0x00 + channel * 0x40 + 0x08
            // Reserved0 = 0x0C,
            CounterValue0 = 0x10, // TC_CV RO 0x00 + channel * 0x40 + 0x10
            A0 = 0x14, // TC_RA RW 0x00 + channel * 0x40 + 0x14
            B0 = 0x18, // TC_RB RW 0x00 + channel * 0x40 + 0x18
            C0 = 0x1C, // TC_RC RW 0x00 + channel * 0x40 + 0x1C
            Status0 = 0x20, // TC_SR RO 0x00 + channel * 0x40 + 0x20
            InterruptEnable0 = 0x24, // TC_IER WO 0x00 + channel * 0x40 + 0x24
            InterruptDisable0 = 0x28, // TC_IDR WO 0x00 + channel * 0x40 + 0x28
            InterruptMask0 = 0x2C, // TC_IMR RO 0x00 + channel * 0x40 + 0x2C

            ChannelControl1 = 0x40,
            ChannelMode1 = 0x44,
            StepperMotorMode1 = 0x48,
            // Reserved1 = 0x4C,
            CounterValue1 = 0x50,
            A1 = 0x54,
            B1 = 0x58,
            C1 = 0x5C,
            Status1 = 0x60,
            InterruptEnable1 = 0x64,
            InterruptDisable1 = 0x68,
            InterruptMask1 = 0x6C,

            ChannelControl2 = 0x80,
            ChannelMode2 = 0x84,
            StepperMotorMode2 = 0x88,
            // Reserved2 = 0x8C,
            CounterValue2 = 0x90,
            A2 = 0x94,
            B2 = 0x98,
            C2 = 0x9C,
            Status2 = 0xA0,
            InterruptEnable2 = 0xA4,
            InterruptDisable2 = 0xA8,
            InterruptMask2 = 0xAC,

            BlockControl = 0xC0, // TC_BCR WO
            BlockMode = 0xC4, // TC_BMR RW
            QdecInterruptEnable = 0xC8, // TC_QIER WO
            QdecInterruptDisable = 0xCC, // TC_QIDR WO
            QdecInterruptMask = 0xD0, // TC_QIMR RO
            QdecInterruptStatus = 0xD4, // TC_QISR RO
            FaultMode = 0xD8, // TC_FMR RW
            WriteProtectionMode = 0xE4, // TC_WPMR RW
        }

        private enum ClockSelection
        {
            MCK_2 = 0,
            MCK_8 = 1,
            MCK_32 = 2,
            MCK_128 = 3,
            SLCK = 4,
            XC0 = 5,
            XC1 = 6,
            XC2 = 7,
        }

        private enum WaveSelection
        {
            Up = 0b00,
            UpDown = 0b01,
            UpRC = 0b10,
            UpDownRC = 0b11,
        }

        private class Channel
        {
            public Channel(IClockSource clockSource, long masterClockFrequency, IPeripheral owner, int channel)
            {
                this.masterClockFrequency = masterClockFrequency;
                this.channel = channel;
                parent = owner;
                IRQ = new GPIO();
                timer = new LimitTimer(clockSource, masterClockFrequency, owner, $"channel-{channel}", MaxValue, Direction.Ascending, eventEnabled: true, divider: 2);
                cTimer = new LimitTimer(clockSource, masterClockFrequency, owner, $"channel-{channel} C capture", MaxValue, Direction.Ascending, workMode: WorkMode.OneShot, eventEnabled: true, divider: 2);
                timer.LimitReached += LimitReached;
                cTimer.LimitReached += delegate
                {
                    if(StopOnC)
                    {
                        timer.Enabled = false;
                        enabled = false;
                    }
                    compareCInterrupt = true;
                    parent.NoisyLog("Channel #{0} compare C", channel);
                    UpdateInterrupts();
                };
            }

            public void Reset()
            {
                valueA = 0x0;
                valueB = 0x0;
                valueC = 0x0;
                enabled = false;
                overflow = false;
                overflowInterruptEnable = false;
                compareAInterrupt = false;
                compareAInterruptEnable = false;
                compareBInterrupt = false;
                compareBInterruptEnable = false;
                compareCInterrupt = false;
                compareCInterruptEnable = false;
                loadingAInterrupt = false;
                loadingAInterruptEnable = false;
                loadingBInterrupt = false;
                loadingBInterruptEnable = false;
                waveformMode = false;
                waveformSelected = WaveSelection.Up;
                clockSelected = ClockSelection.MCK_2;
                timer.Reset();
                cTimer.Reset();
                UpdateInterrupts();
            }

            public void UpdateInterrupts()
            {
                var state = false;
                state |= overflow && overflowInterruptEnable;
                state |= compareAInterrupt && compareAInterruptEnable;
                state |= compareBInterrupt && compareBInterruptEnable;
                state |= compareCInterrupt && compareCInterruptEnable;
                state |= loadingAInterrupt && loadingAInterruptEnable;
                state |= loadingBInterrupt && loadingBInterruptEnable;
                if(state)
                {
                    parent.DebugLog("Channel #{0} IRQ blinked", channel);
                    // NOTE: We use Blink here due to specific software behaviour:
                    // The TC interrupt was set while the interrupts were masked,
                    // after the interrupts were enabled it was expected that
                    // the TC interrupts were handled as they came without clearing
                    // the pending status.
                    IRQ.Blink();
                }
            }

            public void Enable(bool start = false, bool debugLog = true)
            {
                enabled = true;
                UpdateTimer(start);
                if(debugLog)
                {
                    parent.DebugLog("Channel #{0} enabled", channel);
                }
            }

            public void Disable()
            {
                enabled = false;
                UpdateTimer();
                parent.DebugLog("Channel #{0} disabled", channel);
            }

            public void SoftwareTrigger()
            {
                parent.DebugLog("Channel #{0} software trigger", channel);
                timer.Value = 0;
                Enable(true, debugLog: false);
            }

            public IGPIO IRQ { get; }

            public ulong Value => timer.Value;

            public bool Enabled => timer.Enabled;

            public bool Overflow => Misc.ReturnThenClear(ref overflow);

            public bool OverflowInterruptEnable
            {
                get => overflowInterruptEnable;
                set => overflowInterruptEnable = value;
            }

            public bool CompareAInterrupt => Misc.ReturnThenClear(ref compareAInterrupt);

            public bool CompareAInterruptEnable
            {
                get => compareAInterruptEnable;
                set => compareAInterruptEnable = value;
            }

            public bool CompareBInterrupt => Misc.ReturnThenClear(ref compareBInterrupt);

            public bool CompareBInterruptEnable
            {
                get => compareBInterruptEnable;
                set => compareBInterruptEnable = value;
            }

            public bool CompareCInterrupt => Misc.ReturnThenClear(ref compareCInterrupt);

            public bool CompareCInterruptEnable
            {
                get => compareCInterruptEnable;
                set => compareCInterruptEnable = value;
            }

            public bool LoadingAInterrupt => Misc.ReturnThenClear(ref loadingAInterrupt);

            public bool LoadingAInterruptEnable
            {
                get => loadingAInterruptEnable;
                set => loadingAInterruptEnable = value;
            }

            public bool LoadingBInterrupt => Misc.ReturnThenClear(ref loadingBInterrupt);

            public bool LoadingBInterruptEnable
            {
                get => loadingBInterruptEnable;
                set => loadingBInterruptEnable = value;
            }

            public ulong A
            {
                get => valueA;
                set => valueA = value;
            }

            public ulong B
            {
                get => valueB;
                set => valueB = value;
            }

            public ulong C
            {
                get => valueC;
                set
                {
                    valueC = value;
                    UpdateTimer();
                }
            }

            public bool WaveformMode
            {
                get => waveformMode;
                set
                {
                    waveformMode = value;
                    UpdateTimer();
                }
            }

            public WaveSelection WaveformSelected
            {
                get => waveformSelected;
                set
                {
                    waveformSelected = value;
                    UpdateTimer();
                }
            }

            public bool StopOnC { get; set; }

            public ClockSelection ClockSelected
            {
                get => clockSelected;
                set
                {
                    if(clockSelected == value)
                    {
                        return;
                    }

                    clockSelected = value;
                    UpdateFrequency();
                }
            }

            private void UpdateFrequency()
            {
                switch(clockSelected)
                {
                case ClockSelection.MCK_2:
                    timer.Frequency = masterClockFrequency;
                    timer.Divider = 2;
                    break;
                case ClockSelection.MCK_8:
                    timer.Frequency = masterClockFrequency;
                    timer.Divider = 8;
                    break;
                case ClockSelection.MCK_32:
                    timer.Frequency = masterClockFrequency;
                    timer.Divider = 32;
                    break;
                case ClockSelection.MCK_128:
                    timer.Frequency = masterClockFrequency;
                    timer.Divider = 128;
                    break;
                case ClockSelection.SLCK:
                case ClockSelection.XC0:
                case ClockSelection.XC1:
                case ClockSelection.XC2:
                    parent.ErrorLog("Unimplemented");
                    break;
                default:
                    throw new Exception("unreachable");
                }
                cTimer.Frequency = timer.Frequency;
                cTimer.Divider = timer.Divider;
            }

            private void UpdateTimer(bool start = false)
            {
                if(!enabled)
                {
                    return;
                }

                if(!waveformMode)
                {
                    parent.ErrorLog("Unimplemented");
                }
                else
                {
                    switch(waveformSelected)
                    {
                    case WaveSelection.Up:
                        timer.Direction = Direction.Ascending;
                        timer.Limit = MaxValue;
                        break;
                    case WaveSelection.UpDown:
                        timer.Limit = MaxValue;
                        break;
                    case WaveSelection.UpRC:
                        timer.Direction = Direction.Ascending;
                        timer.Limit = valueC;
                        break;
                    case WaveSelection.UpDownRC:
                        timer.Limit = valueC;
                        break;
                    default:
                        throw new Exception("unreachable");
                    }
                }

                timer.Enabled |= start;
                UpdateCTimer();
            }

            private void UpdateCTimer()
            {
                if(!enabled || !waveformMode)
                {
                    cTimer.Enabled = false;
                    return;
                }
                switch(waveformSelected)
                {
                case WaveSelection.Up:
                case WaveSelection.UpDown:
                    var direction = timer.Direction;
                    var value = timer.Value;
                    var limit = timer.Limit;

                    if(direction == Direction.Ascending ? value > valueC : value < valueC)
                    {
                        cTimer.Enabled = false;
                        return;
                    }

                    if(direction == Direction.Ascending)
                    {
                        cTimer.Value = value;
                        cTimer.Limit = valueC;
                    }
                    else
                    {
                        cTimer.Value = limit - value;
                        cTimer.Limit = limit - valueC;
                    }
                    cTimer.Enabled = timer.Enabled;
                    break;
                default:
                    cTimer.Enabled = false;
                    break;
                }
            }

            private void ChangeDirection()
            {
                timer.Direction = timer.Direction == Direction.Ascending ? Direction.Descending : Direction.Ascending;
            }

            private void LimitReached()
            {
                var cReached = timer.Limit == valueC && timer.Direction == Direction.Ascending;
                if(cReached && StopOnC)
                {
                    timer.Enabled = false;
                    enabled = false;
                }
                if(!waveformMode)
                {
                    parent.ErrorLog("Unimplemented");
                }
                else
                {
                    switch(waveformSelected)
                    {
                    case WaveSelection.Up:
                        overflow = true;
                        break;
                    case WaveSelection.UpRC:
                        parent.NoisyLog("Channel #{0} compare C", channel);
                        compareCInterrupt = true;
                        break;
                    case WaveSelection.UpDown:
                        if(timer.Value == MaxValue)
                        {
                            parent.NoisyLog("Channel #{0} overflow", channel);
                            overflow = true;
                        }
                        ChangeDirection();
                        break;
                    case WaveSelection.UpDownRC:
                        if(cReached)
                        {
                            parent.NoisyLog("Channel #{0} compare C", channel);
                            compareCInterrupt = true;
                        }
                        ChangeDirection();
                        break;
                    default:
                        throw new Exception("unreachable");
                    }
                }
                UpdateCTimer();
                UpdateInterrupts();
            }

            private ulong valueA;
            private ulong valueB;
            private ulong valueC;
            private bool enabled;
            private bool overflow;
            private bool overflowInterruptEnable;
            private bool compareAInterrupt;
            private bool compareAInterruptEnable;
            private bool compareBInterrupt;
            private bool compareBInterruptEnable;
            private bool compareCInterrupt;
            private bool compareCInterruptEnable;
            private bool loadingAInterrupt;
            private bool loadingAInterruptEnable;
            private bool loadingBInterrupt;
            private bool loadingBInterruptEnable;
            private bool waveformMode;
            private WaveSelection waveformSelected;

            private ClockSelection clockSelected;
            private readonly long masterClockFrequency;
            private readonly int channel;
            private readonly IPeripheral parent;
            private readonly LimitTimer timer;
            private readonly LimitTimer cTimer;

            private const ulong MaxValue = 0xFFFF;
        }
    }
}
