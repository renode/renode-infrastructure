//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USB;

public partial class STM_USB : BasicDoubleWordPeripheral, IUSBDevice, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
{
    public STM_USB(IMachine machine) : base(machine)
    {
        allMaskIn = new EndpointInterruptMask(this, isIn: true);
        allMaskOut = new EndpointInterruptMask(this, isIn: false);
        DefineRegisters();
    }

    public override void Reset()
    {
        UpdateInterrupts();
        rxFifo.Clear();
        rxFifoPackets.Clear();
        foreach(var ep in endpointsIn)
        {
            ep.Flush();
        }
        base.Reset();
    }

    public IUSBConnection ConnectUSB() => new USBConnection(this);

    public long Size => 0x40000;

    public GPIO IRQ { get; set; } = new GPIO();

    public byte Address => (byte)addressField.Value;

    private void DefineRegisters()
    {
        Registers.ControlAndStatus.Define(this)
            .WithTaggedFlag("Session request success (SRQSCS)", 0)
            .WithTaggedFlag("Session request (SRQ)", 1)
            .WithTaggedFlag("Vbus valid override enable (VBVALOEN)", 2)
            .WithTaggedFlag("Vbus valid override value (VBVALOVAL)", 3)
            .WithTaggedFlag("A-peripheral session valid override enable (AVALOEN)", 4)
            .WithTaggedFlag("A-peripheral session valid override value (AVALOVAL)", 5)
            .WithTaggedFlag("B-peripheral sesssion valid override enable (BVALOEN)", 6)
            .WithTaggedFlag("B-peripheral sesssion valid override value (BVALOVAL)", 7)
            .WithTaggedFlag("Host negotiation success (HNGSCS)", 8)
            .WithTaggedFlag("HNP request (HNPRQ)", 9)
            .WithTaggedFlag("Host set HNP enable (HSHNPEN)", 10)
            .WithTaggedFlag("Device HNP enabled (DHNPEN)", 11)
            .WithTaggedFlag("Embedded host enable (EHEN)", 12)
            .WithReservedBits(13, 2)
            .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "Connector ID status (CIDSTS)")
            .WithReservedBits(17, 2)
            .WithTaggedFlag("B-session valid (BSVLD)", 19)
            .WithTaggedFlag("OTG version (OTGVER)", 20)
            .WithTaggedFlag("Current mode of operation (CURMOD)", 21)
            .WithReservedBits(22, 10);

        Registers.OtgInterrupt.Define(this)
            .WithReservedBits(0, 2)
            .WithFlag(2, out otgSessionEndFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Session end detected (SEDET)")
            .WithReservedBits(3, 5)
            .WithTaggedFlag("Session request success status change (SRSSCHG)", 8)
            .WithTaggedFlag("Host negotiation success status change (HNSSCHG)", 9)
            .WithReservedBits(10, 7)
            .WithTaggedFlag("Host negotiation detected (HNGDET)", 17)
            .WithTaggedFlag("A-device timeout change (ADTOCHG)", 18)
            .WithReservedBits(19, 13)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.AHBConfiguration.Define(this)
            .WithFlag(0, out globalInterruptMaskFlag, changeCallback: (_, __) => UpdateInterrupts(), name: "Global interrupt mask (GINTMSK)")
            .WithTag("Burst length/type (HBSTLEN)", 1, 4)
            .WithTaggedFlag("DMA enabled (DMAEN)", 5)
            .WithReservedBits(6, 1)
            .WithTaggedFlag("Tx FIFO empty level (TXFELVL)", 7)
            .WithReservedBits(8, 24);

        Registers.USBConfiguration.Define(this, 0x1400)
            .WithTag("FS timeout calibration (TOCAL)", 0, 2)
            .WithReservedBits(3, 3)
            .WithTaggedFlag("Full speed serial transceiver mode select (PHYSEL)", 6)
            .WithReservedBits(7, 1)
            .WithTaggedFlag("SRP-capable (SRPCAP)", 8)
            .WithTaggedFlag("NHP-capable (NHPCAP)", 9)
            .WithTag("USB turnaround time (TRDT)", 10, 4)
            .WithReservedBits(14, 1)
            .WithTaggedFlag("PHY low-power clock select (PHYLPC)", 15)
            .WithReservedBits(16, 1)
            .WithTaggedFlag("ULPI FS/LS select (ULPIFSLS)", 17)
            .WithTaggedFlag("ULPI Auto-resume (ULPIAR)", 18)
            .WithTaggedFlag("ULPI clock SuspendM (ULPICSM)", 19)
            .WithTaggedFlag("ULPI external Vbus drive (ULPIEVBUSD)", 20)
            .WithTaggedFlag("ULPI external Vbus indicator (ULPIEVBUSI)", 21)
            .WithTaggedFlag("TermSel DLine pulsing section (TSDPS)", 22)
            .WithTaggedFlag("Indicator complement (PCCI)", 23)
            .WithTaggedFlag("Indicator pass through (PTCI)", 24)
            .WithTaggedFlag("ULPI interface protect disable (ULPIIPD)", 25)
            .WithReservedBits(26, 3)
            .WithFlag(29, out forceHostModeFlag, writeCallback: (_, __) => CheckForcedMode(), name: "Force host mode (FHMOD)")
            .WithFlag(30, out forceDeviceModeFlag, writeCallback: (_, __) => CheckForcedMode(), name: "Force device mode (FDMOD)")
            .WithReservedBits(31, 1);

        Registers.Reset.Define(this)
            .WithTaggedFlag("Core soft reset (CSRST)", 0)
            .WithTaggedFlag("Partial soft reset (PSRST)", 1)
            .WithReservedBits(2, 2)
            // TRM says these are write-to-set (until the clear is done), but since we clear immediately, the flag is always clear
            .WithFlag(4, mode: FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
            {
                if(value)
                {
                    rxFifo.Clear();
                    rxFifoPackets.Clear();
                }
            }, name: "Rx FIFO flush (RXFFLSH)")
            .WithFlag(5, out var txFifoFlush, name: "Tx FIFO flush (TXFFLSH)")
            .WithValueField(6, 5, out txFifoFlushIndexField, name: "Tx FIFO number (TXFNUM)")
            .WithReservedBits(11, 19)
            .WithTaggedFlag("DMA request signal enabled (DMAREQ)", 30)
            .WithFlag(31, mode: FieldMode.Read, valueProviderCallback: _ => true, name: "AHB master idle (AHBIDL)")
            .WithWriteCallback((_, __) =>
            {
                if(!txFifoFlush.Value)
                {
                    return;
                }
                txFifoFlush.Value = false;
                var idx = txFifoFlushIndexField.Value;
                if(idx == AllTxFifosFlushIndex)
                {
                    foreach(var ep in endpointsIn)
                    {
                        ep.Flush();
                    }
                }
                else if(idx < EndpointsNumber)
                {
                    var ep = endpointsIn[idx];
                    ep.Flush();
                }
                else
                {
                    this.WarningLog("Invalid Tx flush number from device: {0}", idx);
                }
            });

        Registers.CoreInterrupt.Define(this, 0x0400_0020)
            .WithTaggedFlag("Current mode of operation (CMOD)", 0)
            .WithTaggedFlag("Mode mismatch (MMIS)", 1)
            .WithFlag(2, mode: FieldMode.Read, valueProviderCallback: _ => otgSessionEndFlag.Value, name: "On-the-go (OTGINT)")
            .WithTaggedFlag("Start of frame (SOF)", 3)
            .WithFlag(4, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.Count > 0, name: "Rx Fifo level (RXFLVL)")
            .WithReservedBits(5, 1)
            .WithTaggedFlag("Global IN non-periodic NAK effective (GINAKEFF)", 6)
            .WithTaggedFlag("Global OUT NAK effective (GONAKEFF)", 7)
            .WithReservedBits(8, 2)
            .WithTaggedFlag("Early suspend (ESUSP)", 10)
            .WithTaggedFlag("USB suspend (USBSUSP)", 11)
            .WithFlag(12, out resetInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "USB reset (USBRST)")
            .WithFlag(13, out enumerationFinishedInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Enumeration done (ENUMDNE)")
            .WithTaggedFlag("Isochronous OUT packet dropped (ISOODRP)", 14)
            .WithTaggedFlag("End of periodic frame (EOPF)", 15)
            .WithReservedBits(16, 2)
            .WithFlag(18, mode: FieldMode.Read, valueProviderCallback: _ => InEndpoinsInterruptActive, name: "In endpoint interrupt (IEPINT)")
            .WithFlag(19, mode: FieldMode.Read, valueProviderCallback: _ => OutEndpointsInterruptActive, name: "Out endpoint interrupt (OEPINT)")
            .WithTaggedFlag("Incomplete isochronous IN transfer (IISOIXFR)", 20)
            .WithTaggedFlag("Incomplete periodic/isochronous OUT transfer (IPXFR/INCOMPISOOUT)", 21)
            .WithTaggedFlag("Data fetch suspended (DATAFSUSP)", 22)
            .WithTaggedFlag("Reset detected (RSTDET)", 23)
            .WithReservedBits(24, 3)
            .WithTaggedFlag("LPM (LPMINT)", 27)
            .WithTaggedFlag("Connector ID status change (CIDSCHG)", 28)
            .WithReservedBits(29, 1)
            .WithFlag(30, out connectedInterruptFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "Session request (SRQINT)")
            .WithTaggedFlag("Wake-up detected (WKUPINT)", 31)
            .WithWriteCallback((_, __) => UpdateInterrupts());

        Registers.CoreInterruptMask.Define(this)
            .WithReservedBits(0, 1)
            .WithTaggedFlag("Mode mismatch (MMIS)", 1)
            .WithFlag(2, out otgInterruptMaskFlag, name: "On-the-go (OTGINT)")
            .WithTaggedFlag("Start of frame (SOF)", 3)
            .WithFlag(4, out rxFifoNotEmptyInterruptFlag, name: "Rx Fifo level (RXFLVL)")
            .WithReservedBits(5, 1)
            .WithTaggedFlag("Global IN non-periodic NAK effective (GINAKEFF)", 6)
            .WithTaggedFlag("Global OUT NAK effective (GONAKEFF)", 7)
            .WithReservedBits(8, 2)
            .WithTaggedFlag("Early suspend (ESUSP)", 10)
            .WithTaggedFlag("USB suspend (USBSUSP)", 11)
            .WithFlag(12, out resetInterruptMaskFlag, name: "USB reset (USBRST)")
            .WithFlag(13, out enumerationFinishedInterruptMaskFlag, name: "Enumeration done (ENUMDNE)")
            .WithTaggedFlag("Isochronous OUT packet dropped (ISOODRP)", 14)
            .WithTaggedFlag("End of periodic frame (EOPF)", 15)
            .WithReservedBits(16, 2)
            .WithFlag(18, out allEpInInterruptMaskFlag, name: "In endpoint interrupt (IEPINT)")
            .WithFlag(19, out allEpOutInterruptMaskFlag, name: "Out endpoint interrupt (OEPINT)")
            .WithTaggedFlag("Incomplete isochronous IN transfer (IISOIXFR)", 20)
            .WithTaggedFlag("Incomplete periodic/isochronous OUT transfer (IPXFR/INCOMPISOOUT)", 21)
            .WithTaggedFlag("Data fetch suspended (DATAFSUSP)", 22)
            .WithTaggedFlag("Reset detected (RSTDET)", 23)
            .WithReservedBits(24, 3)
            .WithTaggedFlag("LPM (LPMINT)", 27)
            .WithTaggedFlag("Connector ID status change (CIDSCHG)", 28)
            .WithReservedBits(29, 1)
            .WithFlag(30, out connectedInterruptMaskFlag, name: "Session request (SRQINT)")
            .WithTaggedFlag("Wake-up detected (WKUPINT)", 31)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.RxFifoStatusDebug.Define(this)
            .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? packet.Endpoint : 0u, name: "Endpoint number (EPNUM)")
            .WithValueField(4, 11, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? (ulong)packet.Data.Length : 0u, name: "Byte count (BCNT)")
            .WithTag("Data PID (DPID)", 15, 2)
            .WithValueField(17, 4, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? (ulong)packet.Status : 0u, name: "Packet status (PKTSTS)")
            .WithTag("Frame number (FRMNUM)", 21, 4)
            .WithReservedBits(25, 2)
            .WithTaggedFlag("Status phase start (STSPHST)", 27)
            .WithReservedBits(28, 4);

        Registers.RxFifoStatusPop.Define(this)
            .WithValueField(0, 4, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? packet.Endpoint : 0u, name: "Endpoint number (EPNUM)")
            .WithValueField(4, 11, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? (ulong)packet.Data.Length : 0u, name: "Byte count (BCNT)")
            .WithTag("Data PID (DPID)", 15, 2)
            .WithValueField(17, 4, mode: FieldMode.Read, valueProviderCallback: _ => rxFifoPackets.TryPeek(out var packet) ? (ulong)packet.Status : 0u, name: "Packet status (PKTSTS)")
            .WithTag("Frame number (FRMNUM)", 21, 4)
            .WithReservedBits(25, 2)
            .WithTaggedFlag("Status phase start (STSPHST)", 27)
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) =>
            {
                if(!rxFifoPackets.TryDequeue(out var packet))
                {
                    return;
                }
                this.DebugLog("Dequeued RX packet: {0}, {1} packets left", packet, rxFifoPackets.Count);
                rxFifo.EnqueueRange(BitHelper.ToUInt32Array(packet.Data, littleEndian: true));
                var ep = endpointsOut[packet.Endpoint];
                ep.SetInterruptsForPacket(packet);
            });

        Registers.RxFifoDepth.Define(this)
            .WithTag("Rx FIFO depth (RXFD)", 0, 16)
            .WithReservedBits(16, 16);

        Registers.EndpointTxFifo0.Define(this, 0x0200_0200)
            .WithTag("Tx FIFO 0 start address (TX0FSA)", 0, 16)
            .WithTag("Tx FIFO 0 address (TX0FD)", 16, 16);

        Registers.GeneralCoreConfiguration.Define(this)
            .WithTaggedFlag("Data contant detection (DCDET)", 0)
            .WithTaggedFlag("Primary detection (PDET)", 1)
            .WithTaggedFlag("Secondary detection (SDET)", 2)
            .WithTaggedFlag("DM pull-up detection (PS2DET)", 3)
            .WithReservedBits(4, 12)
            .WithTaggedFlag("Power down of FS PHY (PWRDWN)", 16)
            .WithTaggedFlag("Battery charging detector enable (BCDEN)", 17)
            .WithTaggedFlag("Data contact detection mode enable (DCDEN)", 18)
            .WithTaggedFlag("Primary detection mode enable (PDEN)", 19)
            .WithTaggedFlag("Secondary detection mode enable (SDEN)", 20)
            .WithFlag(21, out vbusSensingEnableFlag, name: "USB Cbus detection enable (VBDEN)")
            .WithReservedBits(22, 10);

        Registers.CoreID.Define(this, 0x2300)
            .WithReservedBits(0, 32);

        Registers.Undocumented_SynopsisID.Define(this, 0x4F54310A)
            .WithReservedBits(0, 32);

        Registers.CoreLPMConfiguration.Define(this)
            .WithTaggedFlag("LPM support enable (LPMEN)", 0)
            .WithTaggedFlag("LPM token acknowledge enable (LPMACK)", 1)
            .WithTag("Best effort service latency (BESL)", 2, 4)
            .WithTaggedFlag("bRemoteWake value (REMWAKE)", 6)
            .WithTaggedFlag("L1 Shallow Sleep enable (L1SSEN)", 7)
            .WithTag("BESL threshold (BESLTHRS)", 8, 4)
            .WithTaggedFlag("L1 deep sleep enable (L1DSEN)", 12)
            .WithTag("LPM response (LPMRSP)", 13, 2)
            .WithTaggedFlag("Port sleep status (SLPSTS)", 15)
            .WithTaggedFlag("Sleep state resume OK (L1RSMOK)", 16)
            .WithTag("LPM channel index (LPMCHIDX)", 17, 4)
            .WithTag("LPM retry count (LPMRCNT)", 21, 2)
            .WithTaggedFlag("Send LPM transaction (SNDLPM)", 24)
            .WithTag("LPM retyr count status (LPMRCNTSTS)", 25, 3)
            .WithTaggedFlag("Enable best-effort service latency (ENBESL)", 28)
            .WithReservedBits(29, 3);

        for(int idx = 1; idx < EndpointsNumber; idx += 1)
        {
            // NOTE: "address" here refers to PHY-internal RAM, not sysbus (hence why it's 16 bits)
            uint resetValue = (uint)0x0200_0000 | (uint)(0x200 * (idx + 1));
            ((Registers)(Registers.DeviceEndpointTxFifo1 + 4 * (idx - 1))).Define(this, resetValue)
                .WithTag("Tx Fifo address (INEPTXSA)", 0, 16)
                .WithTag("Tx Fifo depth (INEPTXFD)", 16, 16);
        }
        // There are 7 endpoint registers, but STM32Cube writes to 15 registers on init, so let's reserve those
        for(int idx = EndpointsNumber; idx < MaxEndpointsNumber; idx += 1)
        {
            ((Registers)(Registers.DeviceEndpointTxFifo1 + 4 * (idx - 1))).Define(this)
                .WithReservedBits(0, 32);
        }

        Registers.DeviceConfiguration.Define(this, 0x0220_0000)
            .WithTag("Device speed (DSPD)", 0, 2)
            .WithTaggedFlag("Non-zero-length status OUT handshake (NZLSOHSK)", 2)
            .WithReservedBits(3, 1)
            .WithValueField(4, 7, out addressField, name: "Address (DAD)")
            .WithTag("Periodic frame interval (PFIVL)", 11, 2)
            .WithReservedBits(13, 1)
            .WithTaggedFlag("Transceiver delay (XCVRDLY)", 14)
            .WithTaggedFlag("Erratic error interrupt mask (ERRATIM)", 15)
            .WithReservedBits(16, 7)
            .WithTaggedFlag("Scatter/gather DMA (DESCDMA)", 23)
            .WithTag("Periodic schedule interval (PERSCHILV)", 24, 2)
            .WithReservedBits(26, 6);

        Registers.DeviceControl.Define(this, 0x2)
            .WithTaggedFlag("Remote wake-up signaling (RWUSIG)", 0)
            .WithFlag(1, out softDisconnectedFlag, changeCallback: (_, value) =>
            {
                if(!value && hostResetPending)
                {
                    hostResetPending = false;
                    HostReset();
                }
            }, name: "Soft disconnect (SDIS)")
            .WithTaggedFlag("Global IN NAK (GINSTS)", 2)
            .WithTaggedFlag("Global OUT NAK (GINSTS)", 3)
            .WithTag("TCTL", 4, 3)
            .WithTaggedFlag("Set global IN NAK (SGINAK)", 7)
            .WithTaggedFlag("Clear global IN NAK (SGINAK)", 8)
            .WithTaggedFlag("Set global OUT NAK (SGONAK)", 9)
            .WithTaggedFlag("Clear global OUT NAK (SGONAK)", 10)
            .WithTaggedFlag("Power-on programming done (POPRGDNE)", 11)
            .WithReservedBits(12, 5)
            .WithTaggedFlag("Enable continue on BNA (ENCONTONBA)", 17)
            .WithTaggedFlag("Deep sleep BESL reject (DSBESLRJCT)", 18)
            .WithReservedBits(19, 13);

        Registers.DeviceStatus.Define(this, 0x10)
            .WithTaggedFlag("Suspend status (SUSPSTS)", 0)
            .WithValueField(1, 2, valueProviderCallback: _ => (byte)EnumeratedSpeed.FullSpeedHSPhy, name: "Enumerated speed (ENUMSPD)")
            .WithTaggedFlag("Erratic error (EERR)", 3)
            .WithReservedBits(4, 4)
            .WithTag("Frame number of start-of-frame (FNSOR)", 8, 14)
            .WithTag("Device line status (DEVLNSTS)", 22, 2)
            .WithReservedBits(24, 8);

        Registers.DeviceEpCommonInInterruptMask.Bind(this, allMaskIn.Register);

        Registers.DeviceEpCommonOutInterruptMask.Bind(this, allMaskOut.Register);

        Registers.DeviceEpAllInterrupt.Define(this)
            .WithFlags(0, EndpointsNumber, mode: FieldMode.Read, valueProviderCallback: (idx, _) => endpointsIn[idx].MaskMatches(allMaskIn), name: "Out EP interrupt (OEPINT)")
            .WithReservedBits(EndpointsNumber, 16 - EndpointsNumber)
            .WithFlags(16, EndpointsNumber, mode: FieldMode.Read, valueProviderCallback: (idx, _) => endpointsOut[idx].MaskMatches(allMaskOut), name: "In EP interrupt (IEPINT)")
            .WithReservedBits(16 + EndpointsNumber, 16 - EndpointsNumber);

        Registers.DeviceEpAllInterruptMask.Define(this)
            .WithFlags(0, EndpointsNumber, out epInInterruptMaskFlags, name: "Out EP interrupt mask (OEPM)")
            .WithReservedBits(EndpointsNumber, 16 - EndpointsNumber)
            .WithFlags(16, EndpointsNumber, out epOutInterruptMaskFlags, name: "In EP interrupt mask (IEPM)")
            .WithReservedBits(16 + EndpointsNumber, 16 - EndpointsNumber)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.DeviceVbusDischargeTime.Define(this, 0x17D7)
            .WithTag("Vbus discharge time (VBUSDT)", 0, 16)
            .WithReservedBits(16, 16);

        Registers.DeviceVbusPulsingTime.Define(this, 0x5B8)
            .WithTag("Vbus pulsing time (DVBUSP)", 0, 16)
            .WithReservedBits(16, 16);

        Registers.DeviceThresholdControl.Define(this)
            .WithTaggedFlag("Non-ISO IN endpoints threshold enable (NONISOTHREN)", 0)
            .WithTaggedFlag("ISO IN endpoint threshold enable (ISOTHREN)", 1)
            .WithTag("Transmit threshold length (TXTHRLEN)", 2, 9)
            .WithReservedBits(11, 5)
            .WithTaggedFlag("Received threshold enable (RXTHREN)", 16)
            .WithTag("Receive threshold length (RXTHRLEN)", 17, 9)
            .WithReservedBits(26, 1)
            .WithTaggedFlag("Arbiter parking enable (ARPEN)", 27)
            .WithReservedBits(28, 4);

        Registers.DeviceEpInFifoEmptyInterruptMask.Define(this)
            .WithFlags(0, EndpointsNumber, out var fifoEmptyInterruptMaskFlags, name: "In EP Tx Fifo empty interrupt mask (INEPTXFEM)")
            .WithReservedBits(EndpointsNumber, 32 - EndpointsNumber)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.DeviceEpEachInterrupt.Define(this)
            .WithReservedBits(0, 1)
            .WithTaggedFlag("IN endpoint 1 (IEP1INT)", 1)
            .WithReservedBits(2, 15)
            .WithTaggedFlag("OUT endpoint 1 (OEP1INT)", 17)
            .WithReservedBits(18, 14);

        Registers.DeviceEpEachInterruptMask.Define(this)
            .WithReservedBits(0, 1)
            .WithTaggedFlag("IN endpoint 1 mask (IEP1INT)", 1)
            .WithReservedBits(2, 15)
            .WithTaggedFlag("OUT endpoint 1 mask (OEP1INT)", 17)
            .WithReservedBits(18, 14);

        Registers.DeviceEp1InInteruptMask.Bind(this, new EndpointInterruptMask(this, isIn: true).Register);
        Registers.DeviceEp1OutInteruptMask.Bind(this, new EndpointInterruptMask(this, isIn: false).Register);

        for(uint idx = 0; idx < EndpointsNumber; idx += 1)
        {
            endpointsIn[idx] = new EndpointIn(this, idx, fifoEmptyInterruptMaskFlags[idx]);
            endpointsOut[idx] = new EndpointOut(this, idx);
        }

        Registers.PowerAndClockGatingControl.Define(this, 0x200B_8000)
            .WithTaggedFlag("Stop PHY clock (STPPCLK)", 0)
            .WithTaggedFlag("Gate HCLK (GATEHCLK)", 1)
            .WithReservedBits(2, 2)
            .WithTaggedFlag("PHY suspended (PHYSUSP)", 4)
            .WithTaggedFlag("Enable sleep clock gating (ENL1GTG)", 5)
            .WithTaggedFlag("PHY in sleep (PHYSLEEP)", 6)
            .WithTaggedFlag("PHY in deep sleep (SUSP)", 7)
            .WithReservedBits(8, 24);

        Registers.Fifo0.Define(this)
            .WithValueField(0, 32, mode: FieldMode.Read | FieldMode.Write,
                valueProviderCallback: _ =>
                {
                    if(rxFifo.TryDequeue(out var value))
                    {
                        return value;
                    }
                    this.WarningLog("Rx FIFO was read while empty");
                    return 0;
                },
                writeCallback: (_, value) => endpointsIn[0].SubmitData((uint)value));

        Registers.Fifo1.DefineMany(this, EndpointsNumber - 1, (reg, idx) =>
        {
            reg.WithValueField(0, 32, mode: FieldMode.Write, writeCallback: (_, value) =>
            {
                var ep = endpointsIn[idx + 1];
                ep.SubmitData((uint)value);
            });
        }, stepInBytes: 0x1000);
    }

    private void CheckForcedMode()
    {
        if(forceDeviceModeFlag.Value && forceHostModeFlag.Value)
        {
            this.WarningLog("Both force device and force host flags are set");
            return;
        }
        if(forceHostModeFlag.Value)
        {
            this.WarningLog("Host direction unsupported");
            return;
        }
    }

    private void UpdateInterrupts()
    {
        var interrupt = false;
        if(globalInterruptMaskFlag.Value)
        {
            var otgInterrupt = otgSessionEndFlag.Value;
            interrupt |= otgInterrupt && otgInterruptMaskFlag.Value;
            interrupt |= rxFifoPackets.Count > 0 && rxFifoNotEmptyInterruptFlag.Value;
            interrupt |= resetInterruptFlag.Value && resetInterruptMaskFlag.Value;
            interrupt |= enumerationFinishedInterruptFlag.Value && enumerationFinishedInterruptMaskFlag.Value;
            interrupt |= connectedInterruptFlag.Value && connectedInterruptMaskFlag.Value;
            interrupt |= InEndpoinsInterruptActive && allEpInInterruptMaskFlag.Value;
            interrupt |= OutEndpointsInterruptActive && allEpOutInterruptMaskFlag.Value;
        }
        if(IRQ.IsSet != interrupt)
        {
            this.DebugLog("IRQ: {0}", interrupt);
        }
        IRQ.Set(interrupt);
    }

    private void EnqueueHostReset()
    {
        if(softDisconnectedFlag.Value)
        {
            hostResetPending = true;
            return;
        }
        HostReset();
    }

    private void HostReset()
    {
        this.NoisyLog("Resetting USB bus");
        if(vbusSensingEnableFlag.Value)
        {
            connectedInterruptFlag.Value = true;
        }
        resetInterruptFlag.Value = true;
        enumerationFinishedInterruptFlag.Value = true;
        UpdateInterrupts();
    }

    private bool InEndpoinsInterruptActive => epInInterruptMaskFlags
        .Where((mask, idx) => mask.Value && endpointsIn[idx].MaskMatches(allMaskIn))
        .Any();

    private bool OutEndpointsInterruptActive => epOutInterruptMaskFlags
        .Where((mask, idx) => mask.Value && endpointsOut[idx].MaskMatches(allMaskOut))
        .Any();

    private IValueRegisterField addressField;

    private IFlagRegisterField globalInterruptMaskFlag;
    private IFlagRegisterField forceDeviceModeFlag;
    private IFlagRegisterField forceHostModeFlag;
    private IFlagRegisterField softDisconnectedFlag;

    private IFlagRegisterField otgSessionEndFlag;

    private IFlagRegisterField resetInterruptFlag;
    private IFlagRegisterField enumerationFinishedInterruptFlag;
    private IFlagRegisterField connectedInterruptFlag;

    private IFlagRegisterField otgInterruptMaskFlag;
    private IFlagRegisterField rxFifoNotEmptyInterruptFlag;
    private IFlagRegisterField resetInterruptMaskFlag;
    private IFlagRegisterField enumerationFinishedInterruptMaskFlag;
    private IFlagRegisterField connectedInterruptMaskFlag;
    private IFlagRegisterField allEpInInterruptMaskFlag;
    private IFlagRegisterField allEpOutInterruptMaskFlag;

    private IValueRegisterField txFifoFlushIndexField;

    private IFlagRegisterField[] epInInterruptMaskFlags;
    private IFlagRegisterField[] epOutInterruptMaskFlags;

    private IFlagRegisterField vbusSensingEnableFlag;

    private bool hostResetPending;

    private readonly EndpointInterruptMask allMaskIn;
    private readonly EndpointInterruptMask allMaskOut;

    private readonly IValueRegisterField[] txFifoAddressFields = new IValueRegisterField[EndpointsNumber];
    private readonly IValueRegisterField[] txFifoDepthFields = new IValueRegisterField[EndpointsNumber];

    private readonly EndpointIn[] endpointsIn = new EndpointIn[EndpointsNumber];
    private readonly EndpointOut[] endpointsOut = new EndpointOut[EndpointsNumber];

    private readonly ConcurrentQueue<RxPacket> rxFifoPackets = new();
    private readonly Queue<uint> rxFifo = new();

    private const int EndpointsNumber = 9;
    private const int MaxEndpointsNumber = 16;
    private const uint FifoSize = 0x1000;

    private const uint AllTxFifosFlushIndex = 16;

    private class EndpointSetupWrapper : IUSBPipeSetup
    {
        public EndpointSetupWrapper(STM_USB parent, EndpointIn epIn, EndpointOut epOut, byte endpointNumber)
        {
            this.parent = parent;
            this.epIn = epIn;
            this.epOut = epOut;
            this.endpointNumber = endpointNumber;
        }

        public void SetupPacketWrite(SetupPacket packet)
        {
            if(!CheckIfSetup())
            {
                return;
            }
            epOut.SetupPacketWrite(packet);
        }

        public bool CheckIfSetup()
        {
            if(epIn.EndpointTransferTypeField.Value != EndpointTransferType.Control
                || epOut.EndpointTransferTypeField.Value != EndpointTransferType.Control)
            {
                parent.WarningLog("Host tried to access EP #{0} as setup when endpoints weren't configured as setup", endpointNumber);
                return false;
            }
            return true;
        }

        public bool TryRead(out byte[] data) => epIn.TryRead(out data);

        public void Write(byte[] data) => epOut.Write(data);

        public event Action NewPacket
        {
            add => epIn.NewPacket += value;
            remove => epIn.NewPacket -= value;
        }

        private readonly byte endpointNumber;
        private readonly EndpointIn epIn;
        private readonly EndpointOut epOut;
        private readonly STM_USB parent;
    }

    private class USBConnection : IUSBConnection, IDisposable
    {
        public USBConnection(STM_USB parent)
        {
            this.parent = parent;
            parent.EnqueueHostReset();
        }

        public void Dispose()
        {
            if(parent.vbusSensingEnableFlag.Value)
            {
                parent.otgSessionEndFlag.Value = true;
                parent.UpdateInterrupts();
            }
        }

        public IUSBPipeRead ConnectEndpointRead(byte endpoint)
        {
            if(endpoint >= EndpointsNumber)
            {
                parent.WarningLog("Host tried to access non-existent EP #{0}", endpoint);
                return null;
            }
            return parent.endpointsIn[endpoint];
        }

        public IUSBPipeWrite ConnectEndpointWrite(byte endpoint)
        {
            if(endpoint >= EndpointsNumber)
            {
                parent.WarningLog("Host tried to access non-existent EP #{0}", endpoint);
                return null;
            }
            return parent.endpointsOut[endpoint];
        }

        public IUSBPipeSetup ConnectEndpointSetup(byte endpoint)
        {
            if(endpoint >= EndpointsNumber)
            {
                parent.WarningLog("Host tried to access non-existent EP #{0}", endpoint);
                return null;
            }
            var ep = new EndpointSetupWrapper(parent, parent.endpointsIn[endpoint], parent.endpointsOut[endpoint], endpoint);
            if(!ep.CheckIfSetup())
            {
                return null;
            }
            return ep;
        }

        private readonly STM_USB parent;
    }

    // This is a separate struct to aid future implementation of Ep-Each interrupt flow
    private class EndpointInterruptMask
    {
        public EndpointInterruptMask(STM_USB parent, bool isIn)
        {
            Register = new DoubleWordRegister(parent);
            DefineRegisters(parent, isIn);
        }

        public DoubleWordRegister Register { get; }

        public bool TransferComplete { get; private set; }

        public bool EndpointDisabled { get; private set; }

        public bool SetupPhaseDone { get; private set; }

        private void DefineRegisters(STM_USB parent, bool isIn)
        {
            Register
                .WithFlag(0, writeCallback: (_, value) => TransferComplete = value, name: "Transfer complete (XFRCM)")
                .WithFlag(1, writeCallback: (_, value) => EndpointDisabled = value, name: "Endpoint disabled (EPDM)")
                .WithTaggedFlag("AHB error (AHBERRM)", 2)
                .If(isIn)
                    .Then(reg => reg.WithTaggedFlag("Timeout condition (TOM)", 3))
                    .Else(reg => reg.WithFlag(3, writeCallback: (_, value) => SetupPhaseDone = value, name: "Setup phase done (STUPM)"))
                .WithTaggedFlag("IN token received when Tx Fifo empty/OUT token received when endpoint disabled (ITTXFEMSK/OTEPDM)", 4)
                .WithTaggedFlag("IN token received with EP mismatch/Status phase received for control write (INEPNMM/STSPHSRXM)", 5)
                .WithTaggedFlag("IN endpoint NAK effective/Back-to-back SETUP packets received (INEPNEM/B2BSTUPM)", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("OUT packet error/FIFO underrun (OUTPKTERRM/TXFURM)", 8)
                .WithTaggedFlag("Buffer not available (BNAM)", 9)
                .WithReservedBits(10, 2)
                .If(isIn)
                    .Then(reg => reg.WithReservedBits(12, 1))
                    .Else(reg => reg.WithTaggedFlag("Babble error (BERRM)", 12))
                .WithTaggedFlag("Negative acknowledge (NAK)", 13)
                .If(isIn)
                    .Then(reg => reg.WithReservedBits(14, 1))
                    .Else(reg => reg.WithTaggedFlag("Not yet (NYET)", 14))
                .WithReservedBits(15, 17)
                .WithChangeCallback((_, __) => parent.UpdateInterrupts());
        }
    }

    private struct RxPacket
    {
        public bool IsSetup;
        public byte Endpoint;
        public byte[] Data;

        public override string ToString() => $"IsSetup = {IsSetup}, Endpoint={Endpoint}, Data={Misc.PrettyPrintCollectionHex(Data)}";

        public RxPacketStatus Status => IsSetup ? RxPacketStatus.SetupDataPacketReceived : RxPacketStatus.OutDataPacketReceived;
    }

    private enum EnumeratedSpeed
    {
        HighSpeedHSPhy = 0,
        FullSpeedHSPhy = 1,
        FullSpeedFSPhy = 2
    }

    private enum RxPacketStatus
    {
        GlobalOutNak = 1,
        OutDataPacketReceived = 2,
        OutTransferCompleted = 3,
        SetupTransactionCompleted = 4,
        SetupDataPacketReceived = 6
    }

    private enum Registers
    {
        ControlAndStatus = 0x0,
        OtgInterrupt = 0x4,
        AHBConfiguration = 0x8,
        USBConfiguration = 0xC,
        Reset = 0x10,
        CoreInterrupt = 0x14,
        CoreInterruptMask = 0x18,
        RxFifoStatusDebug = 0x1C,
        RxFifoStatusPop = 0x20,
        RxFifoDepth = 0x24,
        EndpointTxFifo0 = 0x28, // Has different meaning in host mode
        // 0x2C is host-only
        GeneralCoreConfiguration = 0x38,
        CoreID = 0x3C,
        Undocumented_SynopsisID = 0x40, // Called gSNPSiD in STM32Cube
        CoreLPMConfiguration = 0x54,
        // 0x100 is host-only
        DeviceEndpointTxFifo1 = 0x104,
        DeviceEndpointTxFifo2 = 0x108,
        DeviceEndpointTxFifo3 = 0x10C,
        DeviceEndpointTxFifo4 = 0x110,
        DeviceEndpointTxFifo5 = 0x114,
        DeviceEndpointTxFifo6 = 0x118,
        DeviceEndpointTxFifo7 = 0x11C,
        DeviceEndpointTxFifo8 = 0x120,
        // 0x400-0x800 is host-only
        DeviceConfiguration = 0x800,
        DeviceControl = 0x804,
        DeviceStatus = 0x808,
        DeviceEpCommonInInterruptMask = 0x810,
        DeviceEpCommonOutInterruptMask = 0x814,
        DeviceEpAllInterrupt = 0x818,
        DeviceEpAllInterruptMask = 0x81C,
        DeviceVbusDischargeTime = 0x828,
        DeviceVbusPulsingTime = 0x82C,
        DeviceThresholdControl = 0x830,
        DeviceEpInFifoEmptyInterruptMask = 0x834,

        // The Ep-Each interrupt mechanism, unimplemented for now
        DeviceEpEachInterrupt = 0x838,
        DeviceEpEachInterruptMask = 0x83C,
        DeviceEp1InInteruptMask = 0x844,
        DeviceEp1OutInteruptMask = 0x884,

        DeviceEpInRegisters = 0x900,
        DeviceEpOutRegisters = 0xB00,

        PowerAndClockGatingControl = 0xE00,

        Fifo0 = 0x1000,
        Fifo1 = 0x2000,
        Fifo2 = 0x3000,
        Fifo3 = 0x4000,
        Fifo4 = 0x5000,
        Fifo5 = 0x6000,
        Fifo6 = 0x7000,
        Fifo7 = 0x8000,
        Fifo8 = 0x9000,
    }
}
