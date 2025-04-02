//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SAM4S_DACC : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM4S_DACC(IMachine machine, decimal referenceVoltage = 3.3m) : base(machine)
        {
            ReferenceVoltage = referenceVoltage;
            DefineRegisters();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(writeProtectionEnabled && Array.IndexOf(writeProtectedOffsets, offset) != -1)
            {
                writeViolationStatus.Value = true;
                writeViolationSource.Value = (ulong)offset;
                this.Log(LogLevel.Warning, "Tried to write {0} while write protection is enabled", Enum.GetName(typeof(Registers), offset));
                return;
            }

            base.WriteDoubleWord(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; } = new GPIO();

        public decimal ReferenceVoltage { get; set; }

        public decimal MinimumVoltage => ReferenceVoltage / 6m;

        public decimal MaximumVoltage => 5m * ReferenceVoltage / 6m;

        public decimal VoltageRange => 4m * ReferenceVoltage / 6m;

        public decimal DAC0 => channelEnabled[0].Value ? ConvertVoltage(channelValue[0]) : 0m;

        public decimal DAC1 => channelEnabled[1].Value ? ConvertVoltage(channelValue[1]) : 0m;

        [field: Transient]
        public event Action<int, decimal, ulong> OutputChanged;

        private void UpdateInterrupts()
        {
            var interrupt = false;

            interrupt |= transmitReadyInterruptEnabled.Value;
            interrupt |= endOfConversionInterruptPending.Value && endOfConversionInterruptEnabled.Value;

            this.Log(LogLevel.Debug, "IRQ set to {0}", interrupt);
            IRQ.Set(interrupt);
        }

        private decimal ConvertVoltage(ulong value)
        {
            return MinimumVoltage + ((decimal)value / (decimal)MaximumChannelValue) * VoltageRange;
        }

        private void PerformConversion(ulong data)
        {
            // NOTE: Split and truncate to 16 bits
            var requests = wordMode.Value
                ? new ulong[] { data & 0xFFFF, (data >> 16) & 0xFFFF }
                : new ulong[] { data & 0xFFFF };

            foreach(var request in requests)
            {
                var value = request & MaximumChannelValue;
                var channel = tagSelectionMode.Value ? (int)request >> 12 : (int)userSelectedChannel.Value;

                if(channel >= NumberOfChannels)
                {
                    this.Log(LogLevel.Warning, "Tried to set channel#{0}, but only {1} channels are available; ignoring", channel, NumberOfChannels);
                    continue;
                }

                if(channelEnabled[channel].Value && channelValue[channel] != value)
                {
                    channelValue[channel] = value;
                    OutputChanged?.Invoke(channel, ConvertVoltage(value), value);
                }
            }

            endOfConversionInterruptPending.Value = true;
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.Write, name: "SWRST",
                    writeCallback: (_, value) => { if(value) Reset(); })
                .WithReservedBits(1, 31)
            ;

            Registers.Mode.Define(this)
                // NOTE: We only support free-running mode.
                .WithFlag(0, name: "TRGEN",
                    valueProviderCallback: _ => false)
                // NOTE: We only support free-running mode, therefore this
                //       field is not used. We are implementing it as value field
                //       to limit unnecessary logs.
                .WithValueField(1, 3, name: "TRGSEL")
                .WithFlag(4, out wordMode, name: "WORD")
                .WithReservedBits(5, 3)
                .WithFlag(8, name: "ONE",
                    valueProviderCallback: _ => true)
                .WithReservedBits(9, 7)
                .WithValueField(16, 2, out userSelectedChannel, name: "USER_SEL")
                .WithReservedBits(18, 2)
                .WithFlag(20, out tagSelectionMode, name: "TAG")
                .WithTaggedFlag("MAXS", 21)
                .WithReservedBits(22, 2)
                // NOTE: We don't implement startup delay.
                //       This should be implemented alongside trigger mode.
                .WithValueField(24, 6, name: "STARTUP")
                .WithReservedBits(30, 2)
            ;

            Registers.ChannelEnable.Define(this)
                .WithFlags(0, 2, FieldMode.Set, name: "CH",
                    writeCallback: (index, _, value) => { if(value) channelEnabled[index].Value = true; })
                .WithReservedBits(2, 30)
            ;

            Registers.ChannelDisable.Define(this)
                .WithFlags(0, 2, FieldMode.WriteToClear, name: "CH",
                    writeCallback: (index, _, value) =>
                    {
                        if(!value || !channelEnabled[index].Value)
                        {
                            return;
                        }

                        channelEnabled[index].Value = false;
                        // NOTE: There is no analog output when channel is disabled
                        OutputChanged?.Invoke(index, 0, 0);
                    })
                .WithReservedBits(2, 30)
            ;

            Registers.ChannelStatus.Define(this)
                .WithFlags(0, 2, out channelEnabled, FieldMode.Read, name: "CH")
                .WithReservedBits(2, 30)
            ;

            Registers.ConversionData.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "DATA",
                    writeCallback: (_, value) => PerformConversion(value))
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, FieldMode.Set, name: "TXRDY",
                    writeCallback: (_, value) => { if(value) transmitReadyInterruptEnabled.Value = true; })
                .WithFlag(1, FieldMode.Set, name: "EOC",
                    writeCallback: (_, value) => { if(value) endOfConversionInterruptEnabled.Value = true; })
                .WithTaggedFlag("ENDTX", 2)
                .WithTaggedFlag("TXBUFE", 3)
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlag(0, FieldMode.WriteToClear, name: "TXRDY",
                    writeCallback: (_, value) => { if(value) transmitReadyInterruptEnabled.Value = false; })
                .WithFlag(1, FieldMode.WriteToClear, name: "EOC",
                    writeCallback: (_, value) => { if(value) endOfConversionInterruptEnabled.Value = false; })
                .WithTaggedFlag("ENDTX", 2)
                .WithTaggedFlag("TXBUFE", 3)
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out transmitReadyInterruptEnabled, FieldMode.Read, name: "TXRDY")
                .WithFlag(1, out endOfConversionInterruptEnabled, FieldMode.Read, name: "EOC")
                .WithTaggedFlag("ENDTX", 2)
                .WithTaggedFlag("TXBUFE", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "TXRDY",
                    valueProviderCallback: _ => true)
                .WithFlag(1, out endOfConversionInterruptPending, FieldMode.ReadToClear, name: "EOC")
                .WithTaggedFlag("ENDTX", 2)
                .WithTaggedFlag("TXBUFE", 3)
                .WithReservedBits(4, 28)
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Registers.AnalogCurrent.Define(this)
                .WithTag("IBCTLCH0", 0, 2)
                .WithTag("IBCTLCH1", 2, 2)
                .WithReservedBits(4, 4)
                .WithTag("IBCTLDACCORE", 8, 2)
                .WithReservedBits(10, 22)
            ;

            Registers.WriteProtectionMode.Define(this)
                .WithFlag(0, out var enableWriteProtection, FieldMode.Write, name: "WPEN")
                .WithReservedBits(1, 7)
                .WithValueField(8, 24, out var writeProtectionKey, FieldMode.Write, name: "WPKEY")
                .WithWriteCallback((_, __) =>
                {
                    if(writeProtectionKey.Value != WriteProtectionKey)
                    {
                        return;
                    }
                    writeProtectionEnabled = enableWriteProtection.Value;
                })
            ;

            Registers.WriteProtectionStatus.Define(this)
                .WithFlag(0, out writeViolationStatus, FieldMode.ReadToClear, name: "WPVS")
                .WithReservedBits(1, 7)
                .WithValueField(8, 8, out writeViolationSource, FieldMode.ReadToClear, name: "WPVSRC")
                .WithReservedBits(16, 16)
            ;
        }

        private bool writeProtectionEnabled;

        private IFlagRegisterField wordMode;
        private IValueRegisterField userSelectedChannel;
        private IFlagRegisterField tagSelectionMode;

        private IFlagRegisterField[] channelEnabled;

        private IFlagRegisterField transmitReadyInterruptEnabled;
        private IFlagRegisterField endOfConversionInterruptEnabled;

        private IFlagRegisterField endOfConversionInterruptPending;

        private IFlagRegisterField writeViolationStatus;
        private IValueRegisterField writeViolationSource;

        private readonly ulong[] writeProtectedOffsets = new ulong[]
        {
            (ulong)Registers.Mode,
            (ulong)Registers.ChannelEnable,
            (ulong)Registers.ChannelDisable,
            (ulong)Registers.AnalogCurrent,
        };

        private readonly ulong[] channelValue = new ulong[NumberOfChannels];

        private const int NumberOfChannels = 2;
        private const uint WriteProtectionKey = 0x444143;
        private const uint MaximumChannelValue = 0xFFF;

        public enum Registers
        {
            Control = 0x00,
            Mode = 0x04,
            ChannelEnable = 0x10,
            ChannelDisable = 0x14,
            ChannelStatus = 0x18,
            ConversionData = 0x20,
            InterruptEnable = 0x24,
            InterruptDisable = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            AnalogCurrent = 0x94,
            WriteProtectionMode = 0xE4,
            WriteProtectionStatus = 0xE8
        }
    }
}
