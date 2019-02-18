//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class LiteX_Ethernet : BasicDoubleWordPeripheral, IMACInterface
    {
        public LiteX_Ethernet(Machine machine, int numberOfWriteSlots = 2, int numberOfReadSlots = 2) : base(machine)
        {
            Interlocked.Add(ref NumberOfInstances, 1);

            MAC = MACAddress.Parse("10:e2:d5:00:00:00").Next(NumberOfInstances - 1);

            writeSlots = new Slot[numberOfWriteSlots];
            readSlots = new Slot[numberOfReadSlots];
            for(var i = 0; i < numberOfWriteSlots; i++)
            {
                writeSlots[i] = new Slot();
            }
            for(var i = 0; i < numberOfReadSlots; i++)
            {
                readSlots[i] = new Slot();
            }
        }

        public override void Reset()
        {
            base.Reset();

            latchedWriterSlot = -1;

            foreach(var slot in writeSlots.Union(readSlots))
            {
                slot.Reset();
            }

            RefreshIrq();
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            this.Log(LogLevel.Noisy, "Received frame of length: {0} bytes", frame.Bytes.Length);

            var emptySlotId = -1;
            for(var i = 0; i < writeSlots.Length; i++)
            {
                if(!writeSlots[i].IsBusy)
                {
                    emptySlotId = i;
                    break;
                }
            }

            if(emptySlotId == -1)
            {
                this.Log(LogLevel.Warning, "There are no empty write slots. Dropping the packet");
                return;
            }

            writerSlotNumber.Value = (uint)emptySlotId;
            var slot = writeSlots[emptySlotId];
            if(!slot.TryWrite(frame.Bytes))
            {
                this.Log(LogLevel.Warning, "Packet is too long. Dropping");
                return;
            }
            
            UpdateEvents();
        }

        [ConnectionRegionAttribute("buffer")]
        public uint ReadDoubleWordFromBuffer(long offset)
        {
            var slot = FindSlot(offset, out var slotOffset);
            if(slot == null)
            {
                this.Log(LogLevel.Warning, "Reading outside buffer memory");
                return 0;
            }

            return slot.ReadUInt32(slotOffset);
        }

        [ConnectionRegionAttribute("buffer")]
        public void WriteDoubleWordToBuffer(long offset, uint value)
        {
            var slot = FindSlot(offset, out var slotOffset);
            if(slot == null || !slot.TryWriteUInt32(slotOffset, value))
            {
                this.Log(LogLevel.Warning, "Writing outside buffer memory");
            }
        }

        public MACAddress MAC { get; set; }

        public event Action<EthernetFrame> FrameReady;

        public GPIO IRQ { get; } = new GPIO();

        protected override void DefineRegisters()
        {
            Registers.ReaderEvPending.Define(this)
                .WithFlag(0, out readerEventPending, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, __) => RefreshIrq())
            ;

            Registers.WriterLength0.DefineMany(this, NumberOfWriterLengthSubRegisters, (reg, idx) => 
                reg.WithValueField(0, DataWidth, FieldMode.Read, name: $"writer_length_{idx}", valueProviderCallback: _ =>
                {
                    return BitHelper.GetValue(writeSlots[writerSlotNumber.Value].DataLength,
                        offset: (NumberOfWriterLengthSubRegisters - idx - 1) * DataWidth,
                        size: DataWidth);
                }))
            ;

            Registers.WriterEvPending.Define(this)
                .WithFlag(0, out writerEventPending, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                {
                    if(!val || latchedWriterSlot == -1)
                    {
                        return;
                    }

                    writeSlots[latchedWriterSlot].Release();
                    latchedWriterSlot = -1;
                    UpdateEvents();
                })
            ;

            Registers.ReaderSlot.Define(this)
                .WithValueField(0, 32, out readerSlotNumber, name: "reader_slot", writeCallback: (_, val) =>
                {
                    if(val >= readSlots.Length)
                    {
                        this.Log(LogLevel.Warning, "Trying to set reader slot number out of range ({0}). Forcing value of 0", val);
                        readerSlotNumber.Value = 0;
                    }
                })
            ;

            Registers.WriterSlot.Define(this)
                .WithValueField(0, 32, out writerSlotNumber, FieldMode.Read, name: "writer_slot", readCallback: (_, val) =>
                {
                    // this is a bit hacky - here we remember the last returned writer slot number to release it later 
                    latchedWriterSlot = (int)val;
                })
            ;

            Registers.ReaderLengthHi.DefineMany(this, NumberOfReaderLengthSubRegisters, (reg, idx) => 
                reg.WithValueField(0, DataWidth, name: $"reader_length_{idx}", 
                writeCallback: (_, val) =>
                {
                    readSlots[readerSlotNumber.Value].DataLength = 
                        BitHelper.ReplaceBits(readSlots[readerSlotNumber.Value].DataLength, val, 
                            width: DataWidth, 
                            destinationPosition: (int)((NumberOfReaderLengthSubRegisters - idx - 1) * DataWidth));
                },
                valueProviderCallback: _ =>
                {
                    return BitHelper.GetValue(readSlots[readerSlotNumber.Value].DataLength, 
                        offset: (NumberOfReaderLengthSubRegisters - idx - 1) * DataWidth, 
                        size: DataWidth);
                }))
            ;

            Registers.ReaderReady.Define(this)
                .WithFlag(0, FieldMode.Read, name: "reader_ready", valueProviderCallback: _ => true)
            ;

            Registers.ReaderStart.Define(this)
                .WithFlag(0, FieldMode.Write, name: "reader_start", writeCallback: (_, __) => SendPacket())
            ;

            Registers.ReaderEvEnable.Define(this)
                .WithFlag(0, out readerEventEnabled, name: "reader_event_enable", writeCallback: (_, __) => RefreshIrq())
            ;

            Registers.WriterEvEnable.Define(this)
                .WithFlag(0, out writerEventEnabled, name: "writer_event_enable", writeCallback: (_, __) => RefreshIrq())
            ;
        }

        private void UpdateEvents()
        {
            writerEventPending.Value = writeSlots.Any(s => s.IsBusy);
            RefreshIrq();
        }

        private void RefreshIrq()
        {
            var anyEventPending = (writerEventEnabled.Value && writerEventPending.Value) 
                || (readerEventEnabled.Value && readerEventPending.Value);

            this.Log(LogLevel.Noisy, "Setting IRQ to: {0}", anyEventPending);
            IRQ.Set(anyEventPending);
        }

        private void SendPacket()
        {
            var slot = readSlots[readerSlotNumber.Value];
            var frame = EthernetFrame.CreateEthernetFrameWithCRC(slot.Read());

            this.Log(LogLevel.Noisy, "Sending packet of length {0} bytes.", frame.Length);
            FrameReady?.Invoke(frame);

            readerEventPending.Value = true;
            RefreshIrq();
        }

        private Slot FindSlot(long offset, out int slotOffset)
        {
            var slotId = offset / SlotSize;
            slotOffset = (int)(offset % SlotSize);
                
            if(slotId < writeSlots.Length)
            {
                return writeSlots[slotId];
            }
            else if(slotId < (writeSlots.Length + readSlots.Length))
            {
                return readSlots[slotId - writeSlots.Length];
            }

            return null;
        }

        private static int NumberOfInstances;

        private IFlagRegisterField readerEventEnabled;
        private IFlagRegisterField writerEventEnabled;
        private IFlagRegisterField readerEventPending;
        private IFlagRegisterField writerEventPending;
        private IValueRegisterField writerSlotNumber;
        private IValueRegisterField readerSlotNumber;
        private int latchedWriterSlot = -1;

        // WARNING:
        // read slots contains packets to be sent
        // write slots contains received packets
        private readonly Slot[] writeSlots;
        private readonly Slot[] readSlots;

        private const int SlotSize = 0x0800;

        private const int DataWidth = 8;
        // 'ReaderLength` is a 16-bit register, but because the data width is set to 8 by default, it is splitted into 2 subregisters
        private const int ReaderLengthWidth = 16;
        private const int NumberOfReaderLengthSubRegisters = ReaderLengthWidth / DataWidth;
        // 'WriterLength` is a 32-bit register, but because the data width is set to 8 by default, it is splitted into 4 subregisters
        private const int WriterLengthWidth = 32;
        private const int NumberOfWriterLengthSubRegisters = WriterLengthWidth / DataWidth;

        private class Slot
        {
            public Slot()
            {
                buffer = new byte[SlotSize];
            }

            public bool TryWriteUInt32(int offset, uint value)
            {
                if(offset > buffer.Length - 4)
                {
                    return false;
                }

                buffer[offset + 3] = (byte)(value >> 24);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 0] = (byte)(value);
                return true;
            }

            public bool TryWrite(byte[] data, int padding = 60)
            {
                if(data.Length > buffer.Length)
                {
                    return false;
                }

                Array.Copy(data, buffer, data.Length);

                var paddingBytesCount = data.Length % padding;
                for(var i = 0; i < paddingBytesCount; i++)
                {
                    buffer[data.Length + i] = 0;
                }

                DataLength = (uint)(data.Length + paddingBytesCount);
                return true;
            }

            public uint ReadUInt32(int offset)
            {
                return (offset > buffer.Length - 4)
                    ? 0
                    : BitHelper.ToUInt32(buffer, offset, 4, true);
            }

            public byte[] Read()
            {
                var result = new byte[DataLength];
                Array.Copy(buffer, result, DataLength);
                return result;
            }

            public void Release()
            {
                DataLength = 0;
            }

            public void Reset()
            {
                DataLength = 0;
                Array.Clear(buffer, 0, buffer.Length);
            }
            
            public bool IsBusy => DataLength > 0;

            public uint DataLength { get; set; }

            private readonly byte[] buffer;
        }

        private enum Registers
        {
            WriterSlot         = 0x0,
            WriterLength0      = 0x04,
            WriterLength1      = 0x08,
            WriterLength2      = 0x0C,
            WriterLength3      = 0x10,
            WriterErrors       = 0x14,
            WriterEvStatus     = 0x24,
            WriterEvPending    = 0x28,
            WriterEvEnable     = 0x2c,
            ReaderStart        = 0x30,
            ReaderReady        = 0x34,
            ReaderLevel        = 0x38,
            ReaderSlot         = 0x3c,
            ReaderLengthHi     = 0x40,
            ReaderLengthLo     = 0x44,
            ReaderEvStatus     = 0x48,
            ReaderEvPending    = 0x4c,
            ReaderEvEnable     = 0x50,
            PreambleCRC        = 0x54,
            PreambleErrors     = 0x58,
            CrcErrors          = 0x68
        }
    }
}
