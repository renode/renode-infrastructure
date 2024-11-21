//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class RenesasRZG_SPI : SimpleContainer<ISPIPeripheral>, ISPIPeripheral, IKnownSize, INumberedGPIOOutput, IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral
    {
        public RenesasRZG_SPI(IMachine machine) : base(machine)
        {
            Connections = Enumerable
                .Range(0, NrOfInterrupts)
                .ToDictionary<int, int, IGPIO>(idx => idx, _ => new GPIO());

            byteRegisters = new ByteRegisterCollection(this);
            wordRegisters = new WordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            receiveQueue.Clear();
            transmitQueue.Clear();
            byteRegisters.Reset();
            wordRegisters.Reset();
            UpdateInterrupts();
        }

        public byte ReadByte(long offset)
        {
            if(IsDataOffset(offset))
            {
                return (byte)HandleDataRead(AccessWidth.Byte);
            }
            return byteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if(IsDataOffset(offset))
            {
                HandleDataWrite(AccessWidth.Byte, (uint)value);
            }
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(IsDataOffset(offset))
            {
                return (ushort)HandleDataRead(AccessWidth.Word);
            }
            return wordRegisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(IsDataOffset(offset))
            {
                HandleDataWrite(AccessWidth.Word, (uint)value);
            }
            wordRegisters.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(IsDataOffset(offset))
            {
                return HandleDataRead(AccessWidth.LongWord);
            }
            this.WarningLog(
                "Trying to read double word from non double word register at offset 0x{1:X}. Returning 0x0",
                offset
            );
            return 0x0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(IsDataOffset(offset))
            {
                HandleDataWrite(AccessWidth.LongWord, value);
            }
            else
            {
                this.WarningLog(
                    "Trying to write double word 0x{1:X} to non double word register at offset 0x{2:X}. Register won't be updated",
                    value,
                    offset
                );
            }
        }

        public byte Transmit(byte data)
        {
            if(isMaster.Value)
            {
                this.ErrorLog("Peripheral is in master mode, but received transmission from another SPI master.");
                return 0x0;
            }

            receiveQueue.Enqueue(data);
            var response = transmitQueue.Count > (byte)0 ? transmitQueue.Dequeue() : (byte)0;
            UpdateInterrupts();

            return response;
        }

        public void FinishTransmission()
        {
            // intentionally left blank
        }

        public long Size => 0x100;

        // IRQs are bundled into 3 signals per channel in the following order:
        // 0: SPRI - Receive interrupt
        // 1: SPTI - Transmit interrupt
        // 2: SPEI - Error interrupt
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            // Byte registers
            Registers.Control.Define(byteRegisters)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("MODFEN", 2)
                .WithFlag(3, out isMaster, name: "MSTR")
                .WithTaggedFlag("SPEIE", 4)
                .WithFlag(5, out transmitInterruptEnabled, name: "SPTIE")
                .WithTaggedFlag("SPE", 6)
                .WithFlag(7, out receiveInterruptEnabled, name: "SPRIE")
                .WithChangeCallback((_, __) => UpdateInterrupts());

            Registers.SlaveSelectPolarity.Define(byteRegisters)
                .WithTaggedFlag("SSL0P", 0)
                .WithReservedBits(1, 7);

            Registers.PinControl.Define(byteRegisters)
                .WithTaggedFlag("SPLP", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("MOIFV", 4)
                .WithTaggedFlag("MOIFE", 5)
                .WithReservedBits(6, 2);

            Registers.Status.Define(byteRegisters, 0x60)
                .WithTaggedFlag("OVRF", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("MODF", 2)
                .WithReservedBits(3, 2)
                .WithFlag(5, out isTransmitBufferEmpty, FieldMode.Read, name: "SPTEF")
                .WithFlag(6, out transmitEnd, FieldMode.Read, name: "TEND")
                .WithFlag(7, out isReceiveBufferFull, FieldMode.Read, name: "SPRF");

            Registers.SequenceControl.Define(byteRegisters)
                .WithTag("SPSLN", 0, 2)
                .WithReservedBits(2, 6);

            Registers.SequenceStatus.Define(byteRegisters)
                .WithTag("SPCP0", 0, 2)
                .WithReservedBits(2, 6);

            Registers.BitRate.Define(byteRegisters, 0xFF)
                .WithTag("SPBR", 0, 8);

            Registers.DataControl.Define(byteRegisters, 0x20)
                .WithReservedBits(0, 5)
                .WithEnumField<ByteRegister, AccessWidth>(5, 2, out dataAccessWidth, name: "SPLW")
                .WithTaggedFlag("TXDMY", 7);

            Registers.ClockDelay.Define(byteRegisters)
                .WithTag("SCKDL", 0, 3)
                .WithReservedBits(3, 5);

            Registers.SlaveSelectNegationDelay.Define(byteRegisters)
                .WithTag("SLNDL", 0, 3)
                .WithReservedBits(3, 5);

            Registers.NextAccessDelay.Define(byteRegisters)
                .WithTag("SPNDL", 0, 3)
                .WithReservedBits(3, 5);

            Registers.BufferControl.Define(byteRegisters)
                .WithValueField(0, 3, out receiveTriggerNumber, name: "RXTRG")
                .WithReservedBits(3, 1)
                .WithValueField(4, 2, out transmitTriggerNumber, name: "TXTRG")
                .WithFlag(6,
                    writeCallback: (_, value) => TryResetReceiveBuffer(value),
                    name: "RXRST")
                .WithFlag(7,
                    writeCallback: (_, value) => TryResetTransmitBuffer(value),
                    name: "TXRST");

            // Word registers
            DefineCommandRegister(Registers.Command0);
            DefineCommandRegister(Registers.Command1);
            DefineCommandRegister(Registers.Command2);
            DefineCommandRegister(Registers.Command3);

            Registers.BufferDataCountSetting.Define(wordRegisters)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => (ulong)receiveQueue.Count, name: "R")
                .WithReservedBits(6, 2)
                .WithValueField(8, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)transmitQueue.Count, name: "T")
                .WithReservedBits(12, 4);
        }

        private void DefineCommandRegister(Registers commandRegister)
        {
            commandRegister.Define(wordRegisters, 0x070D)
                .WithTaggedFlag("CPHA", 0)
                .WithTaggedFlag("CPOL", 1)
                .WithTag("BRDV", 2, 2)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("SSLKP", 7)
                .WithTag("SPB", 8, 4)
                .WithTaggedFlag("LSBF", 12)
                .WithTaggedFlag("SPNDEN", 13)
                .WithTaggedFlag("SLNDEN", 14)
                .WithTaggedFlag("SCKDEN", 15);
        }

        private bool IsDataOffset(long offset)
        {
            return (Registers)offset == Registers.Data;
        }

        private void UpdateInterrupts()
        {
            isReceiveBufferFull.Value = (ulong)receiveQueue.Count >= receiveTriggerNumber.Value;
            isTransmitBufferEmpty.Value = (ulong)transmitQueue.Count <= transmitTriggerNumber.Value;
            transmitEnd.Value = (ulong)transmitQueue.Count == 0;

            Connections[ReceiveInterruptIdx].Set(receiveInterruptEnabled.Value && isReceiveBufferFull.Value);
            Connections[TransmitInterruptIdx].Set(transmitInterruptEnabled.Value && isTransmitBufferEmpty.Value);
            // Error interrupt is not implemented
            Connections[ErrorInterruptIdx].Set(false);
        }

        private uint HandleDataRead(AccessWidth width)
        {
            if(width != dataAccessWidth.Value)
            {
                this.WarningLog(
                    "Trying to read data of width {0} from Data register when access width {1} is set. Returning 0x0.",
                    width,
                    dataAccessWidth.Value
                );
                return 0x0;
            }

            if(receiveQueue.Count == 0)
            {
                this.WarningLog("Trying to read data, but recevie buffer is empty. Returning 0x0.");
                return 0x0;
            }

            uint data = 0;
            switch(width)
            {
                case AccessWidth.Byte:
                    data = receiveQueue.Dequeue();
                    break;
                case AccessWidth.Word:
                    data = (uint)receiveQueue.Dequeue() << 8 |
                           (uint)receiveQueue.Dequeue();
                    break;
                case AccessWidth.LongWord:
                    data = (uint)receiveQueue.Dequeue() << 24 |
                           (uint)receiveQueue.Dequeue() << 16 |
                           (uint)receiveQueue.Dequeue() << 8  |
                           (uint)receiveQueue.Dequeue();
                    break;
            }

            UpdateInterrupts();

            return data;
        }

        private void HandleDataWrite(AccessWidth width, uint value)
        {
            if(width != dataAccessWidth.Value)
            {
                this.WarningLog(
                    "Trying to write data of width {0} to Data register when access width {1} is set. Dropping data.",
                    width,
                    dataAccessWidth.Value
                );
                return;
            }

            if(isMaster.Value)
            {
                TransmitData(value);
            }
            else
            {
                WriteDataToTransmitQueue(value);
            }

            UpdateInterrupts();
        }

        private void WriteDataToTransmitQueue(uint data)
        {
            switch(dataAccessWidth.Value)
            {
                case AccessWidth.Byte:
                    transmitQueue.Enqueue((byte)data);
                    break;
                case AccessWidth.Word:
                    foreach(var b in BitHelper.GetBytesFromValue(data, 2))
                    {
                        transmitQueue.Enqueue((byte)b);
                    }
                    break;
                case AccessWidth.LongWord:
                    foreach(var b in BitHelper.GetBytesFromValue(data, 4))
                    {
                        transmitQueue.Enqueue((byte)b);
                    }
                    break;
            }
        }

        private void TransmitData(uint data)
        {
            if(!TryGetByAddress(0, out var selectedPeripheral))
            {
                this.WarningLog("Trying to transmit data with no slave registered. Data dropped.");
                return;
            }

            switch(dataAccessWidth.Value)
            {
                case AccessWidth.Byte:
                    receiveQueue.Enqueue(selectedPeripheral.Transmit((byte)data));
                    break;
                case AccessWidth.Word:
                    foreach(var b in BitHelper.GetBytesFromValue(data, 2))
                    {
                        receiveQueue.Enqueue(selectedPeripheral.Transmit(b));
                    }
                    break;
                case AccessWidth.LongWord:
                    foreach(var b in BitHelper.GetBytesFromValue(data, 4))
                    {
                        receiveQueue.Enqueue(selectedPeripheral.Transmit(b));
                    }
                    break;
            }
        }

        private void TryResetReceiveBuffer(bool resetRequested)
        {
            if(resetRequested)
            {
                receiveQueue.Clear();
                UpdateInterrupts();
            }
        }

        private void TryResetTransmitBuffer(bool resetRequested)
        {
            if(resetRequested)
            {
                transmitQueue.Clear();
                UpdateInterrupts();
            }
        }

        private readonly Queue<byte> receiveQueue = new Queue<byte>();
        private readonly Queue<byte> transmitQueue = new Queue<byte>();

        private readonly ByteRegisterCollection byteRegisters;
        private readonly WordRegisterCollection wordRegisters;

        private IFlagRegisterField isMaster;
        private IFlagRegisterField transmitInterruptEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField isReceiveBufferFull;
        private IFlagRegisterField isTransmitBufferEmpty;
        private IFlagRegisterField transmitEnd;
        private IEnumRegisterField<AccessWidth> dataAccessWidth;
        private IValueRegisterField receiveTriggerNumber;
        private IValueRegisterField transmitTriggerNumber;

        private const int NrOfInterrupts = 3;
        private const int ReceiveInterruptIdx = 0;
        private const int TransmitInterruptIdx = 1;
        private const int ErrorInterruptIdx = 2;

        private enum Registers
        {
            Control                     = 0x00, // SPCR
            SlaveSelectPolarity         = 0x01, // SSLP
            PinControl                  = 0x02, // SPPCR
            Status                      = 0x03, // SPSR
            Data                        = 0x04, // SPDR
            SequenceControl             = 0x08, // SPSCR
            SequenceStatus              = 0x09, // SPSSR
            BitRate                     = 0x0A, // SPBR
            DataControl                 = 0x0B, // SPDCR
            ClockDelay                  = 0x0C, // SPCKD
            SlaveSelectNegationDelay    = 0x0D, // SSLND
            NextAccessDelay             = 0x0E, // SPND
            Command0                    = 0x10, // SPCMD0
            Command1                    = 0x12, // SPCMD1
            Command2                    = 0x14, // SPCMD2
            Command3                    = 0x16, // SPCMD3
            BufferControl               = 0x20, // SPBFCR
            BufferDataCountSetting      = 0x22, // SPBFDR
        }

        private enum AccessWidth
        {
            Byte        = 1,
            Word        = 2,
            LongWord    = 3,
        }
    }
}
