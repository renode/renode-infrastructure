//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class MAX32650_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_ADC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public uint AIn0
        {
            get => aIn0;
            set => aIn0 = ValidateAIn(0, value);
        }

        public uint AIn1
        {
            get => aIn1;
            set => aIn1 = ValidateAIn(1, value);
        }

        public uint AIn2
        {
            get => aIn2;
            set => aIn2 = ValidateAIn(2, value);
        }

        public uint AIn3
        {
            get => aIn3;
            set => aIn3 = ValidateAIn(3, value);
        }

        private uint GetValueFromActiveChannel()
        {
            switch(currentChannel.Value)
            {
                case Channels.AIn0:
                    return AIn0;
                case Channels.AIn1:
                    return AIn1;
                case Channels.AIn2:
                    return AIn2;
                case Channels.AIn3:
                    return AIn3;
                case Channels.AIn0Div5:
                    return AIn0 / 5;
                case Channels.AIn1Div5:
                    return AIn1 / 5;
                default:
                    this.Log(LogLevel.Warning, "{0} is not supported; ignoring", currentChannel.Value);
                    return 0x00;
            }
        }

        private uint ValidateAIn(int idx, uint value)
        {
            if(value > ADCDataMask)
            {
                this.Log(LogLevel.Warning, "Tried to set AIn{0} to 0x{1:X}, which is bigger than 10-bits; value has been truncated to 0x{2:X03}", idx, value, value & ADCDataMask);
                value &= ADCDataMask;
            }
            return value;
        }

        private void UpdateInterrupts()
        {
            var pending = false;

            pending |= interruptDoneEnabled.Value && interruptDonePending.Value;
            pending |= interruptReferenceReadyEnabled.Value && interruptReferenceReadyPending.Value;

            interruptAnyPending.Value = pending;
            IRQ.Set(pending);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "CTRL.start",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            interruptDonePending.Value = true;
                            UpdateInterrupts();
                        }
                    })
                .WithTaggedFlag("CTRL.pwr", 1)
                .WithReservedBits(2, 1)
                .WithFlag(3, name: "CTRL.refbuf_pwr",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.machine.LocalTimeSource.ExecuteInNearestSyncedState(__ =>
                            {
                                interruptReferenceReadyPending.Value = true;
                                UpdateInterrupts();
                            });
                        }
                    })
                .WithFlag(4, name: "CTRL.ref_sel")
                .WithReservedBits(5, 3)
                .WithTaggedFlag("CTRL.ref_scale", 8)
                .WithTaggedFlag("CTRL.input_scale", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("CTRL.clk_en", 11)
                .WithEnumField<DoubleWordRegister, Channels>(12, 4, out currentChannel, name: "CTRL.ch_sel")
                .WithReservedBits(16, 1)
                .WithEnumField<DoubleWordRegister, Alignment>(17, 1, out dataAlignment, name: "CTRL.data_align")
                .WithReservedBits(19, 13)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "STATUS.active",
                    valueProviderCallback: _ => false)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("STATUS.pwr_up_active", 2)
                .WithTaggedFlag("STATUS.overflow", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.OutputData.Define(this)
                .WithValueField(0, 16, name: "DATA.data",
                    valueProviderCallback: _ =>
                    {
                        var data = GetValueFromActiveChannel();
                        // Data can be justified either to LSB or MSB of the register.
                        // When alignment is set to LSB, we don't have to do anything;
                        // when it's set to MSB, we have to shift it in such way that
                        // MSB of 10-bit data is at 15th position of the register.
                        if(dataAlignment.Value == Alignment.MSB)
                        {
                            data <<= 6;
                        }
                        return data;
                    })
                .WithReservedBits(16, 16)
            ;

            Registers.InterruptControl.Define(this)
                .WithFlag(0, out interruptDoneEnabled, name: "INTR.done_ie")
                .WithFlag(1, out interruptReferenceReadyEnabled, name: "INTR.ref_ready_ie")
                .WithTaggedFlag("INTR.hi_limit_ie", 2)
                .WithTaggedFlag("INTR.lo_limit_ie", 3)
                .WithTaggedFlag("INTR.overflow_ie", 4)
                .WithReservedBits(5, 11)
                .WithFlag(16, out interruptDonePending, FieldMode.WriteOneToClear | FieldMode.Read, name: "INTR.done_if")
                .WithFlag(17, out interruptReferenceReadyPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "INTR.ref_ready_if")
                .WithTaggedFlag("INTR.hi_limit_if", 18)
                .WithTaggedFlag("INTR.lo_limit_if", 19)
                .WithTaggedFlag("INTR.overflow_if", 20)
                .WithReservedBits(21, 1)
                .WithFlag(22, out interruptAnyPending, FieldMode.Read, name: "INTR.pending")
                .WithReservedBits(23, 9)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            for(var i = 0; i < LimitRegistersCount; ++i)
            {
                (Registers.Limit0 + (long)(i * 0x04)).Define(this)
                    .WithTag($"LIMIT{i}.ch_lo_limit", 0, 10)
                    .WithReservedBits(10, 2)
                    .WithTag($"LIMIT{i}.ch_hi_limit", 12, 10)
                    .WithReservedBits(22, 2)
                    .WithTag($"LIMIT{i}.ch_sel", 24, 4)
                    .WithTaggedFlag($"LIMIT{i}.ch_lo_limit_en", 28)
                    .WithTaggedFlag($"LIMIT{i}.ch_hi_limit_en", 29)
                    .WithReservedBits(30, 2)
                ;
            }
        }

        private const int LimitRegistersCount = 4;
        private const uint ADCDataMask = 0x3FF;

        private IEnumRegisterField<Alignment> dataAlignment;
        private IEnumRegisterField<Channels> currentChannel;

        private IFlagRegisterField interruptDoneEnabled;
        private IFlagRegisterField interruptReferenceReadyEnabled;

        private IFlagRegisterField interruptDonePending;
        private IFlagRegisterField interruptReferenceReadyPending;
        private IFlagRegisterField interruptAnyPending;

        private uint aIn0;
        private uint aIn1;
        private uint aIn2;
        private uint aIn3;

        private enum Alignment
        {
            LSB = 0,
            MSB,
        }

        private enum Channels
        {
            AIn0 = 0,
            AIn1,
            AIn2,
            AIn3,
            AIn0Div5,
            AIn1Div5,
            VDDBDiv5,
            VDDA,
            VCORE,
            VRTCDiv2,
            Reserved,
            VDDIODiv4,
            VDDIOHDiv4,
        }

        private enum Registers : long
        {
            Control = 0x00,
            Status = 0x04,
            OutputData = 0x08,
            InterruptControl = 0x0C,
            Limit0 = 0x10,
            Limit1 = 0x14,
            Limit2 = 0x18,
            Limit3 = 0x1C,
        }
    }
}
