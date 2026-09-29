//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#nullable enable
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB;

public class STM32_USB : BasicWordPeripheral, IUSBDevice, IKnownSize
{
    public STM32_USB(IMachine machine, ArrayMemory usbSRAM) : base(machine)
    {
        interruptMaskFlags = new IFlagRegisterField[NumberOfInterrupts];
        interruptStatusFlags = new IFlagRegisterField[NumberOfInterrupts];
        endpoints = new Endpoint[NumberOfEndpoints];
        this.usbSRAM = usbSRAM;
        for(uint i = 0; i < NumberOfEndpoints; i++)
        {
            endpoints[i] = new Endpoint(this, i);
        }

        // Common registers, endpoint registers are defined in the Endpoint constructor
        Registers.Control.Define(this, 0x3)
            .WithFlag(15, out correctTransferInterruptMask, name: "CTRM")
            .WithFlags(7, NumberOfInterrupts, out interruptMaskFlags)
            .WithReservedBits(6, 1)
            .WithTaggedFlag("L1Resume", 5)
            .WithTaggedFlag("ResumeRequest", 4)
            .WithTaggedFlag("ForceSuspend", 3)
            .WithTaggedFlag("Low Power Mode", 2)
            .WithFlag(1, name: "PDWN") // Not implemented, but HAL expects it to keep its value
            .WithFlag(0, name: "FRES", changeCallback: (_, val) =>
            {
                if(val)
                {
                    USBReset();
                }
            })
            .WithChangeCallback((_, _) => UpdateInterrupts());
        Registers.InterruptStatus.Define(this)
            // CTR is handlered seperatly from the other interrupts as it is a combined value from the endpoints
            .WithFlag(15, mode: FieldMode.Read, name: "CTR",
            valueProviderCallback: (_) =>
            {
                return endpoints.Where((endpoint) => endpoint.CorrectTransferRX || endpoint.CorrectTransferTX).Any();
            })
            .WithFlags(7, NumberOfInterrupts, out interruptStatusFlags, mode: FieldMode.Read | FieldMode.WriteZeroToClear)
            .WithReservedBits(5, 2)
            .WithFlag(4, out interruptDirection, mode: FieldMode.Read, name: "DIR")
            .WithValueField(0, 3, out interruptEndpointId, mode: FieldMode.Read, name: "EP_ID")
            .WithChangeCallback((_, _) => UpdateInterrupts());
        Registers.DeviceAddress.Define(this)
            .WithReservedBits(8, 8)
            .WithFlag(7, name: "EnableFunction")
            .WithValueField(0, 7, out deviceAddress, name: "ADD");
        Registers.BufferTableAddress.Define(this)
            .WithValueField(3, 13, out rawBufferTableAddress, name: "BTABLE")
            .WithReservedBits(0, 3);
        Registers.BatteryChargingDetector.Define(this)
            .WithFlag(15, name: "DPPU") // Not implemented, but HAL expects it to keep its value
            .WithReservedBits(8, 7)
            .WithTaggedFlag("PS2DET", 7)
            .WithTaggedFlag("SDET", 6)
            .WithTaggedFlag("PDET", 5)
            .WithTaggedFlag("DCDET", 4)
            .WithTaggedFlag("SDEN", 3)
            .WithTaggedFlag("PDEN", 2)
            .WithTaggedFlag("DCDEN", 1)
            .WithTaggedFlag("BCDEN", 0);
    }

    public override void Reset()
    {
        base.Reset();
        foreach(var endpoint in endpoints)
        {
            endpoint.Reset();
        }
        UpdateInterrupts();
    }

    public IUSBConnection ConnectUSB() => new USBConnection(this);

    public byte Address => (byte)deviceAddress.Value;

    public long Size => 0x400;

    public GPIO IRQ { get; } = new GPIO();

    private void UpdateInterrupts()
    {
        var irq = false;
        // Calculate highest priority pending endpoint interrupt (if any)
        // Double buffered and Isochronous endpoints are higher priority than other endpoints
        // Endpoint in the same set are prioritised in order of lowest to highest (so endpoint 0 is higher priority than endpoint 3)
        // Since neither double buffering or isochronous endpoints are implemented, sort only by endpoint number
        for(var i = 0; i < NumberOfEndpoints; i++)
        {
            var endpoint = endpoints[i];
            if(endpoint.CorrectTransferRX || endpoint.CorrectTransferTX)
            {
                interruptEndpointId.Value = (uint)i;
                interruptDirection.Value = endpoint.CorrectTransferRX;
                irq |= correctTransferInterruptMask.Value;
                break;
            }
        }
        irq |= interruptMaskFlags.Zip(interruptStatusFlags).Where((x, _) =>
        {
            var (a, b) = x;
            return a.Value && b.Value;
        }).Any();
        if(IRQ.IsSet != irq)
        {
            this.DebugLog("Setting IRQ to {0}", irq);
        }
        IRQ.Set(irq);
    }

    private void USBReset()
    {
        this.DebugLog("USBReset");
        interruptStatusFlags[(int)Interrupts.USBResetRequest].Value = true;
        foreach(var endpoint in endpoints)
        {
            endpoint.Reset();
        }
        UpdateInterrupts();
    }

    private BufferTableEntry GetBufferTableEntry(uint endpoint)
    {
        if(endpoint >= NumberOfEndpoints)
        {
            throw new RecoverableException($"Attempted to get buffer table entry for endpoint #{endpoint}, maximum {NumberOfEndpoints - 1}");
        }
        var entryLength = Packet.CalculateLength<BufferTableEntry>();
        var rawEntry = usbSRAM.ReadBytes(this.ReadWord((long)Registers.BufferTableAddress) + endpoint*entryLength, entryLength, this);
        var tableEntry = Packet.Decode<BufferTableEntry>(rawEntry);
        this.NoisyLog("Endpoint #{0}: buffer table entry {1}", endpoint, tableEntry);
        return tableEntry;
    }

    // The buffer table address has 3 read only zeroes at the bottom of the register
    private ulong BufferTableAddress => rawBufferTableAddress.Value << 3;

    private readonly IFlagRegisterField correctTransferInterruptMask;
    private readonly IValueRegisterField deviceAddress;
    private readonly IValueRegisterField rawBufferTableAddress;
    private readonly IFlagRegisterField[] interruptMaskFlags;
    private readonly IFlagRegisterField[] interruptStatusFlags;
    private readonly Endpoint[] endpoints;
    private readonly ArrayMemory usbSRAM;
    private readonly IFlagRegisterField interruptDirection;
    private readonly IValueRegisterField interruptEndpointId;
    private const int NumberOfInterrupts = 8;
    private const int NumberOfEndpoints = 8;

    private class Endpoint : IUSBPipeSetup, IUSBPipeRead, IUSBPipeWrite
    {
        public Endpoint(STM32_USB parent, uint endpointNumber)
        {
            this.parent = parent;
            this.endpointNumber = endpointNumber;
            endpointRegister = parent.RegistersCollection.DefineRegister(endpointNumber * 0x4)
                .WithFlag(15, out correctTransferRX, mode: FieldMode.Read | FieldMode.WriteZeroToClear, changeCallback: (_, _) => parent.UpdateInterrupts(), name: "CTR_RX")
                .WithFlag(14, out dataToggleRX, mode: FieldMode.Read | FieldMode.WriteOneToToggle, name: "DTOG_RX")
                .WithEnumField(12, 2, out statusRX, mode: FieldMode.Read | FieldMode.WriteOneToToggle, name: "STAT_RX",
                changeCallback: (oldValue, value) =>
                {
                    parent.NoisyLog("Endpoint #{0}: changing reception status from {1} to {2}", endpointNumber, oldValue, value);
                })
                .WithFlag(11, out setupComplete, mode: FieldMode.Read, name: "SETUP")
                .WithEnumField(9, 2, out endpointType, name: "EP_TYPE")
                .WithFlag(8, name: "EP_KIND")
                .WithFlag(7, out correctTransferTX, mode: FieldMode.Read | FieldMode.WriteZeroToClear, changeCallback: (_, _) => parent.UpdateInterrupts(), name: "CTR_TX")
                .WithFlag(6, out dataToggleTX, mode: FieldMode.Read | FieldMode.WriteOneToToggle, name: "DTOG_TX")
                .WithEnumField(4, 2, out statusTX, mode: FieldMode.Read | FieldMode.WriteOneToToggle, name: "STAT_TX",
                changeCallback: (oldValue, value) =>
                {
                    parent.NoisyLog("Endpoint #{0}: changing transmission status from {1} to {2}", endpointNumber, oldValue, value);
                    // When the status switches to VALID (as part of the interrupt servicing or setup) we need to trigger a new packet event
                    if(value == TransmissionStatus.Valid)
                    {
                        parent.NoisyLog("Enpoint #{0}: Invoking NewPacket", endpointNumber);
                        NewPacket?.Invoke();
                    }
                })
                .WithValueField(0, 3, out endpointAddress, name: "EA")
                ;
        }

        public void Reset()
        {
            endpointRegister.Reset();
        }

        public void SetupPacketWrite(SetupPacket packet)
        {
            if(statusRX.Value == TransmissionStatus.Disabled)
            {
                parent.WarningLog("Endpoint #{0}: Host tried to write setup packet to disabled endpoint. Packet: {2}", endpointNumber, packet);
                return;
            }
            // Normally NAK and STALL has no effect on SETUP packets, but if CTR_RX is still set from a previous transaction, it will not accept the packet
            if((statusRX.Value == TransmissionStatus.Nak || statusRX.Value == TransmissionStatus.Stall) && CorrectTransferRX)
            {
                parent.WarningLog("Endpoint #{0}: Host wrote setup packet to {1} endpoint, while CTR_RX was still set", endpointNumber, statusRX.Value);
                return;
            }
            if(endpointType.Value != EndpointType.Control)
            {
                parent.WarningLog("Endpoint #{0}: Host tried to write setup packet to non-control endpoint ({1})", endpointNumber, endpointType.Value);
                return;
            }
            parent.DebugLog("Endpoint #{0}: Writing setup packet from host {1}", endpointNumber, packet);
            var tableEntry = parent.GetBufferTableEntry(endpointNumber);
            setupComplete.Value = true;
            statusTX.Value = TransmissionStatus.Nak;
            // Setup sets DTOG_RX to 0 and DTOG_TX to 1
            dataToggleRX.Value = false;
            dataToggleTX.Value = true;
            ReceiveData(tableEntry, Packet.Encode(packet));
        }

        public bool TryRead(out byte[] data)
        {
            if(statusTX.Value != TransmissionStatus.Valid)
            {
                // Debug level as reads from non-enabled endpoints is expected
                parent.DebugLog("Endpoint #{0}: Host tried to read from non-enabled endpoint {1}", this.endpointNumber, statusTX.Value);
                data = new byte[0];
                return false;
            }
            var tableEntry = parent.GetBufferTableEntry(endpointNumber);
            parent.DebugLog("Endpoint #{0}: Reading {1} bytes from offset 0x{2:X}", endpointNumber, tableEntry.TransmissionByteCount, tableEntry.TransmissionBufferAddress);
            data = parent.usbSRAM.ReadBytes(tableEntry.TransmissionBufferAddress, tableEntry.TransmissionByteCount, parent);
            parent.NoisyLog("Endpoint #{0}: Data read {1}", endpointType, data.ToDebugString());
            var oldStatus = statusTX.Value;
            statusTX.Value = TransmissionStatus.Nak;
            statusTX.ChangeCallback?.Invoke(oldStatus, TransmissionStatus.Nak);
            correctTransferTX.Value = true;
            dataToggleTX.Value = !dataToggleTX.Value;
            parent.UpdateInterrupts();
            return true;
        }

        public void Write(byte[] data)
        {
            if(statusRX.Value != TransmissionStatus.Valid)
            {
                parent.WarningLog("Endpoint #{0}: Host tried writing to non-enabled endpoint", this.endpointNumber);
                return;
            }
            setupComplete.Value = false; // Gets cleared on non-setup write
            parent.DebugLog("Endpoint #{0}: Writing data packet from host {1}", endpointNumber, data);
            var tableEntry = parent.GetBufferTableEntry(endpointNumber);
            dataToggleRX.Value = !dataToggleRX.Value;
            ReceiveData(tableEntry, data);
        }

        public bool SetupComplete => setupComplete.Value;

        public bool CorrectTransferRX => correctTransferRX.Value;

        public bool CorrectTransferTX => correctTransferTX.Value;

        public byte EndpointAddress => (byte)endpointAddress.Value;

        public uint EndpointNumber => endpointNumber;

        public bool IsEnabled => statusRX.Value != TransmissionStatus.Disabled || statusTX.Value != TransmissionStatus.Disabled;

        public event Action? NewPacket;

        private void ReceiveData(BufferTableEntry bufferTableEntry, byte[] data)
        {
            // If the data is larger than the allocated buffer, the full buffer is written, rest of the bytes are dropped
            if(data.Length > bufferTableEntry.BufferSize)
            {
                parent.WarningLog("Endpoint #{0}: Attempting to write {1} bytes of data to a buffer of size {2}. Dropping overflow bytes", endpointNumber, data.Length, bufferTableEntry.BufferSize);
            }
            parent.usbSRAM.WriteBytes(bufferTableEntry.ReceptionBufferAddress, data, 0, (int)Math.Min(data.Length, bufferTableEntry.BufferSize));
            // Write the number of bytes written to the count field in the buffer table
            bufferTableEntry.ReceptionByteCount = (ushort)data.Length;
            var encoded = Packet.Encode(bufferTableEntry);
            parent.usbSRAM.WriteBytes(parent.ReadWord((long)Registers.BufferTableAddress) + endpointNumber * encoded.Length, encoded, 0, encoded.Length, parent);
            if(!setupComplete.Value)
            {
                var oldStatus = statusRX.Value;
                statusRX.Value = TransmissionStatus.Nak;
                statusRX.ChangeCallback.Invoke(oldStatus, TransmissionStatus.Nak);
            }
            correctTransferRX.Value = true;
            parent.UpdateInterrupts();
        }

        private readonly IFlagRegisterField setupComplete;
        private readonly IFlagRegisterField correctTransferRX;
        private readonly IEnumRegisterField<EndpointType> endpointType;
        private readonly IEnumRegisterField<TransmissionStatus> statusRX;
        private readonly IEnumRegisterField<TransmissionStatus> statusTX;
        private readonly IFlagRegisterField correctTransferTX;
        private readonly STM32_USB parent;
        private readonly uint endpointNumber;
        private readonly WordRegister endpointRegister;
        private readonly IFlagRegisterField dataToggleRX;
        private readonly IFlagRegisterField dataToggleTX;
        private readonly IValueRegisterField endpointAddress;
    }

    private class USBConnection : IUSBConnection, IDisposable
    {
        public USBConnection(STM32_USB parent)
        {
            this.parent = parent;
            parent.USBReset();
        }

        public IUSBPipeSetup? ConnectEndpointSetup(byte endpoint)
        {
            parent.DebugLog("Connecting endpoint {0} as Setup", endpoint);
            return GetEndpointByAddress(endpoint);
        }

        public IUSBPipeRead? ConnectEndpointRead(byte endpoint)
        {
            parent.DebugLog("Connecting endpoint {0} as Read", endpoint);
            return GetEndpointByAddress(endpoint);
        }

        public IUSBPipeWrite? ConnectEndpointWrite(byte endpoint)
        {
            parent.DebugLog("Connecting endpoint {0} as Write", endpoint);
            return GetEndpointByAddress(endpoint);
        }

        public void Dispose()
        {
            // Intentionally empty
        }

        private Endpoint? GetEndpointByAddress(byte address)
        {
            var matchingEndpoints = parent.endpoints.Where((endpoint) => endpoint.EndpointAddress == address && endpoint.IsEnabled);
            if(!matchingEndpoints.Any())
            {
                parent.WarningLog("Tried to connect endpoint {0}, but no enabled endpoint is configured with that address", address);
                return null;
            }
            if(matchingEndpoints.Count() > 1)
            {
                parent.WarningLog("More than one enabled endpoint configured with address {0}, connecting lowest numbered one. Matching endpoints {1}", address, matchingEndpoints.Select((endpoint) => endpoint.EndpointNumber).ToArray());
            }
            return matchingEndpoints.First();
        }

        private readonly STM32_USB parent;
    }

    [LeastSignificantByteFirst]
    private class BufferTableEntry
    {
        public override string ToString()
        {
            return Misc.ToDebugString(this);
        }

        public uint BufferSize => BlockSize ? (uint)NumberOfBlocks * 32 : (uint)NumberOfBlocks * 2;

#pragma warning disable CS0649 // Fields are assigned via `Packet.Decode()`
        [PacketField, Offset(words:0), Width(bits: 16)]
        public ushort TransmissionBufferAddress;
        [PacketField, Offset(words: 1), Width(bits: 10)]
        public ushort TransmissionByteCount;
        [PacketField, Offset(words: 2), Width(bits: 16)]
        public ushort ReceptionBufferAddress;
        [PacketField, Offset(words: 3, bits: 15), Width(bits: 1)]
        public bool BlockSize;
        [PacketField, Offset(words: 3, bits: 10), Width(bits: 5)]
        public byte NumberOfBlocks;
        [PacketField, Offset(words: 3, bits: 0), Width(bits: 10)]
        public ushort ReceptionByteCount;
#pragma warning restore CS0649
    }

    private enum TransmissionStatus
    {
        Disabled = 0b00,
        Stall = 0b01,
        Nak = 0b10,
        Valid = 0b11
    }

    private enum EndpointType
    {
        // Note: Encodings for the EP_TYPE field, not following bmAttributes order
        Bulk = 0b00,
        Control = 0b01,
        Iso = 0b10,
        Interrupt = 0b11
    }

    private enum Interrupts
    {
        LPML1StateRequest = 0,
        ExpectedStartOfFrame = 1,
        StartOfFrame = 2,
        USBResetRequest = 3,
        SuspendModeRequest = 4,
        Wakeup = 5,
        Error = 6,
        PacketMemoryOverUnderRun = 7
    }

    private enum Registers
    {
        Endpoint0 = 0x00,
        Endpoint1 = 0x04,
        Endpoint2 = 0x08,
        Endpoint3 = 0x0C,
        Endpoint4 = 0x10,
        Endpoint5 = 0x14,
        Endpoint6 = 0x18,
        Endpoint7 = 0x1C,
        Control = 0x40,
        InterruptStatus = 0x44,
        FrameNumber = 0x48,
        DeviceAddress = 0x4C,
        BufferTableAddress = 0x50,
        LinkPowerControlAndStatus = 0x54,
        BatteryChargingDetector = 0x58
    }
}