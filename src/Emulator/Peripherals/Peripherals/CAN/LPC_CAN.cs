//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CAN
{
    public class LPC_CAN : BasicDoubleWordPeripheral, ICAN
    {
        public LPC_CAN(IMachine machine) : base(machine)
        {
            transmitBuffers = new TransmitBuffer[NumberOfTransmitBuffers];
            for(int i = 0; i < NumberOfTransmitBuffers; ++i)
            {
                transmitBuffers[i] = new TransmitBuffer
                {
                    parent = this,
                    data = new IValueRegisterField[2], // 2 double words
                };
            }
            receiveFifo = new Queue<CANMessageFrame>();
            TxIRQ = new GPIO();
            RxIRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public GPIO TxIRQ { get; }
        public GPIO RxIRQ { get; }

        public override void Reset()
        {
            receiveFifo.Clear();
            TxIRQ.Unset();
            RxIRQ.Unset();
            base.Reset();
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            this.DebugLog("Received {0} bytes [{1}] on id 0x{2:X}", message.Data.Length, message.DataAsHex, message.Id);
            receiveFifo.Enqueue(message);
            UpdateInterrupts();
        }

        public event Action<CANMessageFrame> FrameSent;

        private void TransmitMessage(CANMessageFrame message)
        {
            var fs = FrameSent;
            if(fs == null)
            {
                this.WarningLog("Tried to transmit {0} bytes [{1}] to id 0x{2:X} while not connected to the medium",
                    message.Data.Length, message.DataAsHex, message.Id);
                return;
            }

            this.DebugLog("Transmitting {0} bytes [{1}] to id 0x{2:X}", message.Data.Length, message.DataAsHex, message.Id);
            fs(message);
        }

        private void UpdateInterrupts()
        {
            receiveInterruptFlag.Value = receiveFifo.Any();

            var txIrqValue = transmitBuffers.Any(b => b.transmitInterruptEnable.Value && b.transmitInterruptFlag.Value);
            var rxIrqValue = receiveInterruptEnable.Value && receiveInterruptFlag.Value;
            if(TxIRQ.IsSet == txIrqValue && RxIRQ.IsSet == rxIrqValue)
            {
                return;
            }

            this.NoisyLog("Setting interrupts: TX={0}, RX={1}", txIrqValue, rxIrqValue);
            TxIRQ.Set(txIrqValue);
            RxIRQ.Set(rxIrqValue);
        }

        private void DefineRegisters()
        {
            Registers.OperatingMode.Define(this)
                .WithTaggedFlag("RM", 0)
                .WithTaggedFlag("LOM", 1)
                .WithTaggedFlag("STM", 2)
                .WithTaggedFlag("TPM", 3)
                .WithTaggedFlag("SM", 4)
                .WithTaggedFlag("RPM", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("TM", 7)
                .WithReservedBits(8, 24);

            Registers.Command.Define(this)
                .WithFlag(0, mode: FieldMode.WriteOneToClear, name: "TR", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        var buffersToTransmit = transmitBuffers.Where(b => b.select.Value).OrderBy(b => b.priority.Value).ToList();
                        if(!buffersToTransmit.Any())
                        {
                            // If no buffers are selected with the STB bits, we always transmit buffer 1.
                            transmitBuffers[0].DoTransmission();
                            // In this case we also force its transmit interrupt enable flag to on.
                            transmitBuffers[0].transmitInterruptEnable.Value = true;
                        }
                        else
                        {
                            foreach(var buffer in buffersToTransmit)
                            {
                                buffer.DoTransmission();
                            }
                        }
                    }
                })
                .WithTaggedFlag("AT", 1)
                .WithFlag(2, mode: FieldMode.WriteOneToClear, name: "RRB", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        receiveFifo.TryDequeue(out var __); // Release Receive Buffer, discard the current message
                    }
                })
                .WithTaggedFlag("CDO", 3)
                .WithTaggedFlag("SRR", 4)
                .WithFlag(5, out transmitBuffers[0].select, name: "STB1")
                .WithFlag(6, out transmitBuffers[1].select, name: "STB2")
                .WithFlag(7, out transmitBuffers[2].select, name: "STB3")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.GlobalStatus.Define(this, 0x3C)
                .WithFlag(0, mode: FieldMode.Read, name: "RBS",
                    valueProviderCallback: _ => receiveFifo.Any())
                .WithTaggedFlag("DOS", 1)
                .WithTaggedFlag("TBS", 2)
                .WithTaggedFlag("TCS", 3)
                .WithTaggedFlag("RS", 4)
                .WithTaggedFlag("TS", 5)
                .WithTaggedFlag("ES", 6)
                .WithTaggedFlag("BS", 7)
                .WithReservedBits(8, 8)
                .WithTag("RXERR", 16, 8)
                .WithTag("TXERR", 24, 8);

            Registers.InterruptCapture.Define(this)
                .WithFlag(0, out receiveInterruptFlag, mode: FieldMode.Read, name: "RI")
                .WithFlag(1, out transmitBuffers[0].transmitInterruptFlag, mode: FieldMode.ReadToClear, name: "TI1")
                .WithTaggedFlag("EI", 2)
                .WithTaggedFlag("DOI", 3)
                .WithTaggedFlag("WUI", 4)
                .WithTaggedFlag("EPI", 5)
                .WithTaggedFlag("ALI", 6)
                .WithTaggedFlag("BEI", 7)
                .WithTaggedFlag("IDI", 8)
                .WithFlag(9, out transmitBuffers[1].transmitInterruptFlag, mode: FieldMode.ReadToClear, name: "TI2")
                .WithFlag(10, out transmitBuffers[2].transmitInterruptFlag, mode: FieldMode.ReadToClear, name: "TI3")
                .WithReservedBits(11, 5)
                .WithTag("ERRBIT4_0", 16, 5)
                .WithTaggedFlag("ERRDIR", 21)
                .WithTag("ERRC1_0", 22, 2)
                .WithTag("ALCBIT", 24, 8)
                .WithReadCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out receiveInterruptEnable, name: "RIE")
                .WithFlag(1, out transmitBuffers[0].transmitInterruptEnable, name: "TIE1")
                .WithTaggedFlag("EIE", 2)
                .WithTaggedFlag("DOIE", 3)
                .WithTaggedFlag("WUIE", 4)
                .WithTaggedFlag("EPIE", 5)
                .WithTaggedFlag("ALIE", 6)
                .WithTaggedFlag("BEIE", 7)
                .WithTaggedFlag("IDIE", 8)
                .WithFlag(9, out transmitBuffers[1].transmitInterruptEnable, name: "TIE2")
                .WithFlag(10, out transmitBuffers[2].transmitInterruptEnable, name: "TIE3")
                .WithReservedBits(11, 21)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.BusTiming.Define(this, 0x1C0000)
                .WithTag("BRP", 0, 10)
                .WithReservedBits(10, 4)
                .WithTag("SJW", 14, 2)
                .WithTag("TESG1", 16, 4)
                .WithTag("TESG2", 20, 3)
                .WithTaggedFlag("SAM", 23)
                .WithReservedBits(24, 8);

            Registers.ErrorWarningLimit.Define(this, 0x60)
                .WithTag("EWL", 0, 8)
                .WithReservedBits(8, 24);

            Registers.Status.Define(this, 0x3C3C3C)
                .WithFlag(0, mode: FieldMode.Read, name: "RBS_1",
                    valueProviderCallback: _ => receiveFifo.Any()) // Same as RBS in GlobalStatus
                .WithTaggedFlag("DOS_1", 1)
                .WithTaggedFlag("TBS1_1", 2)
                .WithTaggedFlag("TCS1_1", 3)
                .WithTaggedFlag("RS_1", 4)
                .WithTaggedFlag("TS1_1", 5)
                .WithTaggedFlag("ES_1", 6)
                .WithTaggedFlag("BS_1", 7)
                .WithFlag(8, mode: FieldMode.Read, name: "RBS_2",
                    valueProviderCallback: _ => receiveFifo.Any()) // Same as RBS in GlobalStatus
                .WithTaggedFlag("DOS_2", 9)
                .WithTaggedFlag("TBS2_2", 10)
                .WithTaggedFlag("TCS2_2", 11)
                .WithTaggedFlag("RS_2", 12)
                .WithTaggedFlag("TS2_2", 13)
                .WithTaggedFlag("ES_2", 14)
                .WithTaggedFlag("BS_2", 15)
                .WithFlag(16, mode: FieldMode.Read, name: "RBS_3",
                    valueProviderCallback: _ => receiveFifo.Any()) // Same as RBS in GlobalStatus
                .WithTaggedFlag("DOS_3", 17)
                .WithTaggedFlag("TBS3_3", 18)
                .WithTaggedFlag("TCS3_3", 19)
                .WithTaggedFlag("RS_3", 20)
                .WithTaggedFlag("TS3_3", 21)
                .WithTaggedFlag("ES_3", 22)
                .WithTaggedFlag("BS_3", 23)
                .WithReservedBits(24, 8);

            Registers.ReceiveFrameStatus.Define(this)
                .WithTag("IDINDEX", 0, 10)
                .WithTaggedFlag("BP", 10)
                .WithReservedBits(11, 5)
                .WithValueField(16, 4, mode: FieldMode.Read, name: "DLC",
                    valueProviderCallback: _ => (ulong)Math.Min(receiveFifo.FirstOrDefault()?.Data.Length ?? 0, FrameMaxLength))
                .WithReservedBits(20, 10)
                .WithTaggedFlag("RTR", 30)
                .WithTaggedFlag("FF", 31);

            Registers.ReceivedIdentifier.Define(this)
                .WithValueField(0, 11, mode: FieldMode.Read, name: "ID",
                    valueProviderCallback: _ => (ulong)receiveFifo.FirstOrDefault()?.Id)
                .WithReservedBits(11, 21);

            Registers.ReceivedDataBytes1To4.DefineMany(this, 2, stepInBytes: 4, setup: (register, registerIndex) => register
                .WithValueFields(0, 8, 4, FieldMode.Read,
                    valueProviderCallback: (i, _) => receiveFifo.FirstOrDefault()?.Data.ElementAtOrDefault(registerIndex * 4 + i) ?? 0)
            );

            // Each transmit buffer consists of these 4 registers
            Registers.TransmitBuffer1FrameInfo.DefineMany(this, NumberOfTransmitBuffers, stepInBytes: TransmitBufferStride, setup: (register, i) => register
                .WithValueField(0, 8, out transmitBuffers[i].priority, name: "PRIO")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, out transmitBuffers[i].dataLengthCode, name: "DLC")
                .WithReservedBits(20, 10)
                .WithTaggedFlag("RTR", 30)
                .WithTaggedFlag("FF", 31)
            );

            Registers.TransmitBuffer1Identifier.DefineMany(this, NumberOfTransmitBuffers, stepInBytes: TransmitBufferStride, setup: (register, i) => register
                .WithValueField(0, 11, out transmitBuffers[i].id, name: "ID")
                .WithReservedBits(11, 21) // 29-bit ID mode (FrameInfo.FF=1) is not implemented
            );

            Registers.TransmitBuffer1DataBytes1To4.DefineMany(this, NumberOfTransmitBuffers, stepInBytes: TransmitBufferStride, setup: (register, i) => register
                .WithValueField(0, 32, out transmitBuffers[i].data[0], name: "DATA[1:4]")
            );

            Registers.TransmitBuffer1DataBytes5To8.DefineMany(this, NumberOfTransmitBuffers, stepInBytes: TransmitBufferStride, setup: (register, i) => register
                .WithValueField(0, 32, out transmitBuffers[i].data[1], name: "DATA[5:8]")
            );
        }

        private readonly TransmitBuffer[] transmitBuffers;
        private readonly Queue<CANMessageFrame> receiveFifo;

        private IFlagRegisterField receiveInterruptFlag;
        private IFlagRegisterField receiveInterruptEnable;

        private const int NumberOfTransmitBuffers = 3;
        private const int FrameMaxLength = 8;
        private const int TransmitBufferStride = Registers.TransmitBuffer2FrameInfo - Registers.TransmitBuffer1FrameInfo;

        private struct TransmitBuffer
        {
            public void DoTransmission()
            {
                var length = Math.Min((int)dataLengthCode.Value, FrameMaxLength);
                if(length == 0)
                {
                    return;
                }

                var bytes = data.SelectMany(d => BitConverter.GetBytes((uint)d.Value)).Take(length).ToArray();
                parent.TransmitMessage(new CANMessageFrame((uint)id.Value, bytes));
                transmitInterruptFlag.Value = true;
            }

            public LPC_CAN parent;
            public IFlagRegisterField transmitInterruptEnable;
            public IFlagRegisterField transmitInterruptFlag;
            public IFlagRegisterField select;
            public IValueRegisterField priority;
            public IValueRegisterField id;
            public IValueRegisterField dataLengthCode;
            public IValueRegisterField[] data;
        }

        private enum Registers
        {
            OperatingMode = 0x00,
            Command = 0x04,
            GlobalStatus = 0x08,
            InterruptCapture = 0x0c,
            InterruptEnable = 0x10,
            BusTiming = 0x14,
            ErrorWarningLimit = 0x18,
            Status = 0x1c,
            // Receive buffer
            ReceiveFrameStatus = 0x20,
            ReceivedIdentifier = 0x24,
            ReceivedDataBytes1To4 = 0x28,
            ReceivedDataBytes5To8 = 0x2c,
            // Transmit buffer #1
            TransmitBuffer1FrameInfo = 0x30,
            TransmitBuffer1Identifier = 0x34,
            TransmitBuffer1DataBytes1To4 = 0x38,
            TransmitBuffer1DataBytes5To8 = 0x3c,
            // Transmit buffer #2
            TransmitBuffer2FrameInfo = 0x40,
            TransmitBuffer2Identifier = 0x44,
            TransmitBuffer2DataBytes1To4 = 0x48,
            TransmitBuffer2DataBytes5To8 = 0x4c,
            // Transmit buffer #3
            TransmitBuffer3FrameInfo = 0x50,
            TransmitBuffer3Identifier = 0x54,
            TransmitBuffer3DataBytes1To4 = 0x58,
            TransmitBuffer3DataBytes5To8 = 0x5c,
        }
    }
}
