//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public sealed class Cadence_USB : IUSBDevice, IProvidesRegisterCollection<ByteRegisterCollection>, IProvidesRegisterCollection<WordRegisterCollection>, IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_USB(IMachine machine)
        {
            this.machine = machine;

            IRQ = new GPIO();
            USBCore = new USBDeviceCore(this, customSetupPacketHandler: SetupPacketHandler);
            dmaCore = new DMACore(this);

            byteRegisters = new ByteRegisterCollection(this);
            wordRegisters = new WordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            return byteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return wordRegisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            wordRegisters.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return dmaCore.ReadDoubleWord(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dmaCore.WriteDoubleWord(offset, value);
        }

        public void Reset()
        {
            byteRegisters.Reset();
            wordRegisters.Reset();
            dmaCore.Reset();

            pendingConfigPacket = null;
            pendingConfigCallback = null;
            currentSetupPacket = null;
            IRQ.Set(false);
        }

        public void SpoofUSBEvent(UsbEvent usbevent)
        {
            switch(usbevent)
            {
            case UsbEvent.Resume:
                wakeupIrqPending.Value = true;
                break;
            case UsbEvent.Reset:
                resetIrqPending.Value = true;
                break;
            case UsbEvent.Suspend:
                suspendIrqPending.Value = true;
                break;
            case UsbEvent.Setup:
                setupDataValidIrqPending.Value = true;
                break;
            default:
                throw new RecoverableException("Invalid USB event type! Please use one of: 'RESUME', 'RESET', 'SUSPEND', 'SETUP'.");
            }
            UpdateUsbInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public USBDeviceCore USBCore { get; }

        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => byteRegisters;

        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => wordRegisters;

        private void UpdateUsbInterrupts()
        {
            var val = false;
            val |= suspendIrqEnabled.Value && suspendIrqPending.Value;
            val |= resetIrqEnabled.Value && resetIrqPending.Value;
            val |= setupDataValidIrqEnabled.Value && setupDataValidIrqPending.Value;
            val |= setupTokenIrqEnabled.Value && setupTokenIrqPending.Value;
            val |= wakeupIrqPending.Value;
            val |= dmaCore.InterruptPending;

            this.Log(LogLevel.Noisy, "IRQ: suspend: (enabled={0}, pending={1}) | reset: (enabled={2}, pending={3}) | setupDataValid: (enabled={4}, pending={5}) | setupToken: (enabled={6}, pending={7}) wakeup: (pending={8}) | dma_ioc: (pending={9}) | val: (value={10})",
                suspendIrqEnabled.Value, suspendIrqPending.Value,
                resetIrqEnabled.Value, resetIrqPending.Value,
                setupDataValidIrqEnabled.Value, setupDataValidIrqPending.Value,
                setupTokenIrqEnabled.Value, setupTokenIrqPending.Value,
                wakeupIrqPending.Value,
                dmaCore.InterruptPending,
                val
            );

            IRQ.Set(val);
        }

        private void SetupPacketHandler(SetupPacket packet, byte[] additionalData, Action<byte[]> resultCallback)
        {
            currentSetupPacket = packet;

            if(setupPacketResultCallback != null)
            {
                this.Log(LogLevel.Warning, "Attempted to start setup while a previous result handler is still active. USB Setup Packet hander aborted.");
                return;
            }

            // NOTE: The USBIP stack does not send the "SET_ADDRESS" setup request required for some USB drivers' init routines.
            // Check whether this is `SET_CONFIGURATION` while still in Address 0 (default state); if so, prepare a fake `SET_ADDRESS` transaction.
            if((packet.Request == (byte)StandardRequest.SetConfiguration) && (USBCore.Address == 0))
            {
                this.Log(LogLevel.Info, "Received SET_CONFIGURATION while in Default State. USBIP likely skipped SET_ADDRESS. Injecting fake SET_ADDRESS (address = {0}) first.", SpoofedUSBAddress);
                isSpoofingAddress = true;

                // Save the actual "Configuration request" for later
                pendingConfigPacket = packet;
                pendingConfigCallback = resultCallback;
                this.Log(LogLevel.Noisy, $"Assigned pendingConfigCallback to {resultCallback}");

                var addressPacket = new SetupPacket
                {
                    Direction = Direction.HostToDevice,
                    Type = PacketType.Standard,
                    Recipient = PacketRecipient.Device,
                    Request = (byte)StandardRequest.SetAddress,
                    Value = SpoofedUSBAddress,
                    Index = 0,
                    Count = 0
                };

                this.LoadSetupPacketData(addressPacket);
                currentSetupPacket = addressPacket;

                // Rest of the address spoof logic continued in the `ep0cs` write callback
                return;
            }

            this.LoadSetupPacketData(packet);
            setupPacketResultCallback = resultCallback;

            this.Log(LogLevel.Noisy, $"Assigned setupPacketResultCallback to {resultCallback}");
        }

        private void LoadSetupPacketData(SetupPacket packet)
        {
            var packetBytes = Packet.Encode(packet);
            for(var i = 0; i < USBSetupPacketSize; i++)
            {
                setupPacketData[i].Value = packetBytes[i];
            }
            setupBufferContentsChanged.Value = true;
            setupDataValidIrqPending.Value = true;
            UpdateUsbInterrupts();
        }

        private void HandleEp0CsWrite()
        {
            // Only act if the driver is handshaking (ACKing) the setup packet
            if(!handshakeNakBit.Value)
            {
                return;
            }

            if(isSpoofingAddress)
            {
                isSpoofingAddress = false;
                USBCore.Address = SpoofedUSBAddress;
                this.Log(LogLevel.Info, "Spoofed SET_ADDRESS acknowledged by driver. Injecting pending SET_CONFIGURATION.");

                // Now load the real packet that we held back previously
                if(pendingConfigPacket.HasValue)
                {
                    this.LoadSetupPacketData(pendingConfigPacket.Value);

                    // Restore the original packet and callback so the host gets ACKed
                    setupPacketResultCallback = pendingConfigCallback;
                    currentSetupPacket = pendingConfigPacket.Value;
                    pendingConfigPacket = null;
                    pendingConfigCallback = null;
                    this.Log(LogLevel.Noisy, $"Assigned setupPacketResultCallback to pendingConfigCallback");
                }
                return;
            }

            // If the packet has NO data stage (Count == 0), no DMA transfer occurs.
            // The HSNAK bit indicates the driver is done. We must ACK the Host now.
            if((currentSetupPacket?.Count == 0) && (setupPacketResultCallback != null))
            {
                this.Log(LogLevel.Debug, "Driver ACKed 0-length Setup Packet. Sending Status Stage to Host.");
                setupPacketResultCallback(Array.Empty<byte>());
                setupPacketResultCallback = null;
            }
        }

        private void DefineRegisters()
        {
            Registers.ep0cs.Define8(this)
                .WithTaggedFlag("STALL", 0)
                .WithFlag(1, out handshakeNakBit, name: "HSNAK")
                .WithTaggedFlag("TXBSY", 2)
                .WithTaggedFlag("RXBSY", 3)
                .WithTaggedFlag("DSTALL", 4)
                .WithFlag(7, out setupBufferContentsChanged, FieldMode.Read | FieldMode.WriteOneToClear, name: "CHGSET")
                .WithWriteCallback((_, __) => HandleEp0CsWrite());

            for(var i = 0; i < DataEndpointsCount; i++)
            {
                var baseEndpointRegisterAddress = (long)Registers.epBase + i * 8;

                wordRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.rxbc)
                    .WithIgnoredBits(0, 16);
                byteRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.rxcon)
                    .WithIgnoredBits(0, 8);
                byteRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.rxcs)
                    .WithIgnoredBits(0, 8);
                wordRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.txbc)
                    .WithIgnoredBits(0, 16);
                byteRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.txcon)
                    .WithIgnoredBits(0, 8);
                byteRegisters.DefineRegister(baseEndpointRegisterAddress + (long)EndpointRegisterFields.txcs)
                    .WithIgnoredBits(0, 8);
            }

            for(var i = 0; i < USBSetupPacketSize; i++)
            {
                var addr = (long)Registers.setupdat + i * 1;
                byteRegisters.DefineRegister(addr)
                    .WithValueField(0, 8, out setupPacketData[i]);
            }

            Registers.txirq.Define16(this)
                .WithIgnoredBits(0, 16);

            Registers.rxirq.Define16(this)
                .WithIgnoredBits(0, 16);

            Registers.usbirq.Define8(this)
                .WithFlag(0, out setupDataValidIrqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "SUDAV")
                .WithTaggedFlag("SOF", 1)
                .WithFlag(2, out setupTokenIrqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "SUTOK")
                .WithFlag(3, out suspendIrqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "SUSP")
                .WithFlag(4, out resetIrqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "URES")
                .WithTaggedFlag("HSPPED", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("LPMIR", 7)
                .WithWriteCallback((_, __) => UpdateUsbInterrupts());

            Registers.wkirq.Define8(this)
                .WithFlag(7, out wakeupIrqPending, FieldMode.Read | FieldMode.WriteOneToClear)
                .WithWriteCallback((_, __) => UpdateUsbInterrupts());

            Registers.txien.Define16(this)
                .WithIgnoredBits(0, 16);
            Registers.rxien.Define16(this)
                .WithIgnoredBits(0, 16);

            Registers.usbien.Define8(this)
                .WithFlag(0, out setupDataValidIrqEnabled, name: "SUDAVIE")
                .WithTaggedFlag("SOFIE", 1)
                .WithFlag(2, out setupTokenIrqEnabled, name: "SUTOKIE")
                .WithFlag(3, out suspendIrqEnabled, name: "SUSPIE")
                .WithFlag(4, out resetIrqEnabled, name: "URESIE")
                .WithTaggedFlag("HSPIE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("LPMIE", 7)
                .WithWriteCallback((_, __) => UpdateUsbInterrupts());

            Registers.extien.Define8(this)
                .WithIgnoredBits(0, 8);

            Registers.endprst.Define8(this)
                .WithIgnoredBits(0, 8);

            Registers.usbcs.Define8(this)
                .WithIgnoredBits(0, 8);
            Registers.speedctrl.Define8(this)
                .WithFlag(0, name: "SPEEDCTRL_LS", valueProviderCallback: _ => false)
                .WithFlag(1, name: "SPEEDCTRL_FS", valueProviderCallback: _ => true)
                .WithFlag(2, name: "SPEEDCTRL_HS", valueProviderCallback: _ => false)
                .WithFlag(7, name: "SPEEDCTRL_HSDISABLE", valueProviderCallback: _ => true);

            Registers.ep0maxpack.Define8(this)
                .WithIgnoredBits(0, 8);

            for(var i = 0; i < DataEndpointsCount; i++)
            {
                var addr = (long)Registers.rxmaxpackBase + 2 * i;
                wordRegisters.DefineRegister(addr)
                    .WithIgnoredBits(0, 16);
            }

            for(var i = 0; i < DataEndpointsCount; i++)
            {
                // 4-byte stride due to alignment (registers are 16-bit wide)
                var baseTxAddr = (long)Registers.txstaddrBase + i * 4;
                var baseRxAddr = (long)Registers.rxstaddrBase + i * 4;

                wordRegisters.DefineRegister(baseTxAddr)
                    .WithIgnoredBits(0, 16);

                wordRegisters.DefineRegister(baseRxAddr)
                    .WithIgnoredBits(0, 16);
            }

            for(var i = 0; i < DataEndpointsCount; i++)
            {
                // 4-byte stride due to alignment (registers are 16-bit wide)
                var baseAddr = (long)Registers.epirqBase + i * 4;

                // inirqm
                byteRegisters.DefineRegister(baseAddr)
                    .WithIgnoredBits(0, 8);

                // outirqm
                byteRegisters.DefineRegister(baseAddr + 2)
                    .WithIgnoredBits(0, 8);
            }

            Registers.cpuctrl.Define8(this)
                .WithIgnoredBits(0, 8);

            for(var i = 0; i < DataEndpointsCount; i++)
            {
                var addr = (long)Registers.txmaxpackBase + 2 * i;
                wordRegisters.DefineRegister(addr)
                    .WithIgnoredBits(0, 16);
            }
        }

        /* Spoofing USB address state */
        private bool isSpoofingAddress = false;
        private SetupPacket? pendingConfigPacket;

        private IFlagRegisterField setupDataValidIrqEnabled;
        private IFlagRegisterField setupDataValidIrqPending;
        private IFlagRegisterField setupTokenIrqEnabled;
        private IFlagRegisterField setupTokenIrqPending;
        private IFlagRegisterField suspendIrqEnabled;
        private IFlagRegisterField suspendIrqPending;
        private IFlagRegisterField resetIrqEnabled;
        private IFlagRegisterField resetIrqPending;
        private IFlagRegisterField wakeupIrqPending;

        private IFlagRegisterField handshakeNakBit;
        private IFlagRegisterField setupBufferContentsChanged;

        private SetupPacket? currentSetupPacket;
        private Action<byte[]> pendingConfigCallback;
        private Action<byte[]> setupPacketResultCallback;

        private readonly IValueRegisterField[] setupPacketData = new IValueRegisterField[8];

        private readonly IMachine machine;
        private readonly DMACore dmaCore;
        private readonly ByteRegisterCollection byteRegisters;
        private readonly WordRegisterCollection wordRegisters;

        private const int SpoofedUSBAddress = 0x1;
        private const int USBSetupPacketSize = 0x8;
        private const int DMARegistersOffset = 0x400;
        private const int DataEndpointsCount = 15;

        public enum UsbEvent
        {
            Resume,
            Reset,
            Suspend,
            Setup,
        }

        private class DMACore : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral
        {
            public DMACore(Cadence_USB parent)
            {
                this.parent = parent;
                RegistersCollection = new DoubleWordRegisterCollection(this);
                dmaEndpointInterruptStatus = new IFlagRegisterField[EndpointInterruptStatusCount];
                DefineRegisters();
            }

            public uint ReadDoubleWord(long offset)
            {
                return RegistersCollection.Read(offset);
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                RegistersCollection.Write(offset, value);
            }

            public void Reset()
            {
                RegistersCollection.Reset();
            }

            public bool InterruptPending => endpointInterruptPending.Value;

            public DoubleWordRegisterCollection RegistersCollection { get; }

            private void DefineRegisters()
            {
                DMARegisters.conf.Define32(this)
                    .WithTaggedFlag("DMARF_RESET", 0)
                    .WithTaggedFlag("DMARF_LENDIAN", 5)
                    .WithTaggedFlag("DMARF_BENDIAN", 6)
                    .WithTaggedFlag("DMARF_SWRST", 7)
                    .WithTaggedFlag("DMARF_DSING", 8)
                    .WithTaggedFlag("DMARF_DMULT", 9)
                    .WithReservedBits(10, 22);

                DMARegisters.sts.Define32(this)
                    .WithTaggedFlag("DMARF_DTRANS", 3)
                    .WithTaggedFlag("DMARF_ENDIAN", 7)
                    .WithReservedBits(8, 23)
                    .WithTaggedFlag("DMARF_ENDIAN2", 31);

                DMARegisters.ep_sel.Define32(this)
                    .WithValueField(0, 4, out selectedEndpointNumber, name: "DMARM_EP_NUM")
                    .WithReservedBits(4, 3)
                    .WithFlag(7, out selectedEndpointDirection, name: "DMARM_EP_DIR")
                    .WithReservedBits(8, 24);

                DMARegisters.traddr.Define32(this)
                    .WithValueField(0, 32, out trbAddress, name: "DMARM_EP_TRADDR");

                DMARegisters.ep_cfg.Define32(this)
                    .WithTaggedFlag("DMARF_EP_ENABLE", 0)
                    .WithReservedBits(1, 6)
                    .WithTaggedFlag("DMARF_EP_ENDIAN", 7)
                    .WithReservedBits(8, 4)
                    .WithTaggedFlag("DMARF_EP_DSING", 12)
                    .WithTaggedFlag("DMARF_EP_DMULT", 13)
                    .WithReservedBits(14, 18);

                DMARegisters.ep_cmd.Define32(this)
                    .WithTaggedFlag("DMARF_EP_EPRST", 0)
                    .WithReservedBits(1, 5)
                    .WithFlag(6, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            StartDmaTransfer();
                        }
                    }, name: "DMARF_EP_DRDY")
                    .WithTaggedFlag("DMARF_EP_DFLUSH", 7)
                    .WithReservedBits(8, 24);

                DMARegisters.ep_sts.Define32(this)
                    // Set IOC/ISP - using one variable as they are not really that different from the HAL standpoint
                    .WithFlag(2, out endpointInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMARF_EP_IOC")
                    .WithFlag(3, out endpointInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMARF_EP_ISP")
                    .WithTaggedFlag("DMARF_EP_DESCMIS", 4)
                    .WithReservedBits(5, 2)
                    .WithFlag(7, out endpointTrbError, name: "DMARF_EP_TRBERR")
                    .WithReservedBits(8, 1)
                    .WithTaggedFlag("DMARF_EP_DBUSY", 9)
                    .WithReservedBits(10, 1)
                    .WithTaggedFlag("DMARF_EP_CCS", 11)
                    .WithReservedBits(12, 2)
                    .WithTaggedFlag("DMARF_EP_OUTSMM", 14)
                    .WithTaggedFlag("DMARF_EP_ISOERR", 15)
                    .WithReservedBits(16, 15)
                    .WithTaggedFlag("DMARF_EP_DTRANS", 31)
                    .WithWriteCallback((_, __) => parent.UpdateUsbInterrupts());

                DMARegisters.ep_sts_sid.Define32(this)
                    .WithTag("DMARF_EP_STS_SID", 0, 16)
                    .WithReservedBits(16, 16);

                DMARegisters.ep_sts_en.Define32(this)
                    .WithReservedBits(0, 4)
                    .WithTaggedFlag("DMARF_EP_DESCMISEN", 4)
                    .WithReservedBits(5, 2)
                    .WithTaggedFlag("DMARF_EP_TRBERREN", 7)
                    .WithReservedBits(8, 6)
                    .WithTaggedFlag("DMARF_EP_OUTSMMEN", 14)
                    .WithTaggedFlag("DMARF_EP_ISOERREN", 15)
                    .WithReservedBits(16, 16);

                DMARegisters.drbl.Define32(this)
                    .WithTag("DMARD_DRBL", 0, 32);

                DMARegisters.ep_ien.Define32(this)
                    .WithTag("DMARD_EP_INTERRUPT_EN", 0, 32);

                DMARegisters.ep_ists.Define32(this)
                    .WithFlags(0, 31, out dmaEndpointInterruptStatus, name: "DMARF_EP_ISTS");
            }

            private void StartDmaTransfer()
            {
                if(parent.setupPacketResultCallback == null)
                {
                    parent.Log(LogLevel.Error, "setupPacketResultCallback is null. Triggered USBDMA transfer without ongoing USB transfer. Aborting transfer!");
                    return;
                }

                var sysbus = parent.machine.GetSystemBus(parent);
                var trb = new TransferRequestBlock(this.trbAddress.Value, (int)selectedEndpointNumber.Value, selectedEndpointDirection.Value, sysbus);

                parent.Log(LogLevel.Debug, "Processing TRB at 0x{0:X}: {1}", this.trbAddress.Value, trb);

                if((trb.Endpoint != 0) || (trb.Direction != TRBDirection.In))
                {
                    parent.Log(LogLevel.Warning, "Unimplemented TRB configuration: {0}", trb);
                    return;
                }

                var data = sysbus.ReadBytes(trb.BufferAddress, trb.Length);

                parent.Log(LogLevel.Noisy, "Executing setupPacketResultCallback");
                parent.setupPacketResultCallback(data);
                parent.setupPacketResultCallback = null;
                endpointInterruptPending.Value = true;

                var statusIndex = GetEndpointInterruptIndex(trb.Endpoint, trb.Direction == TRBDirection.In);
                dmaEndpointInterruptStatus[statusIndex].Value = true;

                parent.UpdateUsbInterrupts();
            }

            private int GetEndpointInterruptIndex(int ep, bool dirIn)
            {
                if(ep > SupportedEndpoints)
                {
                    parent.Log(LogLevel.Error, "Attempted to raise interrupt for not supported EP {0} ({1})", ep, dirIn ? "IN" : "OUT");
                    return 0;
                }

                var statusIndex = ep;
                if(dirIn)
                {
                    statusIndex += InterruptStatusDirInOffset;
                }

                return statusIndex;
            }

            private IValueRegisterField trbAddress;
            private IValueRegisterField selectedEndpointNumber;
            private IFlagRegisterField selectedEndpointDirection;
            private IFlagRegisterField endpointTrbError;
            private IFlagRegisterField endpointInterruptPending;
            private IFlagRegisterField[] dmaEndpointInterruptStatus;

            private readonly Cadence_USB parent;

            private const int TRBSizeMask = 0x1FFFF;
            private const int InterruptStatusDirInOffset = 16;
            private const int SupportedEndpoints = 16;
            private const int EndpointInterruptStatusCount = SupportedEndpoints * 2;

            private struct TransferRequestBlock
            {
                public TransferRequestBlock(ulong address, int endpoint, bool directionIn, IBusController sysbus)
                {
                    var bufferSizeRaw = sysbus.ReadDoubleWord(address + 4);

                    Address = address;
                    BufferAddress = sysbus.ReadDoubleWord(address);
                    Length = (int)(bufferSizeRaw & TRBSizeMask);
                    Control = sysbus.ReadDoubleWord(address + 8);
                    Endpoint = endpoint;
                    Direction = directionIn ? TRBDirection.In : TRBDirection.Out;
                }

                public ulong Address { get; }

                public ulong BufferAddress { get; }

                public int Length { get; }

                public ulong Control { get; }

                public int Endpoint { get; }

                public TRBDirection Direction { get; }

                public override string ToString()
                {
                    return $"Buffer: 0x{BufferAddress:X}, Len: {Length}, EP: {Endpoint}, Dir: {Direction} (ctrl: 0x{Control:X})";
                }
            }

            private enum TRBDirection
            {
                Out,
                In
            }

            private enum DMARegisters
            {
                conf       = 0x400,
                sts        = 0x404,
                ep_sel     = 0x41C,
                traddr     = 0x420,
                ep_cfg     = 0x424,
                ep_cmd     = 0x428,
                ep_sts     = 0x42c,
                ep_sts_sid = 0x430,
                ep_sts_en  = 0x434,
                drbl       = 0x438,
                ep_ien     = 0x43C,
                ep_ists    = 0x440,
            }
        }

        private enum EndpointRegisterFields
        {
            rxbc  = 0x00,
            rxcon = 0x02,
            rxcs  = 0x03,
            txbc  = 0x04,
            txcon = 0x06,
            txcs  = 0x07,
        }

        // No public documentation is available, therefore:
        //   * register names are taken directly from the HAL headers
        //   * some registers have incomplete definitions
        private enum Registers
        {
            ep0Rxbc        = 0x000,
            ep0Txbc        = 0x001,
            ep0cs          = 0x002,

            lpmctrll       = 0x004,
            lpmctrlh       = 0x005,
            lpmclock       = 0x006,
            ep0fifoctrl    = 0x007,

            epBase         = 0x008,

            fifodatBase    = 0x084,

            ep0datatx      = 0x100,
            ep0datarx      = 0x140,
            setupdat       = 0x180,

            txirq          = 0x188,
            rxirq          = 0x18A,
            usbirq         = 0x18C,
            wkirq          = 0x18D,
            rxpngirq       = 0x18E,
            txfullirq      = 0x190,
            rxemptirq      = 0x192,

            txien          = 0x194,
            rxien          = 0x196,
            usbien         = 0x198,

            extien         = 0x199,
            rxpngien       = 0x19A,
            txfullien      = 0x19C,
            rxemptien      = 0x19E,

            usbivect       = 0x1A0,
            fifoivect      = 0x1A1,
            endprst        = 0x1A2,
            usbcs          = 0x1A3,
            frmnr          = 0x1A4,
            fnaddr         = 0x1A6,
            clkgate        = 0x1A7,
            fifoctrl       = 0x1A8,
            speedctrl      = 0x1A9,

            isoautoarm     = 0x1CC,
            adpbc1ien      = 0x1CE,
            adpbc2ien      = 0x1CF,
            adpbcctr0      = 0x1D0,
            adpbcctr1      = 0x1D1,
            adpbcctr2      = 0x1D2,
            adpbc1irq      = 0x1D3,
            adpbc0status   = 0x1D4,
            adpbc1status   = 0x1D5,
            adpbc2status   = 0x1D6,
            adpbc2irq      = 0x1D7,
            isodctrl       = 0x1D8,
            isoautodump    = 0x1DC,

            ep0maxpack     = 0x1E0,
            rxmaxpackBase  = 0x1E2,

            rxstaddrBase   = 0x304,
            txstaddrBase   = 0x344,
            epirqBase      = 0x384,

            cpuctrl        = 0x3C0,
            txmaxpackBase  = 0x3E2,
        }
    }
}