//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;

namespace Antmicro.Renode.Peripherals.USB;

public partial class STM_USB
{
    private class Endpoint
    {
        public Endpoint(STM_USB parent, ulong offset, bool isIn, byte endpointNumber)
        {
            this.parent = parent;
            this.endpointNumber = endpointNumber;
            DefineRegisters(offset, isIn, endpointNumber == 0);
        }

        public IEnumRegisterField<EndpointTransferType> EndpointTransferTypeField { get; private set; }

        public bool Active => endpointActiveFlag.Value;

        protected void DefineRegisters(ulong offset, bool isIn, bool isEp0)
        {
            var alwaysTrue = (bool _) => true;
            var reg = ((Registers)((ulong)Registers.Control + offset)).Define(parent)
                .WithValueField(0, 11, out maxPacketSize, name: "Max packet size (MPSIZ)")
                .WithReservedBits(11, 4)
                .WithFlag(15, out endpointActiveFlag, valueProviderCallback: isEp0 ? alwaysTrue : null, name: "USB Endpoint enabled (USBAEP)")
                .WithTaggedFlag("Endpoint data PID (EONUM/DPID)", 16)
                .WithFlag(17, out nakStatusFlag, mode: FieldMode.Read, name: "NAK status (NAKSTS)")
                .WithEnumField<DoubleWordRegister, EndpointTransferType>(18, 2, out var endpointTransferTypeField, name: "Endpoint transfer type (EPTYP)")
                .If(isIn)
                    .Then(reg => reg.WithReservedBits(20, 1))
                    .Else(reg => reg.WithTaggedFlag("Snoop mode (SNPM)", 20))
                .WithTaggedFlag("STALL handshake (STALL)", 21)
                .If(isIn)
                    .Then(reg => reg.WithTag("Tx FIFO number (TXFNUM)", 22, 4))
                    .Else(reg => reg.WithReservedBits(22, 4))
                // The TRM doesn't say what happens when both fields are set at once
                .WithFlag(26, mode: FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        nakStatusFlag.Value = false;
                    }
                }, name: "Clear NAK (CNAK)")
                .WithFlag(27, mode: FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        nakStatusFlag.Value = true;
                    }
                }, name: "Set NAK (SNAK)")
                .WithTaggedFlag("Set DATA0 PID/Set even frame (SD0PID/SEVNFRM)", 28)
                .WithTaggedFlag("Set odd frame (SODDFRM)", 29)
                .WithTaggedFlag("Endpoint disable (EPDIS)", 30)
                .WithFlag(31, out endpointEnabledFlag, mode: FieldMode.Read | FieldMode.Set, valueProviderCallback: !isIn && isEp0 ? alwaysTrue : null, changeCallback: (_, value) =>
                {
                    if(value)
                    {
                        OnEndpointEnable();
                    }
                }, name: "Endpoint enable (EPENA)");
            // Some `valueProviderCallback`s provide non-0 values, run them so that fields have correct values
            reg.Read();

            ((Registers)((ulong)Registers.Interrupt + offset)).Define(parent)
                .WithFlag(0, out transferCompleteInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Transfer complete (XFRC)")
                .WithFlag(1, out endpointDisabledInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Endpoint disabled (EPD)")
                .WithTaggedFlag("AHB error (AHBERR)", 2)
                .If(isIn)
                    .Then(reg => reg
                        .WithTaggedFlag("Timeout condition (TOC)", 3)
                        .WithTaggedFlag("IN token received when Tx Fifo empty (ITTXFE)", 4)
                        .WithTaggedFlag("IN token received with EP mismatch (INEPNM)", 5)
                        .WithTaggedFlag("IN endpoint NAK effective (INEPNE)", 6)
                        .WithFlag(7, out txFifoEmptyInterruptFlag, mode: FieldMode.Read, name: "TX Fifo empty (TXFE)"))
                    .Else(reg => reg
                        .WithFlag(3, out setupPhaseDoneInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Setup phase done (STUP)")
                        .WithTaggedFlag("Out token received when endpoint disabled (OTEPDIS)", 4)
                        .WithTaggedFlag("Status phase received for control write (STSPHSRX)", 5)
                        .WithTaggedFlag("Back-to-back SETUP packets received (B2BSTUP)", 6)
                        .WithReservedBits(7, 1))
                .WithTaggedFlag("Buffer not available (BNA)", 9)
                .WithReservedBits(10, 1)
                .If(isIn)
                    .Then(reg => reg
                        .WithTaggedFlag("Packet dropped status (PKTDRPSTS)", 11)
                        .WithReservedBits(12, 1))
                    .Else(reg => reg
                        .WithReservedBits(11, 1)
                        .WithTaggedFlag("Babble error (BERR)", 12))
                .WithTaggedFlag("Negative acknowledge (NAK)", 13)
                .If(isIn)
                    .Then(reg => reg.WithReservedBits(14, 2))
                    .Else(reg => reg
                        .WithTaggedFlag("Not yet (NYET)", 14)
                        .WithFlag(15, out setupPacketReceivedInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Setup packet received (STPKTRX)"))
                .WithReservedBits(16, 16)
                .WithChangeCallback((_, __) => parent.UpdateInterrupts());
            ((Registers)((ulong)Registers.TransferSize + offset)).Define(parent)
                .WithValueField(0, 19, out transferSizeField, name: "Transfer size (XFRSIZ)")
                .WithTag("Packet count (PKTCNT)", 19, 10, true)
                .WithTag("Received data PID/SETUP packet count/Multi count (RXDPID/STUPCNT/MCNT)", 29, 2)
                .WithReservedBits(31, 1);
            ((Registers)((ulong)Registers.DmaAddress + offset)).Define(parent)
                .WithTag("DMA address (DMAADDR)", 0, 32);
            if(isIn)
            {
                ((Registers)((ulong)Registers.FifoStatus + offset)).Define(parent)
                    .WithValueField(0, 16, mode: FieldMode.Read, valueProviderCallback: _ => (FifoAvailableSpace + 3) / 4, name: "Tx FIFO space available (INEPTFSAV)");
            }
            EndpointTransferTypeField = endpointTransferTypeField;
        }

        protected virtual void OnEndpointEnable()
        {
            // Intentionally left empty
        }

        protected virtual ulong FifoAvailableSpace => 0;

        protected IValueRegisterField maxPacketSize;
        protected IValueRegisterField transferSizeField;

        // This endpoint is active from a USB logic standpoint (ie. the revelant interface has been activated)
        protected IFlagRegisterField endpointActiveFlag;
        // This endpoint is waiting for relevant IN/OUT requests to send/receive data
        protected IFlagRegisterField endpointEnabledFlag;
        protected IFlagRegisterField nakStatusFlag;
        protected IFlagRegisterField transferCompleteInterruptFlag;
        protected IFlagRegisterField endpointDisabledInterruptFlag;
        protected IFlagRegisterField setupPhaseDoneInterruptFlag;
        protected IFlagRegisterField txFifoEmptyInterruptFlag;
        protected IFlagRegisterField setupPacketReceivedInterruptFlag;

        protected readonly byte endpointNumber;
        protected readonly STM_USB parent;

        private enum Registers
        {
            Control = 0x0,
            Interrupt = 0x8,
            TransferSize = 0x10,
            DmaAddress = 0x14,
            FifoStatus = 0x18, // only for IN endpoints
        }
    }
}
