//
// Copyright (c) 2010-2023 Antmicro
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
    public class LiteX_Ethernet_CSR32 : NetworkWithPHY, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IMACInterface, IKnownSize
    {
        public LiteX_Ethernet_CSR32(IMachine machine, int numberOfWriteSlots = 2, int numberOfReadSlots = 2) : base(machine)
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

            bbHelper = new BitBangHelper(width: 16, loggingParent: this);

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            bbHelper.Reset();

            latchedWriterSlot = -1;
            lastPhyAddress = 0;
            lastRegisterAddress = 0;

            foreach(var slot in writeSlots.Union(readSlots))
            {
                slot.Reset();
            }

            RefreshIrq();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
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

        [ConnectionRegionAttribute("phy")]
        public uint ReadDoubleWordOverMDIO(long offset)
        {
            this.Log(LogLevel.Noisy, "Reading from PHY: offset 0x{0:X}", offset);
            if(offset == (long)MDIORegisters.Read)
            {
                var result = bbHelper.EncodedInput
                    ? 1
                    : 0u;

                this.Log(LogLevel.Noisy, "Returning value: 0x{0:X}", result);
                return result;
            }

            this.Log(LogLevel.Warning, "Unhandled read from PHY register: 0x{0:X}", offset);
            return 0;
        }

        [ConnectionRegionAttribute("phy")]
        public void WriteDoubleWordOverMDIO(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Writing to PHY: offset 0x{0:X}, value 0x{1:X}", offset, value);

            if(offset != (long)MDIORegisters.Write)
            {
                this.Log(LogLevel.Warning, "Unhandled write to PHY register: 0x{0:X}", offset);
                return;
            }

            var dataDecoded = bbHelper.Update(value, dataBit: 2, clockBit: 0);
            if(!dataDecoded)
            {
                return;
            }

            this.Log(LogLevel.Noisy, "Got a 16-bit packet in {0} state, the value is 0x{1:X}", phyState, bbHelper.DecodedOutput);

            switch(phyState)
            {
            case PhyState.Idle:
            {
                if(bbHelper.DecodedOutput == 0xffff)
                {
                    // sync, move on
                    phyState = PhyState.Syncing;
                }
                // if not, wait for sync pattern
                break;
            }
            case PhyState.Syncing:
            {
                if(bbHelper.DecodedOutput == 0xffff)
                {
                    phyState = PhyState.WaitingForCommand;
                }
                else
                {
                    this.Log(LogLevel.Warning, "Unexpected bit pattern when syncing (0x{0:X}), returning to idle state", bbHelper.DecodedOutput);
                    phyState = PhyState.Idle;
                }
                break;
            }
            case PhyState.WaitingForCommand:
            {
                const int OpCodeRead = 0x1;
                const int OpCodeWrite = 0x2;

                var startField = bbHelper.DecodedOutput & 0x3;
                var opCode = (bbHelper.DecodedOutput >> 2) & 0x3;
                var phyAddress = (ushort)(BitHelper.ReverseBits((ushort)((bbHelper.DecodedOutput >> 4) & 0x1f)) >> 11);
                var registerAddress = (ushort)(BitHelper.ReverseBits((ushort)((bbHelper.DecodedOutput >> 9) & 0x1f)) >> 11);

                if(startField != 0x2
                    || (opCode != OpCodeRead && opCode != OpCodeWrite))
                {
                    this.Log(LogLevel.Warning, "Received an invalid PHY command: 0x{0:X}. Ignoring it", bbHelper.DecodedOutput);
                    phyState = PhyState.Idle;
                    break;
                }

                if(opCode == OpCodeWrite)
                {
                    phyState = PhyState.WaitingForData;
                    this.Log(LogLevel.Noisy, "Write command to PHY 0x{0:X}, register 0x{1:X}. Waiting for data", phyAddress, registerAddress);

                    lastPhyAddress = phyAddress;
                    lastRegisterAddress = registerAddress;
                }
                else
                {
                    ushort readValue = 0;
                    if(!TryGetPhy<ushort>(phyAddress, out var phy))
                    {
                        this.Log(LogLevel.Warning, "Trying to read from non-existing PHY #{0}", phyAddress);
                    }
                    else
                    {
                        readValue = (ushort)phy.Read(registerAddress);
                    }

                    this.Log(LogLevel.Noisy, "Read value 0x{0:X} from PHY 0x{1:X}, register 0x{2:X}", readValue, phyAddress, registerAddress);

                    bbHelper.SetInputBuffer(readValue);
                    phyState = PhyState.Idle;
                }
                break;
            }
            case PhyState.WaitingForData:
            {
                if(!TryGetPhy<ushort>(lastPhyAddress, out var phy))
                {
                    this.Log(LogLevel.Warning, "Trying to write to non-existing PHY #{0}", lastPhyAddress);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to PHY 0x{1:X}, register 0x{2:X}", bbHelper.DecodedOutput, lastPhyAddress, lastRegisterAddress);
                    phy.Write(lastRegisterAddress, (ushort)bbHelper.DecodedOutput);
                }

                phyState = PhyState.Idle;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException("Unexpected PHY state: {0}".FormatWith(phyState));
            }
        }

        public MACAddress MAC { get; set; }

        public event Action<EthernetFrame> FrameReady;

        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.ReaderEvPending.Define(this)
                .WithFlag(0, out readerEventPending, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, __) => RefreshIrq())
            ;

            Registers.WriterLength.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "writer_length", valueProviderCallback: _ => writeSlots[writerSlotNumber.Value].DataLength)
            ;

            Registers.WriterEvPending.Define(this)
                .WithFlag(0, out writerEventPending, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    if(latchedWriterSlot == -1)
                    {
                        // if no slot has been latched, release all (this might happen at startup when resetting the state)
                        foreach(var slot in writeSlots)
                        {
                            slot.Release();
                        }
                    }
                    else
                    {
                        writeSlots[latchedWriterSlot].Release();
                    }

                    latchedWriterSlot = -1;

                    // update writerSlotNumber
                    writerSlotNumber.Value = (uint)writeSlots
                        .Select((v, idx) => new { v, idx })
                        .Where(x => x.v.IsBusy)
                        .Select(x => x.idx)
                        .FirstOrDefault();

                    UpdateEvents();
                })
            ;

            Registers.ReaderSlot.Define(this)
                .WithValueField(0, 32, out readerSlotNumber, name: "reader_slot", writeCallback: (_, val) =>
                {
                    if((long)val >= readSlots.Length)
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

            Registers.ReaderLength.Define(this)
                .WithValueField(0, 32, name: "reader_length",
                    writeCallback: (_, val) =>
                    {
                        readSlots[readerSlotNumber.Value].DataLength = (uint)val;
                    },
                    valueProviderCallback: _ => readSlots[readerSlotNumber.Value].DataLength)
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
            if(!Misc.TryCreateFrameOrLogWarning(this, slot.Read(), out var frame, addCrc: true))
            {
                return;
            }

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
        private ushort lastPhyAddress;
        private ushort lastRegisterAddress;
        private PhyState phyState;
        private BitBangHelper bbHelper;

        // WARNING:
        // read slots contains packets to be sent
        // write slots contains received packets
        private readonly Slot[] writeSlots;
        private readonly Slot[] readSlots;

        private const int SlotSize = 0x0800;

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
            WriterLength       = 0x04,
            WriterErrors       = 0x08,
            WriterEvStatus     = 0x0C,
            WriterEvPending    = 0x10,
            WriterEvEnable     = 0x14,
            ReaderStart        = 0x18,
            ReaderReady        = 0x1c,
            ReaderLevel        = 0x20,
            ReaderSlot         = 0x24,
            ReaderLength       = 0x28,
            ReaderEvStatus     = 0x2c,
            ReaderEvPending    = 0x30,
            ReaderEvEnable     = 0x34,
            PreambleCRC        = 0x38,
            PreambleErrors     = 0x3c,
            CrcErrors          = 0x40
        }

        private enum MDIORegisters
        {
            Reset = 0x0,
            Write = 0x4,
            Read = 0x8
        }

        private enum PhyState
        {
            Idle,
            Syncing,
            WaitingForCommand,
            WaitingForData
        }
    }
}
