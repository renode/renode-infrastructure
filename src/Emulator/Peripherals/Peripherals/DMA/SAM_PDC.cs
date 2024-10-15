//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public interface ISamPdcPeripheral : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IBusPeripheral
    {
        TransferType DmaReadAccessWidth { get; }
        TransferType DmaWriteAccessWidth { get; }
    }

    public interface ISamPdcBytePeripheral : ISamPdcPeripheral
    {
        byte? DmaByteRead();
        void DmaByteWrite(byte data);
    }

    public interface ISamPdcWordPeripheral : ISamPdcPeripheral
    {
        ushort? DmaWordRead();
        void DmaWordWrite(ushort data);
    }

    public interface ISamPdcDoubleWordPeripheral : ISamPdcPeripheral
    {
        uint? DmaDoubleWordRead();
        void DmaDoubleWordWrite(uint data);
    }

    public interface ISamPdcQuadWordPeripheral : ISamPdcPeripheral
    {
        ulong? DmaQuadWordRead();
        void DmaQuadWordWrite(ulong data);
    }

    public class SAM_PDC
    {
        public SAM_PDC(IMachine machine, ISamPdcPeripheral parent, long registersOffset, Action flagsChangedCallback, bool receiverEnabled = true, bool transmitterEnabled = true)
        {
            this.machine = machine;
            this.parent = parent;
            this.receiverEnabled = receiverEnabled;
            this.transmitterEnabled = transmitterEnabled;
            FlagsChanged = flagsChangedCallback;
            engine = new DmaEngine(machine.GetSystemBus(parent));
            receiverBuffer = new List<byte>();
            DefineRegisters(registersOffset);
            Reset();
        }

        public void Reset()
        {
            RxBufferFull = true;
            TxBufferEmpty = true;
            EndOfRxBuffer = true;
            EndOfTxBuffer = true;
            receiverBuffer.Clear();
            transmitterBuffer = null;
            transmitterBufferOffset = 0;
            FlagsChanged?.Invoke();
        }

        public void DefineRegisters(long offset)
        {
            ((Registers)((long)Registers.ReceivePointer + offset)).Define(parent)
                .WithValueField(0, 32, out receivePointer, name: "RXPTR")
            ;

            ((Registers)((long)Registers.ReceiveCounter + offset)).Define(parent)
                .WithValueField(0, 16, out receiveCounter, name: "RXCTR")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => ClearRxFlags())
            ;

            ((Registers)((long)Registers.TransmitPointer + offset)).Define(parent)
                .WithValueField(0, 32, out transmitPointer, name: "TXPTR")
            ;

            ((Registers)((long)Registers.TransmitCounter + offset)).Define(parent)
                .WithValueField(0, 16, out transmitCounter, name: "TXCTR")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => ClearTxFlags())
            ;

            ((Registers)((long)Registers.ReceiveNextPointer + offset)).Define(parent)
                .WithValueField(0, 32, out receiveNextPointer, name: "RXNPTR")
            ;

            ((Registers)((long)Registers.ReceiveNextCounter + offset)).Define(parent)
                .WithValueField(0, 16, out receiveNextCounter, name: "RXNCTR")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => ClearRxFlags())
            ;

            ((Registers)((long)Registers.TransmitNextPointer + offset)).Define(parent)
                .WithValueField(0, 32, out transmitNextPointer, name: "TXNPTR")
            ;

            ((Registers)((long)Registers.TransmitNextCounter + offset)).Define(parent)
                .WithValueField(0, 16, out transmitNextCounter, name: "TXNCTR")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => ClearTxFlags())
            ;

            ((Registers)((long)Registers.TransferControl + offset)).Define(parent)
                .If(receiverEnabled)
                    .Then(reg => reg
                        .WithFlag(0, out receiverTransferEnabled, FieldMode.Set, name: "RXTEN")
                        .WithFlag(1, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) receiverTransferEnabled.Value = false; }, name: "RXTDIS")
                        .WithWriteCallback((_, __) => TriggerReceiver())
                    )
                    .Else(reg => reg
                        .WithReservedBits(0, 2)
                    )
                .WithReservedBits(2, 6)
                .If(transmitterEnabled)
                    .Then(reg => reg
                        .WithFlag(8, out transmitterTransferEnabled, FieldMode.Set, name: "TXTEN")
                        .WithFlag(9, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) transmitterTransferEnabled.Value = false; }, name: "TXTDIS")
                        .WithWriteCallback((_, __) => TriggerTransmitter())
                    )
                    .Else(reg => reg
                        .WithReservedBits(8, 2)
                    )
                .WithReservedBits(10, 22)
            ;

            ((Registers)((long)Registers.TransferStatus + offset)).Define(parent)
                .If(receiverEnabled)
                    .Then(reg => reg
                        .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => receiverTransferEnabled.Value, name: "RXTEN")
                    )
                    .Else(reg => reg
                        .WithReservedBits(0, 1)
                    )
                .WithReservedBits(1, 7)
                .If(transmitterEnabled)
                    .Then(reg => reg
                        .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => transmitterTransferEnabled.Value, name: "TXTEN")
                    )
                    .Else(reg => reg
                        .WithReservedBits(8, 1)
                    )
                .WithReservedBits(9, 23)
            ;
        }

        public void TriggerReceiver()
        {
            if(!receiverEnabled || !receiverTransferEnabled.Value)
            {
                return;
            }

            if(receiveCounter.Value == 0)
            {
                parent.NoisyLog("Receiver triggered, but buffer is not available");
                receiverTransferEnabled.Value = false;
                return;
            }
            parent.NoisyLog("Receiver triggered");

            TriggerReceiverInner();

            if(receiveCounter.Value == 0)
            {
                EndOfRxBuffer = true;
                if(receiveNextCounter.Value == 0)
                {
                    receiverTransferEnabled.Value = false;
                    RxBufferFull = true;
                    FlagsChanged?.Invoke();
                    return;
                }

                receivePointer.Value = receiveNextPointer.Value;
                receiveNextPointer.Value = 0x0;
                receiveCounter.Value = receiveNextCounter.Value;
                receiveNextCounter.Value = 0;
                TriggerReceiverInner();
                FlagsChanged?.Invoke();
            }
        }

        public bool RxBufferFull { get; private set; }
        public bool TxBufferEmpty { get; private set; }
        public bool EndOfRxBuffer { get; private set; }
        public bool EndOfTxBuffer { get; private set; }

        private void ClearRxFlags()
        {
            var changed = RxBufferFull || EndOfRxBuffer;
            RxBufferFull = false;
            EndOfRxBuffer = false;
            if(changed)
            {
                FlagsChanged?.Invoke();
            }
        }

        private void ClearTxFlags()
        {
            var changed = TxBufferEmpty || EndOfTxBuffer;
            TxBufferEmpty = false;
            EndOfTxBuffer = false;
            if(changed)
            {
                FlagsChanged?.Invoke();
            }
        }

        private void FinalizeReceiverTransfer()
        {
            if(receiverBuffer.Count == 0)
            {
                return;
            }
            var transferType = parent.DmaReadAccessWidth;

            var request = new Request(
                new Place(receiverBuffer.ToArray(), 0),
                new Place(receivePointer.Value),
                receiverBuffer.Count,
                transferType,
                transferType
            );

            parent.DebugLog("Executing receiver transfer to 0x{0:X} of {1} bytes", receivePointer.Value, receiverBuffer.Count);
            engine.IssueCopy(request);
            receiverBuffer.Clear();
        }

        private void TriggerTransmitter()
        {
            if(!transmitterEnabled || !transmitterTransferEnabled.Value)
            {
                return;
            }

            if(transmitCounter.Value == 0)
            {
                parent.NoisyLog("Transmitter triggered, but buffer is not available");
                transmitterTransferEnabled.Value = false;
                return;
            }
            parent.NoisyLog("Transmitter triggered");

            TriggerTransmitterInner();

            if(transmitCounter.Value == 0)
            {
                EndOfTxBuffer = true;
                if(transmitNextCounter.Value == 0)
                {
                    transmitterTransferEnabled.Value = false;
                    TxBufferEmpty = true;
                    FlagsChanged?.Invoke();
                    return;
                }

                transmitPointer.Value = transmitNextPointer.Value;
                transmitNextPointer.Value = 0x0;
                transmitCounter.Value = transmitNextCounter.Value;
                transmitNextCounter.Value = 0;
                TriggerTransmitterInner();
                FlagsChanged?.Invoke();
            }
        }

        private void StartTransmitterTransfer()
        {
            var transferType = parent.DmaWriteAccessWidth;
            transmitterBuffer = new byte[(int)transmitCounter.Value * (int)transferType];
            transmitterBufferOffset = 0;
            var request = new Request(
                new Place(transmitPointer.Value),
                new Place(transmitterBuffer, 0),
                (int)transmitCounter.Value,
                transferType,
                transferType
            );

            parent.DebugLog("Executing transmitter transfer from 0x{0:X} of {1} bytes", transmitPointer.Value, transmitterBuffer.Length);
            engine.IssueCopy(request);
        }

        private void TriggerReceiverInner()
        {
            for(; receiveCounter.Value > 0; receiveCounter.Value -= 1)
            {
                if(!TryRead())
                {
                    break;
                }
            }

            if(receiveCounter.Value == 0)
            {
                FinalizeReceiverTransfer();
            }
        }

        private void TriggerTransmitterInner()
        {
            if(transmitterBuffer == null)
            {
                StartTransmitterTransfer();
            }

            for(; transmitCounter.Value > 0; transmitCounter.Value -= 1)
            {
                Write();
            }
        }

        private void Write()
        {
            var transferType = parent.DmaWriteAccessWidth;
            switch(transferType)
            {
            case TransferType.Byte:
                if(parent is ISamPdcBytePeripheral bytePeripheral)
                {
                    bytePeripheral.DmaByteWrite(transmitterBuffer[transmitterBufferOffset]);
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcBytePeripheral, but Byte transfer was selected");
                return;
            case TransferType.Word:
                if(parent is ISamPdcWordPeripheral wordPeripheral)
                {
                    wordPeripheral.DmaWordWrite(BitConverter.ToUInt16(transmitterBuffer, transmitterBufferOffset));
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcWordPeripheral, but Word transfer was selected");
                return;
            case TransferType.DoubleWord:
                if(parent is ISamPdcDoubleWordPeripheral doubleWordPeripheral)
                {
                    doubleWordPeripheral.DmaDoubleWordWrite(BitConverter.ToUInt32(transmitterBuffer, transmitterBufferOffset));
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcDoubleWordPeripheral, but DoubleWord transfer was selected");
                return;
            case TransferType.QuadWord:
                if(parent is ISamPdcQuadWordPeripheral quadWordPeripheral)
                {
                    quadWordPeripheral.DmaQuadWordWrite(BitConverter.ToUInt64(transmitterBuffer, transmitterBufferOffset));
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcQuadWordPeripheral, but QuadWord transfer was selected");
                return;
            default:
                throw new Exception("Unreachable");
            }
            transmitterBufferOffset += (int)transferType;
            if(transmitterBufferOffset == transmitterBuffer.Length)
            {
                transmitterBuffer = null;
            }
        }

        private bool TryRead()
        {
            byte[] data = null;

            switch(parent.DmaReadAccessWidth)
            {
            case TransferType.Byte:
                if(parent is ISamPdcBytePeripheral bytePeripheral)
                {
                    data = bytePeripheral.DmaByteRead()?.AsRawBytes();
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcBytePeripheral, but Byte transfer was selected");
                return false;
            case TransferType.Word:
                if(parent is ISamPdcWordPeripheral wordPeripheral)
                {
                    data = wordPeripheral.DmaWordRead()?.AsRawBytes();
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcWordPeripheral, but Word transfer was selected");
                return false;
            case TransferType.DoubleWord:
                if(parent is ISamPdcDoubleWordPeripheral doubleWordPeripheral)
                {
                    data = doubleWordPeripheral.DmaDoubleWordRead()?.AsRawBytes();
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcDoubleWordPeripheral, but DoubleWord transfer was selected");
                return false;
            case TransferType.QuadWord:
                if(parent is ISamPdcQuadWordPeripheral quadWordPeripheral)
                {
                    data = quadWordPeripheral.DmaQuadWordRead()?.AsRawBytes();
                    break;
                }
                parent.ErrorLog("Perent peripheral doesn't implement ISamPdcQuadWordPeripheral, but QuadWord transfer was selected");
                return false;
            default:
                throw new Exception("Unreachable");
            }

            if(data == null)
            {
                return false;
            }

            receiverBuffer.AddRange(data);
            return true;
        }

        private Action FlagsChanged { get; }

        private IValueRegisterField receivePointer;
        private IValueRegisterField receiveCounter;
        private IValueRegisterField transmitPointer;
        private IValueRegisterField transmitCounter;
        private IValueRegisterField receiveNextPointer;
        private IValueRegisterField receiveNextCounter;
        private IValueRegisterField transmitNextPointer;
        private IValueRegisterField transmitNextCounter;
        private IFlagRegisterField receiverTransferEnabled;
        private IFlagRegisterField transmitterTransferEnabled;

        private byte[] transmitterBuffer;
        private int transmitterBufferOffset;

        private readonly IMachine machine;
        private readonly ISamPdcPeripheral parent;
        private readonly DmaEngine engine;
        private readonly List<byte> receiverBuffer;
        private readonly bool receiverEnabled;
        private readonly bool transmitterEnabled;

        public enum Registers
        {
            ReceivePointer      = 0x00,
            ReceiveCounter      = 0x04,
            TransmitPointer     = 0x08,
            TransmitCounter     = 0x0C,
            ReceiveNextPointer  = 0x10,
            ReceiveNextCounter  = 0x14,
            TransmitNextPointer = 0x18,
            TransmitNextCounter = 0x1C,
            TransferControl     = 0x20,
            TransferStatus      = 0x24,
        }
    }
}