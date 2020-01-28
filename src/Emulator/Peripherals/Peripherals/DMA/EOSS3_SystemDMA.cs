//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class EOSS3_SystemDMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public EOSS3_SystemDMA(Machine machine) : base(machine)
        {
            channels = new Channel[ChannelCount];
            for(var i = 0; i < ChannelCount; ++i)
            {
                channels[i] = new Channel();
            }
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Status.Define(this, 0x0)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => enable.Value, name: "master_enable")
                .WithReservedBits(1, 3)
                .WithEnumField<DoubleWordRegister, State>(4, 4, FieldMode.Read, name: "state")
                .WithReservedBits(8, 8)
                .WithValueField(16, 5, FieldMode.Read, valueProviderCallback: _ => ChannelCount - 1, name: "chnis_minus") // Number of available DMA channels minus one
                .WithReservedBits(21, 7)
                .WithValueField(28, 4, FieldMode.Read, name: "test_status") // 0 - no integration test logic, 1 - integration test logic, 0x2-0xF undefined
            ;

            Registers.Configuration.Define(this)
                .WithFlag(0, out enable, FieldMode.Write, name: "master_enable")
                .WithReservedBits(1, 4)
                .WithTag("chnl_prot_ctrl", 5, 3)
                .WithReservedBits(8, 24)
            ;

            Registers.ControlBasePointer.Define(this)
                .WithReservedBits(0, 9)
                .WithValueField(9, 23, name: "ctrl_base_ptr") // Pointer to the base address of the primary data structure
            ;

            Registers.AlternativeControlBasePointer.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "alt_ctrl_ptr") // Base address of the alternate data structure
            ;

            Registers.WaitOnRequestStatus.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "dma_waitonreq_status")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelSoftwareRequest.Define(this)
                .WithValueField(0, 16, FieldMode.Write, name: "chnl_sw_request")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelUseBurstSet.Define(this)
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Set, name: "chnl_useburst")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelEnableClear.Define(this)
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Set, name: "chnl_useburst_clr")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelRequestMaskSet.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_req_mask_set")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelRequestMaskClear.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_req_mask_clr")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelEnableSet.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_enable_set")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelEnableClear.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_enable_clr")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelPrimaryAlternativeSet.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_pri_alt_set")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelPrimaryAlternativeClear.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_pri_alt_clr")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelPrioritySet.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_priority_set")
                .WithReservedBits(16, 16)
            ;

            Registers.ChannelPriorityClear.Define(this)
                .WithValueField(0, 15, FieldMode.Read | FieldMode.Set, name: "chnl_priority_clr")
                .WithReservedBits(16, 16)
            ;

            Registers.ErrorClear.Define(this)
                .WithFlag(0, name: "err_clr")
                .WithReservedBits(1, 31)
            ;

            Registers.PeripheralIdentification0.DefineMany(this, 4, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => peripheralId[idx]);
            });

            Registers.PrimeCellIdentification0.DefineMany(this, 4, setup: (reg, idx) =>
            {
                reg.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => primecellId[idx]);
            });
        }

        private static byte[] peripheralId = new byte[] { 0x30, 0xB2, 0x0B, 0x00 };
        private static byte[] primecellId = new byte[] { 0x0D, 0xF0, 0x05, 0xB1 };

        private IFlagRegisterField enable;

        private Channel [] channels;

        private const int ChannelCount = 16;

        private class Channel
        {
            public bool Enabled = false;

        }

        private enum Registers
        {
            Status = 0x00,
            Configuration = 0x04,
            ControlBasePointer = 0x08,
            AlternativeControlBasePointer = 0x0C,
            WaitOnRequestStatus = 0x10,
            ChannelSoftwareRequest = 0x14,
            ChannelUseBurstSet = 0x18,
            ChannelUseBurstClear = 0x1C,
            ChannelRequestMaskSet = 0x20,
            ChannelRequestMaskClear = 0x24,
            ChannelEnableSet = 0x28,
            ChannelEnableClear = 0x2C,
            ChannelPrimaryAlternativeSet = 0x30,
            ChannelPrimaryAlternativeClear = 0x34,
            ChannelPrioritySet = 0x38,
            ChannelPriorityClear = 0x3C,
            // GAP?
            ErrorClear = 0x4C,
            // GAP
            PeripheralIdentification0 = 0xFE0,
            PeripheralIdentification1 = 0xFE4,
            PeripheralIdentification2 = 0xFE8,
            PeripheralIdentification3 = 0xFEC,
            PeripheralIdentification4 = 0xFD0,
            // GAP
            PrimeCellIdentification0 = 0xFF0,
            PrimeCellIdentification1 = 0xFF4,
            PrimeCellIdentification2 = 0xFF8,
            PrimeCellIdentification3 = 0xFFC
        }

        private enum State
        {
            Idle = 0x0,
            ReadingChannelControllerData = 0x1,
            ReadingSourceDataEndPointer = 0x2,
            ReadingDestinationDataEndPointer = 0x3,
            ReadingSourceData = 0x4,
            WritingDestinationDatat = 0x5,
            WaitingForDMARequestToClear = 0x6,
            WritingChannelControllerData = 0x7,
            Stalled = 0x8,
            Done = 0x9
        }

    }
}
