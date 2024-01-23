//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Storage;

namespace Antmicro.Renode.Peripherals.MTD
{
    public static class SamsungK9NANDFlashExtensions
    {
        public static void SamsungK9NANDFlashFromFile(this IMachine machine, string fileName, ulong busAddress, string name,
            bool nonPersistent = false, byte? partId = null, byte? manufacturerId = null)
        {
            SamsungK9NANDFlash flash;
            try
            {
                flash = new SamsungK9NANDFlash(machine, fileName, nonPersistent, partId, manufacturerId);
            }
            catch(Exception e)
            {
                throw new ConstructionException($"Could not create {nameof(SamsungK9NANDFlash)}: {e.Message}", e);
            }
            machine.SystemBus.Register(flash, new BusRangeRegistration(busAddress, (ulong)flash.Size));
            machine.SetLocalName(flash, name);
        }
    }

    public sealed class SamsungK9NANDFlash : BasicBytePeripheral, IKnownSize, IDisposable
    {
        public SamsungK9NANDFlash(IMachine machine, string fileName, bool nonPersistent = false,
            byte? partId = null, byte? manufacturerId = null) : base(machine)
        {
            this.partId = partId ?? DefaultPartId;
            this.manufacturerId = manufacturerId ?? DefaultManufacturerId;
            backingStream = DataStorage.Create(fileName, persistent: !nonPersistent, paddingByte: ErasedValue);
            addressBytes = new byte[AddressCycles];
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            state = State.ReadMemory;
            idReadOffset = 0;
            statusRegister = StatusFlags.WriteEnabled | StatusFlags.Ready;
        }

        public void Dispose()
        {
            backingStream.Dispose();
        }

        // Note that this is not the size of the flash memory, but the size of its register window. This
        // flash is not memory-mapped.
        public long Size => 0x10;

        protected override void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ =>
                {
                    switch(state)
                    {
                        case State.ReadMemory:
                            return HandleReadByte();
                        case State.ReadStatus:
                            return (byte)statusRegister;
                        case State.ReadID:
                            return HandleReadID(idReadOffset++);
                        case State.ReadOob:
                            return ErasedValue;
                        default:
                            this.Log(LogLevel.Warning, "Data register read in unexpected state {0}", state);
                            return 0;
                    }
                }, writeCallback: (_, value) =>
                {
                    switch(state)
                    {
                        case State.Programming:
                            HandleProgramByte((byte)value);
                            return;
                        default:
                            this.WarningLog("Data register written with 0x{0:x2} in unexpected state {1}", value, state);
                            return;
                    }
                });

            Registers.Address.Define(this)
                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(addressCycle < AddressCycles)
                    {
                        addressBytes[addressCycle++] = (byte)value;
                    }
                    else
                    {
                        this.WarningLog("Too long ({0} bytes) address write: 0x{1:x2}", ++addressCycle, value);
                        return;
                    }

                    if(state == State.Erasing && addressCycle == EraseAddressCycles) // 3-byte address for erase
                    {
                        var memoryOffset = addressBytes[0] | (uint)addressBytes[1] << 8 | (uint)addressBytes[2] << 16;
                        memoryOffset *= (uint)EraseBlockSize / 32; // The block number shifted into the device is multiplied by 32
                        SeekStream(memoryOffset);
                    }
                    else if(addressCycle == AddressCycles) // 5-byte address for read/write
                    {
                        var column = addressBytes[0] | (uint)addressBytes[1] << 8;
                        var row = addressBytes[2] | (uint)addressBytes[3] << 8 | (uint)addressBytes[4] << 16;
                        var memoryOffset = row * RowWidth + column;
                        SeekStream(memoryOffset);
                    }
                });

            Registers.Command.Define(this)
                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                {
                    // Get ready for a new address to be shifted in
                    addressCycle = 0;

                    switch((Command)value)
                    {
                        case Command.ReadMemory1: // some flash chips might only have 1 command cycle for reading, this supports both
                        case Command.ReadMemory2:
                            state = State.ReadMemory;
                            return;
                        case Command.BlockErase1:
                            state = State.Erasing;
                            return;
                        case Command.BlockErase2:
                            HandleErase();
                            state = State.ReadStatus;
                            return;
                        case Command.PageProgram1:
                            state = State.Programming;
                            return;
                        case Command.ReadID:
                            state = State.ReadID;
                            return;
                        case Command.ReadOob:
                            state = State.ReadOob;
                            return;
                        case Command.PageProgram2: // do nothing, we already programmed on the #1 command
                        case Command.ReadStatus:
                            state = State.ReadStatus;
                            return;
                        case Command.Reset:
                            Reset();
                            return;
                        default:
                            this.WarningLog("Unknown command written, value 0x{0:x2}", value);
                            return;
                    }
                });
        }

        private byte HandleReadID(long offset)
        {
            switch(offset)
            {
                case 0:
                    return manufacturerId;
                case 1:
                    return partId;
                default:
                    this.Log(LogLevel.Warning, "ID Read at unsupported offset 0x{0:x} (tried to read extended ID?)", offset);
                    return 0;
            }
        }

        private void SeekStream(long offset)
        {
            if(!IsValidOffset(offset))
            {
                this.WarningLog("Tried to seek to invalid offset 0x{0:x}", offset);
                return;
            }
            backingStream.Seek(offset, SeekOrigin.Begin);
        }

        private void HandleErase()
        {
            var offset = backingStream.Position;
            if(!IsValidOffset(offset) || offset % EraseBlockSize != 0)
            {
                this.ErrorLog("Invalid (or not divisible by 0x{0:x}) block erase address given: 0x{1:x}", EraseBlockSize, offset);
                statusRegister |= StatusFlags.ProgramEraseError;
                return;
            }

            backingStream.Write(Enumerable.Repeat(ErasedValue, EraseBlockSize).ToArray(), 0, EraseBlockSize);
            backingStream.Seek(offset, SeekOrigin.Begin);
            this.NoisyLog("Erased block at offset 0x{0:x}, size 0x{1:x}", offset, EraseBlockSize);
        }

        private byte HandleReadByte()
        {
            if(!IsValidOffset(backingStream.Position))
            {
                return ErasedValue;
            }
            return (byte)backingStream.ReadByte();
        }

        private void HandleProgramByte(byte value)
        {
            var offset = backingStream.Position;
            if(!IsValidOffset(offset))
            {
                return;
            }

            var flashCellState = (byte)(backingStream.ReadByte() & value);
            backingStream.Seek(offset, SeekOrigin.Begin);
            backingStream.WriteByte(flashCellState);
            this.NoisyLog("Programmed byte at offset 0x{0:x} to 0x{1:x}{2}", offset, flashCellState,
                value == flashCellState ? "" : " using flash behavior");
        }

        private bool IsValidOffset(long offset)
        {
            return offset >= 0 && offset < backingStream.Length;
        }

        // The buffer size must be larger than the erase block size
        private const int BufferSize = 100 * 1024;
        private const int EraseBlockSize = 16 * 1024;
        private const int DataBytesPerRow = 512;
        private const int SpareBytesPerRow = 32;
        private const int RowWidth = DataBytesPerRow + SpareBytesPerRow;
        private const byte ErasedValue = 0xff;
        private const byte DefaultManufacturerId = 0xec;
        private const byte DefaultPartId = 0xd5;
        // Address cycles for read and write
        private const int AddressCycles = 5;
        // Address cycles for block erase
        private const int EraseAddressCycles = 3;
        private readonly byte partId;
        private readonly byte manufacturerId;
        private readonly byte[] addressBytes;
        private readonly Stream backingStream;

        private StatusFlags statusRegister;
        private State state;
        private uint idReadOffset;
        private int addressCycle;

        private enum Command : byte
        {
            ReadID = 0x90,
            ReadStatus = 0x70,
            ReadOob = 0x50,
            ReadMemory1 = 0x00,
            ReadMemory2 = 0x30,
            PageProgram1 = 0x80,
            PageProgram2 = 0x10,
            BlockErase1 = 0x60,
            BlockErase2 = 0xD0,
            Reset = 0xFF,
        }

        [Flags]
        private enum StatusFlags : byte
        {
            ProgramEraseError = 0x01,
            Ready = 0x40,
            WriteEnabled = 0x80,
        }

        private enum State
        {
            ReadID,
            ReadMemory,
            ReadOob,
            Erasing,
            Programming,
            ReadStatus
        }

        private enum Registers
        {
            Data = 0x00,
            Address = 0x04,
            Command = 0x08,
        }
    }
}
