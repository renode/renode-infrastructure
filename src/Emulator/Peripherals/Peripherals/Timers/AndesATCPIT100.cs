//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    /// <remark>
    /// Only supports the single 32-bit timer mode for each channel currently, as this is all that the zephyr driver supports 
    /// </remark>
    public class AndesATCPIT100 : BasicDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IHasFrequency
    {
        public AndesATCPIT100(IMachine machine, ulong clockFrequency, ushort numberOfChannels = 4) : base(machine)
        {
            if(numberOfChannels == 0 || numberOfChannels > 4)
            {
                throw new ConstructionException($"Invalid number of channels ({numberOfChannels}) provided, numbers between 1-4 are supported");
            }
            IRQ = new GPIO();

            interruptEnableFlags = new IFlagRegisterField[numberOfChannels, 4];
            interruptStatusFlags = new IFlagRegisterField[numberOfChannels, 4];
            timerEnableFlags = new IFlagRegisterField[numberOfChannels, 4];
            timers = new LimitTimer[numberOfChannels, 4];
            reloadValues = new IValueRegisterField[numberOfChannels];
            channelModes = new ChannelMode[numberOfChannels];
            this.numberOfChannels = numberOfChannels;

            for(var i = 0; i < numberOfChannels; i++)
            {
                for(var j = 0; j < 4; j++)
                {
                    // Variables for lambda capture
                    var local_j = j;
                    var local_i = i;
                    timers[i, j] = new LimitTimer(machine.ClockSource, (long)clockFrequency, this, $"Channel {i} timer {j}", limit: 1, eventEnabled: true);
                    timers[i, j].LimitReached += delegate
                    {
                        this.DebugLog("{0} fired", timers[local_i, local_j].LocalName);
                        interruptStatusFlags[local_i, local_j].Value = interruptEnableFlags[local_i, local_j].Value;
                        UpdateReload(local_i, local_j);
                        UpdateInterrupts();
                    };
                }
            }

            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            IRQ.Unset();
            foreach(var timer in timers)
            {
                timer.Reset();
                timer.Enabled = false;
            }
            base.Reset();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public long Frequency
        {
            // All timers should always have the same frequency
            get => timers[0, 0].Frequency;
            set
            {
                foreach(var timer in timers)
                {
                    timer.Frequency = value;
                }
            }
        }

        private void UpdateInterrupts()
        {
            var value = false;
            for(var i = 0; i < numberOfChannels; i++)
            {
                for(var j = 0; j < 4; j++)
                {
                    value |= interruptStatusFlags[i, j].Value && interruptEnableFlags[i, j].Value;
                }
            }
            if(value != IRQ.IsSet)
            {
                this.DebugLog("Setting IRQ to {0}", value);
            }
            IRQ.Set(value);
        }

        private void DefineRegisters()
        {
            Register.IdRev.Define(this)
                .WithValueField(12, 20, mode: FieldMode.Read, valueProviderCallback: _ => 0x03031, name: "ID")
                .WithValueField(4, 8, mode: FieldMode.Read, valueProviderCallback: _ => 0, name: "RevMajor")
                .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ => 0, name: "RevMinor");
            Register.Cfg.Define(this)
                .WithReservedBits(3, 29)
                .WithValueField(0, 3, mode: FieldMode.Read, valueProviderCallback: _ => numberOfChannels, name: "NumCh");
            Register.IntEn.Define(this)
                .WithReservedBits(16, 16)
                .For((r, i) => r.For((_, j) => r.WithFlag(i * 4 + j, out interruptEnableFlags[i, j], name: $"Ch{i}Int{j}En"), 0, 4), 0, numberOfChannels)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Register.IntSt.Define(this)
                .WithReservedBits(16, 16)
                .For((r, i) => r.For((_, j) => r.WithFlag(i * 4 + j, out interruptStatusFlags[i, j], mode: FieldMode.Read | FieldMode.WriteOneToClear, name: $"Ch{i}Int{j}"), 0, 4), 0, numberOfChannels)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Register.ChEn.Define(this)
                .WithReservedBits(16, 16)
                .For((r, i) => r.For((_, j) => r.WithFlag(i * 4 + j, out timerEnableFlags[i, j], changeCallback: (__, ___) => UpdateEnabledTimers(i), name: $"Ch{i}TMR{j}En"), 0, 4), 0, numberOfChannels);
            Register.Ch0Ctrl.DefineMany(this, 4, (r, i) =>
            {
                r.WithReservedBits(5, 27)
                .WithTaggedFlag("PWMPark", 4)
                .WithTaggedFlag("ChClk", 3)
                .WithValueField(0, 3, name: "ChMode", changeCallback: (_, value) => UpdateMode(i, value));
            },
            stepInBytes: StepInBytesBetweenChannelRegisters);
            Register.Ch0Reload.DefineMany(this, 4, (r, i) =>
            {
                // This fields interpretation changes with the channel mode, but all bits are always R/W
                r.WithValueField(0, 32, out reloadValues[i], name: $"Ch{i}Reload");
            },
            stepInBytes: StepInBytesBetweenChannelRegisters);
            Register.Ch0Cntr.DefineMany(this, 4, (r, i) =>
            {
                // This fields interpretation changes with the channel mode, but all bits are always RO
                r.WithValueField(0, 32, mode: FieldMode.Read, name: $"Ch{i}Cntr", valueProviderCallback: _ => GetCounterRegister(i));
            },
            stepInBytes: StepInBytesBetweenChannelRegisters);
        }

        private void UpdateEnabledTimers(int channel)
        {
            switch(channelModes[channel])
            {
            case ChannelMode.Single32Bit:
                UpdateReload(channel, 0);
                timers[channel, 0].Enabled = timerEnableFlags[channel, 0].Value;
                break;
            default:
                // No other modes supported currently 
                break;
            }
        }

        private uint GetCounterRegister(int channel)
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
            switch(channelModes[channel])
            {
            case ChannelMode.Single32Bit:
                return (uint)timers[channel, 0].Value + 1;
            default:
                // No other modes supported currently 
                return 0;
            }
        }

        private void UpdateReload(int channel, int timer)
        {
            switch(channelModes[channel])
            {
            case ChannelMode.Single32Bit:
                // Only timer 0 should be active in 32-bit mode
                DebugHelper.Assert(timer == 0);
                timers[channel, 0].Limit = reloadValues[channel].Value + 1;
                timers[channel, 0].ResetValue();
                break;
            default:
                // No other modes supported currently 
                break;
            }
        }

        private void UpdateMode(int channel, ulong value)
        {
            if(!Enum.IsDefined(typeof(ChannelMode), (int)value))
            {
                this.WarningLog("Tried to set channel {0} to a reserved mode ({1}), ignoring write", channel, value);
                return;
            }
            switch((ChannelMode)value)
            {
            case ChannelMode.Single32Bit:
                // With only one mode supported no mode changeing is logic needed
                channelModes[channel] = (ChannelMode)value;
                break;
            default:
                // Only single 32-bit mode supported for now since that is all zephyr supports
                this.WarningLog("Tried to set channel {0} to an unsupported mode ({1}), ignoring write", channel, (ChannelMode)value);
                break;
            }
        }

        private readonly IFlagRegisterField[,] interruptEnableFlags, interruptStatusFlags, timerEnableFlags;
        private readonly IValueRegisterField[] reloadValues;
        private readonly ChannelMode[] channelModes;
        private readonly LimitTimer[,] timers;

        private readonly ushort numberOfChannels;

        private const ushort StepInBytesBetweenChannelRegisters = 0x10;

        private enum ChannelMode
        {
            Single32Bit = 1,
            Dual16Bit = 2,
            Quad8Bit = 3,
            PWM = 4,
            MixedPWM16BitTimer = 6,
            MixedPWM8BitTimers = 7
        }

        private enum Register
        {
            IdRev = 0x0,
            Cfg = 0x10,
            IntEn = 0x14,
            IntSt = 0x18,
            ChEn = 0x1C,
            Ch0Ctrl = 0x20,
            Ch0Reload = 0x24,
            Ch0Cntr = 0x28,
            Ch1Ctrl = 0x30,
            Ch1Reload = 0x34,
            Ch1Cntr = 0x38,
            Ch2Ctrl = 0x40,
            Ch2Reload = 0x44,
            Ch2Cntr = 0x48,
            Ch3Ctrl = 0x50,
            Ch3Reload = 0x54,
            Ch3Cntr = 0x58,
        }
    }
}