//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.MTD
{
    [Icon("sd")]
    public sealed class CFIFlash : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize, IDisposable
    {
        public CFIFlash(string fileName, int? size = null, SysbusAccessWidth bits = SysbusAccessWidth.DoubleWord, bool nonPersistent = false)
        {
            switch(bits)
            {
            case SysbusAccessWidth.Byte:
                busWidth = 0;
                break;
            case SysbusAccessWidth.Word:
                busWidth = 1;
                break;
            case SysbusAccessWidth.DoubleWord:
                busWidth = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }
            Init(fileName, size, nonPersistent);
            CheckBuffer(0);
        }

        public long Size
        {
            get
            {
                return size;
            }
        }

        /// <summary>
        /// Gets or sets the size of the erase block.
        /// </summary>
        /// <value>
        /// The size of the erase block in bytes.
        /// </value>
        /// <exception cref='ArgumentException'>
        /// Thrown when erase block size is not divisible by 256 or greater than 16776960 or it
        /// does not divide whole flash size.
        /// </exception>
        public int EraseBlockSize
        {
            get
            {
                return 256 * eraseBlockSizeDivided;
            }
            set
            {
                if(value % 256 != 0)
                {
                    throw new ArgumentException("Erase block size has to be divisible by 256.");
                }
                if(size % value != 0)
                {
                    throw new ArgumentException(string.Format(
                        "Erase block has to divide flash size, which is {0}B.", Misc.NormalizeBinary(size))
                    );
                }
                if(size / value > ushort.MaxValue + 1)
                {
                    throw new ArgumentException(string.Format(
                        "Erase block cannot be smaller than {0}B for given flash size {1}B.",
                        Misc.NormalizeBinary(size / (ushort.MaxValue + 1)),
                        Misc.NormalizeBinary(Size))
                    );
                }
                if(value / 256 > ushort.MaxValue)
                {
                    throw new ArgumentException(string.Format(
                        "Erase block cannot be larger than {0}B ({2}B was given) for given flash size {1}B.",
                        256 * ushort.MaxValue,
                        Misc.NormalizeBinary(Size),
                        value)
                    );
                }
                eraseBlockSizeDivided = (ushort)(value / 256);
                eraseBlockCountMinusOne = (ushort)(size / value - 1);
            }
        }

        public byte ReadByte(long offset)
        {
            switch(state)
            {
            case State.ReadArray:
                return (byte)HandleRead(offset, 0);
            case State.ReadQuery:
                return HandleQuery(offset);
            case State.ActionDone:
                return (byte)statusRegister;
            case State.WaitingForElementCount:
                return (byte)statusRegister;
            case State.ReadJEDECId:
                return HandleReadJEDEC(offset);
            }
            this.Log(LogLevel.Warning, string.Format(
                "Read @ unknown state {1}, offset 0x{0:X}", offset, state)
            );
            return 0;
        }

        public ushort ReadWord(long offset)
        {
            if(state == State.ReadArray)
            {
                return (ushort)HandleRead(offset, 1);
            }
            offset >>= (int)busWidth;
            return ReadByte(offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(state == State.ReadArray)
            {
                return HandleRead(offset, 2);
            }
            offset >>= (int)busWidth;
            return ReadByte(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            switch(state)
            {
            case State.ProgramWish:
                HandleProgramByte(offset, value, 0);
                return;
            case State.WaitingForElementCount:
                SetupWriteBuffer(offset, value);
                return;
            case State.WaitingForBufferData:
                HandleMultiByteWrite(offset, value, 0);
                return;
            case State.WaitingForWriteConfirm:
                HandleWriteConfirm(offset, value);
                return;
            }
            switch(value)
            {
            case Command.ReadArray:
                state = State.ReadArray;
                return;
            case Command.ReadQuery:
                state = State.ReadQuery;
                return;
            case Command.EraseBlock:
                state = State.EraseWish;
                return;
            case Command.ProgramByte:
                state = State.ProgramWish;
                return;
            case Command.ReadJEDECId: // TODO
                state = State.ReadJEDECId;
                return;
            case Command.ReadStatus:
                state = State.ActionDone;
                return;
            case Command.SetupWriteBuffer:
                state = State.WaitingForElementCount;
                statusRegister |= StatusRegister.ActionDone;
                return;
            case Command.Confirm:
                switch(state)
                {
                case State.EraseWish:
                    HandleErase(offset);
                    state = State.ActionDone;
                    return;
                default:
                    this.Log(LogLevel.Warning,
                        "Confirm command sent in improper state {0}.", state);
                    break;
                }
                return;
            case Command.ClearStatus:
                statusRegister = 0;
                this.NoisyLog("Status register cleared.", offset, EraseBlockSize);
                return;
            }
            this.Log(LogLevel.Warning,
                "Unknown command written @ offset 0x{0:X}, value 0x{1:X}.", offset, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            switch(state)
            {
            case State.ProgramWish:
                HandleProgramByte(offset, value, 1);
                break;
            case State.WaitingForBufferData:
                HandleMultiByteWrite(offset, value, 1);
                break;
            default:
                WriteByte(offset, (byte)value);
                break;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch(state)
            {
            case State.ProgramWish:
                HandleProgramByte(offset, value, 2);
                break;
            case State.WaitingForBufferData:
                HandleMultiByteWrite(offset, value, 2);
                break;
            default:
                WriteByte(offset, (byte)value);
                break;
            }
        }

        public void Reset()
        {
            FlushBuffer();
            writeBuffer = null;
            buffer = new byte[DesiredBufferSize];
            currentBufferSize = 0;
            currentBufferStart = 0;
            state = State.ReadArray;
            CheckBuffer(0);
        }

        public void Dispose()
        {
            this.NoisyLog("Dispose: flushing buffer and closing underlying stream.");
            FlushBuffer();
            stream.Dispose();
        }

        private void Init(string fileName, int? requestedSize, bool nonPersistent)
        {
            if(nonPersistent)
            {
                var tempFile = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(fileName, tempFile, true);
                fileName = tempFile;
            }
            // if `requestedSize` is `null`, the file lenght will be used
            stream = new SerializableStreamView(new FileStream(fileName, FileMode.OpenOrCreate), requestedSize, 0xFF);
            size = (int)stream.Length;
            CheckSize(size, requestedSize);
            size2n = (byte)Misc.Logarithm2(size);
            buffer = new byte[DesiredBufferSize];
            // default erase block is whole flash or 256KB
            EraseBlockSize = Math.Min(size, DefaultEraseBlockSize);
        }

        private uint HandleRead(long offset, int width)
        {
            CheckBuffer(offset);
            var localOffset = offset - currentBufferStart;
            var returnValue = (uint)buffer[localOffset];
            if(width > 0)
            {
                returnValue |= ((uint)buffer[localOffset + 1] << 8);
            }
            if(width > 1)
            {
                returnValue |= ((uint)buffer[localOffset + 2] << 16);
                returnValue |= ((uint)buffer[localOffset + 3] << 24);
            }
            return returnValue;
        }

        private byte HandleReadJEDEC(long offset)
        {
            switch(offset)
            {
            case 0:
                return 0x89;
            case 1:
                return 0x18;
            default:
                this.Log(LogLevel.Warning, "Read @ unsupported offset 0x{0:X} while in state {1}.", offset, state);
                return 0;
            }
        }

        private byte HandleQuery(long offset)
        {
            //TODO: enum/const!!!
            this.NoisyLog("Query at 0x{0:X}.", offset);
            switch(offset)
            {
            case 0x10: //Q
                return 0x51;
            case 0x11: //R
                return 0x52;
            case 0x12: //Y
                return 0x59;
            case 0x13:
                return 0x03; // Intel command set
            case 0x15:
                return 0x31;
            case 0x1B:
                return 0x45;
            case 0x1C:
                return 0x55;
            case 0x1F:
                return 0x07; // timeout
            case 0x20:
                return 0x07;
            case 0x21:
                return 0x0A;
            case 0x23:
                return 0x04; // timeout 2
            case 0x24:
                return 0x04;
            case 0x25:
                return 0x04;
            case 0x27:
                return size2n; // size
            case 0x28:
                return 0x02;
            case 0x2A:
                return 8; // write buffer size
            case 0x2C:
                return 0x01; // no of erase block regions, currently one
            case 0x2D:
                return unchecked((byte)(eraseBlockCountMinusOne));
            case 0x2E:
                return unchecked((byte)((eraseBlockCountMinusOne) >> 8));
            case 0x2F:
                return unchecked((byte)eraseBlockSizeDivided);
            case 0x30:
                return unchecked((byte)(eraseBlockSizeDivided >> 8));
            case 0x31:
                return 0x50;
            case 0x32:
                return 0x52;
            case 0x33:
                return 0x49;
            case 0x34:
                return 0x31;
            case 0x35:
                return 0x31;
            default:
                return 0x00;
            }
        }

        private void HandleErase(long offset)
        {
            if(!IsBlockAddress(offset))
            {
                this.Log(LogLevel.Error,
                    "Block address given to erase is not block address; given was 0x{0:X}. Cancelling erase.", offset);
                statusRegister |= StatusRegister.EraseOrClearLockError;
                return;
            }
            var inBuffer = false;
            if(currentBufferStart <= offset && currentBufferStart + currentBufferSize >= offset + EraseBlockSize)
            {
                // we can handle erase in the current buffer
                inBuffer = true;
                for(var i = 0; i < EraseBlockSize; i++)
                {
                    buffer[offset - currentBufferStart + i] = 0xFF;
                }
            }
            else
            {
                FlushBuffer();
                DiscardBuffer();
                stream.Seek(offset, SeekOrigin.Begin);
                var a = new byte[EraseBlockSize];
                for(var i = 0; i < a.Length; i++)
                {
                    a[i] = 0xFF;
                }
                stream.Write(a, 0, EraseBlockSize);
                CheckBuffer(offset);
            }
            statusRegister |= StatusRegister.ActionDone;
            this.NoisyLog(
                "Erased block @ offset 0x{0:X}, size 0x{1:X} ({2}).", offset, EraseBlockSize, inBuffer ? "in buffer" : "in stream");
        }

        private void HandleProgramByte(long offset, uint value, int width)
        {
            CheckBuffer(offset);
            dirty = true;
            var index = offset - currentBufferStart;
            WriteLikeFlash(ref buffer[index], (byte)value);
            if(width > 0)
            {
                WriteLikeFlash(ref buffer[index + 1], (byte)(value >> 8));
            }
            if(width > 1)
            {
                WriteLikeFlash(ref buffer[index + 2], (byte)(value >> 16));
                WriteLikeFlash(ref buffer[index + 3], (byte)(value >> 24));
            }
            state = State.ActionDone;
            statusRegister |= StatusRegister.ActionDone;
            this.NoisyLog("Programmed byte @ offset 0x{0:X}, value 0x{1:X}.", offset, value);
        }

        private void HandleWriteConfirm(long offset, byte value)
        {
            if(value != Command.Confirm)
            {
                // discarding data
                this.NoisyLog("Discarded buffer of size {0}B.", Misc.NormalizeBinary(writeBuffer.Length));
                writeBuffer = null;
                state = State.ReadArray;
                return;
            }
            if(offset >= currentBufferStart &&
                writeBufferStart - currentBufferStart + writeBuffer.Length <= currentBufferSize)
            {
                writeBuffer.CopyTo(buffer, writeBufferStart - currentBufferStart);
                dirty = true;
                this.NoisyLog("Programmed buffer (with delayed write) of {0}B at 0x{1:X}.",
                    Misc.NormalizeBinary(writeBuffer.Length), writeBufferStart);
            }
            else
            {
                FlushBuffer();
                DiscardBuffer(); // to assure consistency
                stream.Seek(writeBufferStart, SeekOrigin.Begin);
                stream.Write(writeBuffer, 0, writeBuffer.Length);
                this.NoisyLog("Programmed buffer (with direct write) of {0}B at 0x{1:X}.",
                    Misc.NormalizeBinary(writeBuffer.Length), writeBufferStart);
            }
            writeBuffer = null;
            statusRegister |= StatusRegister.ActionDone;
            state = State.ActionDone;
            writtenCount = 0;
        }

        private void HandleMultiByteWrite(long offset, uint value, int width)
        {
            if(writtenCount == 0)
            {
                writeBufferStart = offset;
                if(likeFlashProgramming)
                {
                    // maybe we can read from buffer
                    if(currentBufferStart <= offset && currentBufferStart + currentBufferSize >= writeBuffer.Length + offset)
                    {
                        for(var i = 0; i < writeBuffer.Length; i++)
                        {
                            writeBuffer[i] = buffer[offset - currentBufferStart + i];
                        }
                        this.NoisyLog("Write buffer filled from buffer.");
                    }
                    else
                    {
                        FlushBuffer();
                        stream.Seek(offset, SeekOrigin.Begin);
                        var read = stream.Read(writeBuffer, 0, writeBuffer.Length);
                        if(read != writeBuffer.Length)
                        {
                            this.Log(LogLevel.Error,
                                "Error while reading data to fill write buffer. Read {0}, but requested {1}.",
                                read, writeBuffer.Length);
                        }
                        this.NoisyLog("Write buffer filled from stream.");
                    }
                }
            }
#if DEBUG
            if(offset != (writeBufferStart + writtenCount))
            {
                this.Log(LogLevel.Error,
                    "Non continous write using write buffer, offset 0x{0:X} but buffer start 0x{1:X} and written count 0x{2:X}." +
                    "Possibility of data corruption.", offset, writeBufferStart, writtenCount);
            }
#endif
            WriteLikeFlash(ref writeBuffer[writtenCount], (byte)value);
            writtenCount++;
            if(width > 0)
            {
                WriteLikeFlash(ref writeBuffer[writtenCount], (byte)(value >> 8));
                writtenCount++;
            }
            if(width > 1)
            {
                WriteLikeFlash(ref writeBuffer[writtenCount], (byte)(value >> 16));
                WriteLikeFlash(ref writeBuffer[writtenCount + 1], (byte)(value >> 24));
                writtenCount += 2;
            }
            if(writtenCount == writeBuffer.Length)
            {
                state = State.WaitingForWriteConfirm;
            }
        }

        private void SetupWriteBuffer(long offset, byte value)
        {
            var size = ((value + 1) << (int)busWidth);
            this.NoisyLog("Setting up write buffer of size {0}.", size);
            writeBuffer = new byte[size];
            state = State.WaitingForBufferData;
            writeBufferStart = offset;
        }

        private bool IsBlockAddress(long offset)
        {
            return offset % EraseBlockSize == 0;
        }

        private void CheckSize(int sizeToCheck, int? requestedSize)
        {
            if(sizeToCheck == 0 && !requestedSize.HasValue)
            {
                // most probably we've just created an empty file
                throw new ConstructionException("Size must be provided when creating a new backend file");
            }
            if(sizeToCheck < 256)
            {
                throw new ConstructionException("Size cannot be less than 256B.");
            }
            if((sizeToCheck & (sizeToCheck - 1)) != 0)
            {
                throw new ConstructionException("Size has to be power of two.");
            }
        }

        private void CheckBuffer(long offset)
        {
            if(offset >= currentBufferStart && offset < currentBufferStart + currentBufferSize)
            {
                return;
            }
            FlushBuffer();
            var alignedAddress = offset & (~3);
            this.NoisyLog("Reloading buffer at 0x{0:X}.", alignedAddress);
            stream.Seek(alignedAddress, SeekOrigin.Begin);
            currentBufferStart = alignedAddress;
            currentBufferSize = unchecked((int)Math.Min(DesiredBufferSize, size - alignedAddress));
            var read = stream.Read(buffer, 0, currentBufferSize);
            if(read != currentBufferSize)
            {
                this.Log(LogLevel.Error, "Error while reading from file: read {0}B, but {1}B requested.",
                    read, currentBufferSize);
            }
        }

        private void FlushBuffer()
        {
            if(buffer == null || !dirty)
            {
                return;
            }
            this.NoisyLog("Buffer flushed.");
            stream.Seek(currentBufferStart, SeekOrigin.Begin);
            stream.Write(buffer, 0, currentBufferSize);
            dirty = false;
        }

        private void DiscardBuffer()
        {
            this.NoisyLog("Buffer discarded.");
            currentBufferStart = 0;
            currentBufferSize = 0;
            dirty = false;
        }

        private void WriteLikeFlash(ref byte where, byte what)
        {
            if(likeFlashProgramming)
            {
                where &= what;
            }
            else
            {
                where = what;
            }
        }

        private class Command
        {
            public const byte ReadArray = 0xFF;
            public const byte ReadQuery = 0x98;
            public const byte ReadJEDECId = 0x90;
            public const byte ReadStatus = 0x70;
            public const byte ClearStatus = 0x50;
            public const byte EraseBlock = 0x20;
            public const byte Confirm = 0xD0;
            public const byte ProgramByte = 0x40;
            public const byte PageLock = 0x60;
            public const byte PageUnlock = 0x60;
            public const byte SetupWriteBuffer = 0xE8;
        }

        private enum State : int
        {
            ReadArray = 0,
            ReadStatus,
            ReadQuery,
            ReadJEDECId,
            EraseWish,
            ProgramWish,
            WaitingForElementCount,
            WaitingForBufferData,
            WaitingForWriteConfirm,
            ActionDone
        }

        [Flags]
        private enum StatusRegister : byte
        {
            Protected = 2,
            WriteOrSetLockError = 16,
            EraseOrClearLockError = 32,
            ActionDone = 128
        }

        private const int DesiredBufferSize = 100 * 1024;
        private const int DefaultEraseBlockSize = 256 * 1024;
        private const int CopyBufferSize = 256 * 1024;
        private long currentBufferStart;
        private int currentBufferSize;
        private byte[] buffer;
        private bool dirty;
        private byte[] writeBuffer;
        private long writeBufferStart;
        private int writtenCount;
        private ushort eraseBlockSizeDivided;
        private ushort eraseBlockCountMinusOne;
        private StatusRegister statusRegister;
        private byte size2n;
        private int size;
        private State state;
        private SerializableStreamView stream;
        private readonly int busWidth;
        private static readonly bool likeFlashProgramming = true;
    }
}

