//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public static class BinaryReaderExtensions
    {
        public static string ReadCString(this BinaryReader @this)
        {
            var bytes = Misc.Iterate(@this.ReadByte).TakeWhile(b => b != 0x00).ToArray();
            return System.Text.Encoding.Default.GetString(bytes);
        }
    }

    public class SafeBinaryReader : BinaryReader
    {
        public SafeBinaryReader(Stream stream) : base(stream)
        {
            Length = this.BaseStream.Length;
        }

        public SafeBinaryReader WithLength(long newLength)
        {
            var newReader = new SafeBinaryReader(this.BaseStream, newLength);
            return newReader;
        }

        public override bool ReadBoolean()
        {
            return ExecuteAndHandleError(base.ReadBoolean);
        }

        public bool TryReadBoolean(out bool value)
        {
            return TryExecuteAndLogError(base.ReadBoolean, out value);
        }

        public override byte ReadByte()
        {
            return ExecuteAndHandleError(base.ReadByte);
        }

        public bool TryReadByte(out byte value)
        {
            return TryExecuteAndLogError(base.ReadByte, out value);
        }

        public override byte[] ReadBytes(int count)
        {
            return ExecuteAndHandleError(() => base.ReadBytes(count));
        }

        public bool TryReadBytes(int count, out byte[] value)
        {
            return TryExecuteAndLogError(() => base.ReadBytes(count), out value);
        }

        public override char ReadChar()
        {
            return ExecuteAndHandleError(base.ReadChar);
        }

        public bool TryReadChar(out char value)
        {
            return TryExecuteAndLogError(base.ReadChar, out value);
        }

        public override char[] ReadChars(int count)
        {
            return ExecuteAndHandleError(() => base.ReadChars(count));
        }

        public bool TryReadChars(int count, out char[] value)
        {
            return TryExecuteAndLogError(() => base.ReadChars(count), out value);
        }

        public override decimal ReadDecimal()
        {
            return ExecuteAndHandleError(base.ReadDecimal);
        }

        public bool TryReadDecimal(out decimal value)
        {
            return TryExecuteAndLogError(base.ReadDecimal, out value);
        }

        public override double ReadDouble()
        {
            return ExecuteAndHandleError(base.ReadDouble);
        }

        public bool TryReadDouble(out double value)
        {
            return TryExecuteAndLogError(base.ReadDouble, out value);
        }

        public override short ReadInt16()
        {
            return ExecuteAndHandleError(base.ReadInt16);
        }

        public bool TryReadInt16(out short value)
        {
            return TryExecuteAndLogError(base.ReadInt16, out value);
        }

        public override int ReadInt32()
        {
            return ExecuteAndHandleError(base.ReadInt32);
        }

        public bool TryReadInt32(out int value)
        {
            return TryExecuteAndLogError(base.ReadInt32, out value);
        }

        public override long ReadInt64()
        {
            return ExecuteAndHandleError(base.ReadInt64);
        }

        public bool TryReadInt64(out long value)
        {
            return TryExecuteAndLogError(base.ReadInt64, out value);
        }

        public override sbyte ReadSByte()
        {
            return ExecuteAndHandleError(base.ReadSByte);
        }

        public bool TryReadSByte(out sbyte value)
        {
            return TryExecuteAndLogError(base.ReadSByte, out value);
        }

        public override float ReadSingle()
        {
            return ExecuteAndHandleError(base.ReadSingle);
        }

        public bool TryReadSingle(out float value)
        {
            return TryExecuteAndLogError(base.ReadSingle, out value);
        }

        public override string ReadString()
        {
            return ExecuteAndHandleError(base.ReadString);
        }

        public bool TryReadString(out string value)
        {
            return TryExecuteAndLogError(base.ReadString, out value);
        }

        public override ushort ReadUInt16()
        {
            return ExecuteAndHandleError(base.ReadUInt16);
        }

        public bool TryReadUInt16(out ushort value)
        {
            return TryExecuteAndLogError(base.ReadUInt16, out value);
        }

        public override uint ReadUInt32()
        {
            return ExecuteAndHandleError(base.ReadUInt32);
        }

        public bool TryReadUInt32(out uint value)
        {
            return TryExecuteAndLogError(base.ReadUInt32, out value);
        }

        public override ulong ReadUInt64()
        {
            return ExecuteAndHandleError(base.ReadUInt64);
        }

        public bool TryReadUInt64(out ulong value)
        {
            return TryExecuteAndLogError(base.ReadUInt64, out value);
        }

        public string ReadCString()
        {
            return ExecuteAndHandleError(() => ((BinaryReader)this).ReadCString());
        }

        public bool TryReadCString(out string value)
        {
            return TryExecuteAndLogError(() => ((BinaryReader)this).ReadCString(), out value);
        }

        public bool SkipBytes(long count)
        {
            var previousPosition = this.BaseStream.Position;
            if(previousPosition + count > Length)
            {
                return false;
            }

            var currentPosition = this.BaseStream.Seek(count, SeekOrigin.Current);
            if(previousPosition + count > currentPosition)
            {
                EndOfStreamEvent?.Invoke("Stream ended when skipping bytes");
            }

            return (previousPosition + count) == currentPosition;
        }

        public bool SeekToEnd()
        {
            if(this.BaseStream.Position == Length)
            {
                return false;
            }
            this.BaseStream.Seek(Length, SeekOrigin.Begin);
            return true;
        }

        public long Length { get; }
        public bool EOF => BaseStream.Position >= Length;
        public IDisposable Checkpoint
        {
            get
            {
                var currentPosition = this.BaseStream.Position;
                return DisposableWrapper.New(() => this.BaseStream.Seek(currentPosition, SeekOrigin.Begin));
            }
        }

        public Action<string> EndOfStreamEvent;

        private SafeBinaryReader(Stream stream, long length) : this(stream)
        {
            Length = length;
        }

        private T ExecuteAndHandleError<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch(EndOfStreamException e)
            {
                EndOfStreamEvent?.Invoke(e.Message);
                return default(T);
            }
        }

        private bool TryExecuteAndLogError<T>(Func<T> func, out T value)
        {
            try
            {
                value = func();
                return true;
            }
            catch(EndOfStreamException e)
            {
                EndOfStreamEvent?.Invoke(e.Message);
                value = default(T);
                return false;
            }
        }
    }
}
