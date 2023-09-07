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

        public override byte ReadByte()
        {
            return ExecuteAndHandleError(base.ReadByte);
        }

        public override byte[] ReadBytes(int count)
        {
            return ExecuteAndHandleError(delegate { return base.ReadBytes(count); });
        }

        public override char ReadChar()
        {
            return ExecuteAndHandleError(base.ReadChar);
        }

        public override char[] ReadChars(int count)
        {
            return ExecuteAndHandleError(delegate { return base.ReadChars(count); });
        }

        public override decimal ReadDecimal()
        {
            return ExecuteAndHandleError(base.ReadDecimal);
        }

        public override double ReadDouble()
        {
            return ExecuteAndHandleError(base.ReadDouble);
        }

        public override short ReadInt16()
        {
            return ExecuteAndHandleError(base.ReadInt16);
        }

        public override int ReadInt32()
        {
            return ExecuteAndHandleError(base.ReadInt32);
        }

        public override long ReadInt64()
        {
            return ExecuteAndHandleError(base.ReadInt64);
        }

        public override sbyte ReadSByte()
        {
            return ExecuteAndHandleError(base.ReadSByte);
        }

        public override float ReadSingle()
        {
            return ExecuteAndHandleError(base.ReadSingle);
        }

        public override string ReadString()
        {
            return ExecuteAndHandleError(base.ReadString);
        }

        public override ushort ReadUInt16()
        {
            return ExecuteAndHandleError(base.ReadUInt16);
        }

        public override uint ReadUInt32()
        {
            return ExecuteAndHandleError(base.ReadUInt32);
        }

        public override ulong ReadUInt64()
        {
            return ExecuteAndHandleError(base.ReadUInt64);
        }

        public string ReadCString()
        {
            return ExecuteAndHandleError(() => ((BinaryReader)this).ReadCString());
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
    }
}
