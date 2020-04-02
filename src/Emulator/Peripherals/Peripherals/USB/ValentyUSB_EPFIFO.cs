//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public class ValentyUSB : BasicDoubleWordPeripheral, IUSBDevice, IKnownSize
    {
        public ValentyUSB(Machine machine, int maximumPacketSize = 64) : base(machine)
        {
            maxPacketSize = maximumPacketSize;
            USBCore = new USBDeviceCore(this);

            USBCore.ControlEndpoint.SetupPacketHandler = SetupPacketHandler;
            USBCore.ControlEndpoint.InPacketHandler = InPacketHandler;
            USBCore.ControlEndpoint.OutPacketHandler = OutPacketHandler;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            masterToSlaveBuffer.Clear();
            slaveToMasterBuffer.Clear();
        }

        public long Size => 0x100;

        public USBDeviceCore USBCore { get; }

        public GPIO IRQ { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Endpoint0OutEventPending.Define(this)
                .WithFlag(0, out endpoint0OutErrorPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "error")
                .WithFlag(1, out endpoint0OutPacketPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "packet")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Endpoint0OutEventEnable.Define(this)
                .WithFlag(0, out endpoint0OutErrorEventEnabled, name: "error")
                .WithFlag(1, out endpoint0OutPacketEventEnabled, name: "packet")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Endpoint0InEventPending.Define(this)
                .WithFlag(0, out endpoint0InErrorPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "error")
                .WithFlag(1, out endpoint0InPacketPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "packet")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Endpoint0InEventEnable.Define(this)
                .WithFlag(0, out endpoint0InErrorEventEnabled, name: "error")
                .WithFlag(1, out endpoint0InPacketEventEnabled, name: "packet")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Endpoint0LastTokenRead.Define(this)
                .WithEnumField<DoubleWordRegister, USBTokenType>(0, 2, out lastTokenField, FieldMode.Read)
                .WithReservedBits(2, 30);
            ;

            Registers.Endpoint0OutBufferEmpty.Define(this)
                .WithFlag(0, FieldMode.Read, name: "bufferEmpty", valueProviderCallback: _ => !masterToSlaveBuffer.Any())
                .WithReservedBits(1, 31)
            ;

            Registers.Endpoint0OutBufferHead.Define(this)
                .WithValueField(0, 8, name: "bufferHeadByte",
                    // this buffer works in a special way -
                    // in order to move to the next item
                    // software must execute 'write' operation
                    valueProviderCallback: _ =>
                    {
                        if(masterToSlaveBuffer.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty queue");
                            return 0u;
                        }

                        var result = masterToSlaveBuffer.Peek();
                        this.Log(LogLevel.Noisy, "Reading byte from out buffer: 0x{0:X}. Bytes left: {1}", result, masterToSlaveBuffer.Count);
                        return result;
                    },
                    writeCallback: (_, __) => masterToSlaveBuffer.TryDequeue(out var _))
                .WithReservedBits(8, 24)
            ;

            Registers.Endpoint0OutRespond.Define(this)
                .WithEnumField<DoubleWordRegister, USBResponse>(0, 2, out endpoint0OutRespond, writeCallback: (_, v) =>
                {
                    lock(outLock)
                    {
                        this.Log(LogLevel.Noisy, "Endpoint 0 OUT response set to: {0}", v);
                        switch(v)
                        {
                        case USBResponse.Stall:
                            TrySendOutStall();
                            break;

                        case USBResponse.Ack:
                            TryAcceptSetupOrOut();
                            break;

                        case USBResponse.NotAck:
                            // intentionally do nothing
                            break;

                        default:
                            this.Log(LogLevel.Warning, "Unexpected endpoint 0 OUT response: {0}. Expect problems", v);
                            break;
                        }
                    }
                })
                .WithReservedBits(2, 30)
            ;

            Registers.Endpoint0InRespond.Define(this)
                .WithEnumField<DoubleWordRegister, USBResponse>(0, 2, out endpoint0InRespond, writeCallback: (_, v) =>
                {
                    this.Log(LogLevel.Noisy, "Endpoint 0 IN response set to: {0}", v);
                    switch(v)
                    {
                    case USBResponse.Stall:
                        TrySendInStall();
                        break;

                    case USBResponse.Ack:
                        TrySendData();
                        break;

                    case USBResponse.NotAck:
                        // intentionally do nothing
                        break;

                    default:
                        this.Log(LogLevel.Warning, "Unexpected endpoint 0 IN response: {0}. Expect problems", v);
                        break;
                    }
                })
                .WithReservedBits(2, 30)
            ;

            Registers.Endpoint0InBufferEmpty.Define(this)
                .WithFlag(0, FieldMode.Read, name: "bufferEmpty", valueProviderCallback: _ => slaveToMasterBuffer.Count == 0)
                .WithReservedBits(1, 31)
            ;

            Registers.Endpoint0InBufferHead.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "bufferHeadByte",
                        writeCallback: (_, b) => slaveToMasterBuffer.Enqueue((byte)b))
                .WithReservedBits(8, 24)
            ;

            Registers.Endpoint0InDataToggleBit.Define(this)
                // TODO: implement it properely!
                .WithFlag(0, name: "Data Toggle Bit", valueProviderCallback: _ => false)
                .WithReservedBits(1, 31)
            ;

            Registers.Address.Define(this)
                .WithValueField(0, 8, name: "USBAddress",
                    writeCallback: (_, val) => { USBCore.Address = (byte)val; },
                    valueProviderCallback: _ => USBCore.Address)
                .WithReservedBits(8, 24)
            ;
        }

        private void EnqueueDataFromMaster(IEnumerable<byte> data)
        {
            masterToSlaveBuffer.EnqueueRange(data);

            // fake 16-bit CRC
            masterToSlaveBuffer.Enqueue(0);
            masterToSlaveBuffer.Enqueue(0);

            endpoint0OutPacketPending.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var irqState = (endpoint0OutPacketPending.Value && endpoint0OutPacketEventEnabled.Value)
                || (endpoint0OutErrorPending.Value && endpoint0OutErrorEventEnabled.Value)
                || (endpoint0InPacketPending.Value && endpoint0InPacketEventEnabled.Value)
                || (endpoint0InErrorPending.Value && endpoint0InErrorEventEnabled.Value);

            IRQ.Set(irqState);
        }

        private void SetupPacketHandler(USBEndpoint endpoint, USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            this.Log(LogLevel.Noisy, "Received setup packet");
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Setup packet bytes: [{0}]", Misc.PrettyPrintCollectionHex(hostStage.Data));
#endif

            if(masterToSlaveBuffer.Count != 0)
            {
                this.Log(LogLevel.Warning, "Received SETUP packet, but some stale bytes from the master are still in the buffer - removing them, but this might indicate a problem");
#if DEBUG_PACKETS
                this.Log(LogLevel.Noisy, "Master to slave buffer content: [{0}]", Misc.PrettyPrintCollectionHex(masterToSlaveBuffer));
#endif
                masterToSlaveBuffer.Clear();
            }

            lock(outLock)
            {
                // SETUP packets share the same fifo as OUT packets
                if(outPacketResponse != null)
                {
                    this.Log(LogLevel.Warning, "Received SETUP packet, but the response callback is not empty - this might indicate problems!");
                }

                if(endpoint0OutRespond.Value == USBResponse.Stall)
                {
                    endpoint0OutRespond.Value = USBResponse.Ack;
                }

                lastOutPacket = hostStage;
                outPacketResponse = deviceStage;

                if(endpoint0OutRespond.Value == USBResponse.Ack)
                {
                    TryAcceptSetupOrOut();
                }
            }
        }

        private void OutPacketHandler(USBEndpoint endpoint, USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            lock(outLock)
            {
                if(outPacketResponse != null)
                {
                    this.Log(LogLevel.Warning, "Received OUT packet, but the response callback is not empty - this might indicate problems!");
                }

                lastOutPacket = hostStage;
                outPacketResponse = deviceStage;

                switch(endpoint0OutRespond.Value)
                {
                case USBResponse.Stall:
                    TrySendOutStall();
                    break;

                case USBResponse.Ack:
                    TryAcceptSetupOrOut();
                    break;
                }
            }
        }

        private bool TrySendOutStall()
        {
            lock(outLock)
            {
                if(outPacketResponse == null)
                {
                    return false;
                }

                this.Log(LogLevel.Noisy, "Sending OUT-STALL handshake on endpoint #0");

                var callback = outPacketResponse;
                outPacketResponse = null;

                callback(USBTransactionStage.Stall());

                return true;
            }
        }

        private bool TrySendInStall()
        {
            lock(inLock)
            {
                if(inPacketResponse == null)
                {
                    return false;
                }

                this.Log(LogLevel.Noisy, "Sending IN-STALL handshake on endpoint #0");

                endpoint0InRespond.Value = USBResponse.NotAck;

                var callback = inPacketResponse;
                inPacketResponse = null;

                callback(USBTransactionStage.Stall());

                return true;
            }
        }

        private bool TryAcceptSetupOrOut()
        {
            lock(outLock)
            {
                if(outPacketResponse == null)
                {
                    return false;
                }

                this.Log(LogLevel.Noisy, "Accepting SETUP/OUT data from the host");
                switch(lastOutPacket.PacketID)
                {
                    case USBPacketId.SetupToken:
#if DEBUG_PACKETS
                        this.Log(LogLevel.Noisy, "Setup data: {0}", Misc.PrettyPrintCollectionHex(((USBSetupTransaction)lastOutPacket).Payload));
#endif
                        lastTokenField.Value = USBTokenType.Setup;
                        EnqueueDataFromMaster(lastOutPacket.Payload);
                        break;

                    case USBPacketId.OutToken:
#if DEBUG_PACKETS
                        this.Log(LogLevel.Noisy, "Data from host: {0}", Misc.PrettyPrintCollectionHex(((USBOutTransaction)lastOutPacket).Payload));
#endif
                        lastTokenField.Value = USBTokenType.Out;
                        EnqueueDataFromMaster(lastOutPacket.Payload);
                        break;

                    default:
                        this.Log(LogLevel.Error, "Unexpected packet type {0}", lastOutPacket.PacketID);
                        return false;
                }

                endpoint0OutRespond.Value = USBResponse.NotAck;

                var callback = outPacketResponse;
                outPacketResponse = null;

                callback(USBTransactionStage.Ack());

                return true;
            }
        }

        private bool TrySendData()
        {
            lock(inLock)
            {
                if(inPacketResponse == null)
                {
                    return false;
                }

                var response = USBTransactionStage.Data(slaveToMasterBuffer.DequeueAll());
                this.Log(LogLevel.Noisy, "Sending response to IN transaction of size {0} bytes", response.Payload.Length);
    #if DEBUG_PACKETS
                this.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(response.Payload));
    #endif

                endpoint0InRespond.Value = USBResponse.NotAck;

                var callback = inPacketResponse;
                inPacketResponse = null;
                // this is to avoid warnings TODO: expoand on it
                callback(response);

                return true;
            }
        }

        private void InPacketHandler(USBEndpoint endpoint, USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            lock(inLock)
            {
                if(inPacketResponse != null)
                {
                    this.Log(LogLevel.Warning, "It looks like handling the previous IN packet is still in progress - it means problems");
                }

                inPacketResponse = deviceStage;

                switch(endpoint0InRespond.Value)
                {
                    case USBResponse.Stall:
                        TrySendInStall();
                        break;

                    case USBResponse.Ack:
                        TrySendData();
                        break;
                }
            }
        }

        private IFlagRegisterField endpoint0OutPacketPending;
        private IFlagRegisterField endpoint0InPacketPending;
        private IFlagRegisterField endpoint0InPacketEventEnabled;
        private IFlagRegisterField endpoint0InErrorEventEnabled;
        private IFlagRegisterField endpoint0InErrorPending;
        private IFlagRegisterField endpoint0OutPacketEventEnabled;
        private IFlagRegisterField endpoint0OutErrorEventEnabled;
        private IFlagRegisterField endpoint0OutErrorPending;
        private IEnumRegisterField<USBResponse> endpoint0OutRespond;
        private IEnumRegisterField<USBResponse> endpoint0InRespond;
        private IEnumRegisterField<USBTokenType> lastTokenField;

        private USBTransactionStage lastOutPacket;
        private Action<USBTransactionStage> outPacketResponse;
        private Action<USBTransactionStage> inPacketResponse;

        private readonly int maxPacketSize;
        private readonly Queue<byte> masterToSlaveBuffer = new Queue<byte>();
        private readonly Queue<byte> slaveToMasterBuffer = new Queue<byte>();
        private readonly object outLock = new object();
        private readonly object inLock = new object();

        private enum USBResponse
        {
            Ack,
            NotAck,
            None,
            Stall
        }

        private enum USBTokenType
        {
            Out = 0,
            StartOfFrame = 1,
            In = 2,
            Setup = 3
        }

        private enum Registers
        {
            PullupOut = 0x0,

            Endpoint0OutEventStatus = 0x4,
            Endpoint0OutEventPending = 0x08,
            Endpoint0OutEventEnable = 0x0C,
            Endpoint0LastTokenRead = 0x10,
            Endpoint0OutRespond = 0x14,
            Endpoint0OutDataToggleBit = 0x18,
            Endpoint0OutBufferHead = 0x1C,
            Endpoint0OutBufferEmpty = 0x20,

            Endpoint0InEventStatus = 0x24,
            Endpoint0InEventPending = 0x28,
            Endpoint0InEventEnable = 0x2C,
            Endpoint0InLastToken = 0x30,
            Endpoint0InRespond = 0x34,
            Endpoint0InDataToggleBit = 0x38,
            Endpoint0InBufferHead = 0x3C,
            Endpoint0InBufferEmpty = 0x40,

            Endpoint1InEventStatus = 0x44,
            Endpoint1InEventPending = 0x48,
            Endpoint1InEventEnable = 0x4C,
            Endpoint1InLastToken = 0x50,
            Endpoint1InRespond = 0x54,
            Endpoint1InDataToggleBit = 0x58,
            Endpoint1InBufferHead = 0x5C,
            Endpoint1InBufferEmpty = 0x60,

            Endpoint2OutEventStatus = 0x64,
            Endpoint2OutEventPending = 0x68,
            Endpoint2OutEventEnable = 0x6C,
            Endpoint2OutLastToken = 0x70,
            Endpoint2OutRespond = 0x74,
            Endpoint2OutDataToggleBit = 0x78,
            Endpoint2OutBufferHead = 0x7C,
            Endpoint2OutBufferEmpty = 0x80,

            Endpoint2InEventStatus = 0x84,
            Endpoint2InEventPending = 0x88,
            Endpoint2InEventEnable = 0x8C,
            Endpoint2InLastToken = 0x90,
            Endpoint2InRespond = 0x94,
            Endpoint2InDataToggleBit = 0x98,
            Endpoint2InBufferHead = 0x9C,
            Endpoint2InBufferEmpty = 0xA0,

            Address = 0xA4
        }
    }
}
