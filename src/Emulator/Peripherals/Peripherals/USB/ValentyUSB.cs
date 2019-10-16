//
// Copyright (c) 2010-2019 Antmicro
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
            USBCore = new USBDeviceCore(this, customSetupPacketHandler: SetupPacketHandler);
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            slaveToMasterBufferVirtualBase = 0;
            state = State.Idle;

            masterToSlaveBuffer.Clear();
            masterToSlaveAdditionalDataBuffer.Clear();
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
                .WithEnumField<DoubleWordRegister, USBTokenType>(0, 2, FieldMode.Read, valueProviderCallback: _ =>
                {
                    switch(state)
                    {
                    case State.SetupTokenReceived:
                    case State.SetupTokenAcked:
                        return USBTokenType.Setup;

                    case State.ReadyForDataFromMaster:
                    case State.DataFromMasterAcked:
                        return USBTokenType.Out;

                    case State.DataToMasterReady:
                        return USBTokenType.In;

                    default:
                        return USBTokenType.Out;
                    }
                })
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
                    writeCallback: (_, __) => masterToSlaveBuffer.Dequeue())
                .WithReservedBits(8, 24)
            ;

            Registers.Endpoint0OutRespond.Define(this)
                .WithEnumField<DoubleWordRegister, USBResponse>(0, 2, out endpoint0OutRespond, writeCallback: (_, v) =>
                {
                    this.Log(LogLevel.Noisy, "Endpoint 0 OUT response set to: {0} in state {1}", v, state);
                    switch(v)
                    {
                    case USBResponse.Stall:
                        state = State.Stall;
                        HandleStall();
                        break;

                    case USBResponse.Ack:
                        HandleOutAckRespond();
                        break;

                    case USBResponse.NotAck:
                        // intentionally do nothing
                        break;

                    default:
                        this.Log(LogLevel.Warning, "Unexpected endpoint 0 OUT response: {0}. Expect problems", v);
                        state = State.Error;
                        break;
                    }
                })
                .WithReservedBits(2, 30)
            ;

            Registers.Endpoint0InRespond.Define(this)
                .WithEnumField<DoubleWordRegister, USBResponse>(0, 2, out endpoint0InRespond, writeCallback: (_, v) =>
                {
                    this.Log(LogLevel.Noisy, "Endpoint 0 IN response set to: {0} in state {1}", v, state);
                    switch(v)
                    {
                    case USBResponse.Ack:
                        state = State.DataToMasterReady;
                        ProduceDataToMaster();
                        break;

                    case USBResponse.Stall:
                        state = State.Stall;
                        HandleStall();
                        break;

                    case USBResponse.NotAck:
                        // intentionally do nothing
                        break;

                    default:
                        this.Log(LogLevel.Warning, "Unexpected endpoint 0 IN response: {0}. Expect problems", v);
                        state = State.Error;
                        break;
                    }
                })
                .WithReservedBits(2, 30)
            ;

            Registers.Endpoint0InBufferEmpty.Define(this)
                .WithFlag(0, FieldMode.Read, name: "bufferEmpty", valueProviderCallback: _ => slaveToMasterBuffer.Count == slaveToMasterBufferVirtualBase)
                .WithReservedBits(1, 31)
            ;

            Registers.Endpoint0InBufferHead.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "bufferHeadByte",
                        writeCallback: (_, b) => slaveToMasterBuffer.Enqueue((byte)b))
                .WithReservedBits(8, 24)
            ;

            Registers.Endpoint0InDataToggleBit.Define(this)
                // since we don't generate separate Data0/Data1 packets in a transaction anyway, writes can be ignored
                // we must return 0, because otherwise foboot does not work...
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

        private void SendSetupPacketResponse()
        {
            if(masterToSlaveAdditionalDataBuffer.Count != 0)
            {
                this.Log(LogLevel.Error, "Setup packet handling finished, but there is still some unhandled additional data left. Dropping it, but expect problems");
                masterToSlaveAdditionalDataBuffer.Clear();
            }

            this.Log(LogLevel.Noisy, "Setup packet handled");
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Response bytes: [{0}]", Misc.PrettyPrintCollection(slaveToMasterBuffer, b => b.ToString("X")));
#endif
            slaveToMasterBufferVirtualBase = 0;

            if(setupPacketResultCallback == null)
            {
                this.Log(LogLevel.Error, "No setup packet is handled at the moment, but the software wants to send data back. It might indicate a faulty driver");
                return;
            }

            setupPacketResultCallback(slaveToMasterBuffer.DequeueAll());
            setupPacketResultCallback = null;
        }

        private void HandleStall()
        {
            this.Log(LogLevel.Debug, "Endpoint 0 stalled");

            // this could happen since HandleStall is called for both IN and OUT packets
            if(setupPacketResultCallback == null)
            {
                return;
            }

            SendSetupPacketResponse();
        }

        private void ProduceDataToMaster()
        {
            var chunkSize = slaveToMasterBuffer.Count - slaveToMasterBufferVirtualBase;
            slaveToMasterBufferVirtualBase = slaveToMasterBuffer.Count;

            if(chunkSize < maxPacketSize)
            {
                this.Log(LogLevel.Noisy, "Data chunk was shorter than max packet size (0x{0:X} vs 0x{1:X}), so this is the end of data", chunkSize, maxPacketSize);
                SendSetupPacketResponse();
            }

            // IN packet pending means that the master is waiting for more data
            // and slave should generate it
            endpoint0InPacketPending.Value = true;
            UpdateInterrupts();
        }

        private void PrepareDataFromMaster()
        {
            if(masterToSlaveAdditionalDataBuffer.Count == 0)
            {
                this.Log(LogLevel.Warning, "Asked for additional data from master, but there is no more of it");
                return;
            }

            var chunk = masterToSlaveAdditionalDataBuffer.DequeueRange(maxPacketSize);
            this.Log(LogLevel.Noisy, "Enqueuing chunk of additional data from master of size {0}", chunk.Length);
            EnqueueDataFromMaster(chunk);
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

        private void HandleOutAckRespond()
        {
            switch(state)
            {
            case State.Idle:
                // do nothing
                break;

            case State.SetupTokenReceived:
                state = State.SetupTokenAcked;
                break;

            case State.SetupTokenAcked:
            case State.DataFromMasterAcked:
                state = State.ReadyForDataFromMaster;
                PrepareDataFromMaster();
                break;

            case State.ReadyForDataFromMaster:
                state = State.DataFromMasterAcked;
                break;

            default:
                this.Log(LogLevel.Warning, "Unexpected state when handling OUT ACK response: {0}. Expect problems", state);
                state = State.Error;
                break;
            }
        }

        private void UpdateInterrupts()
        {
            var irqState = (endpoint0OutPacketPending.Value && endpoint0OutPacketEventEnabled.Value)
                || (endpoint0OutErrorPending.Value && endpoint0OutErrorEventEnabled.Value)
                || (endpoint0InPacketPending.Value && endpoint0InPacketEventEnabled.Value)
                || (endpoint0InErrorPending.Value && endpoint0InErrorEventEnabled.Value);

            IRQ.Set(irqState);
        }

        // NOTE: Here we assume that the communication is well-formed, i.e.,
        // the controller does not send two setup packets in a row (without waiting for a response),
        // or a device does not start to respond by itself (without the request from the master).
        // There are some checks verifying it and printing errors, but there is no mechanism enforcing it.
        private void SetupPacketHandler(SetupPacket packet, byte[] additionalData, Action<byte[]> resultCallback)
        {
            this.Log(LogLevel.Noisy, "Received setup packet: {0}", packet.ToString());

            if(setupPacketResultCallback != null)
            {
                this.Log(LogLevel.Error, "Setup packet result handler is set. It means that the previous setup packet handler has not yet finished. Expect problems!");
            }
            setupPacketResultCallback = resultCallback;

            slaveToMasterBuffer.Clear();
            slaveToMasterBufferVirtualBase = 0;
            state = State.SetupTokenReceived;

            var packetBytes = Packet.Encode(packet);
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Setup packet bytes: [{0}]", Misc.PrettyPrintCollection(packetBytes, b => b.ToString("X")));
#endif
            EnqueueDataFromMaster(packetBytes);

            // this is a trick:
            // in fact we don't know if the master expects any data from the slave,
            // but we can safely assume so - if there is no data, we should simply
            // receive NAK;
            // without generating this interrupt the slave would never know that
            // it should generate any response and we would be stuck
            endpoint0InPacketPending.Value = true;
            UpdateInterrupts();

            if(additionalData != null)
            {
                masterToSlaveAdditionalDataBuffer.EnqueueRange(additionalData);
            }
        }

        private Action<byte[]> setupPacketResultCallback;

        private State state;
        // in order to avoid copying data from `slaveToMasterBuffer`
        // into another buffer it's not cleared after generating ACK
        // packet; in order to detect how much data has been added to
        // the buffer this variable contains the length of the buffer
        // before the previous ACK packet
        private int slaveToMasterBufferVirtualBase;

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

        private readonly int maxPacketSize;
        private readonly Queue<byte> masterToSlaveBuffer = new Queue<byte>();
        private readonly Queue<byte> masterToSlaveAdditionalDataBuffer = new Queue<byte>();
        private readonly Queue<byte> slaveToMasterBuffer = new Queue<byte>();

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

        private enum State
        {
            Idle,
            Stall,
            DataToMasterReady,
            ReadyForDataFromMaster,
            SetupTokenReceived,
            SetupTokenAcked,
            DataFromMasterAcked,
            Error,
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
