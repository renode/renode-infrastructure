//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Migrant;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Storage
{
    public class SerializableStreamView : Stream, ISpeciallySerializable
    {
        //
        //    this data is not accessible         filled with padding byte
        //    |                                   |
        //    |    Position = 0                   |   Position = length - 1
        //    |    |                              |   |
        //    v    v                              v   v
        // *~~~~~~~+---------------------------*......#
        // ^       ^                           ^      ^
        // |       |                           |      |
        // |       offset                      |      |
        // |                                   |      length
        // underlying stream's beginning       |
        //                                     underlying stream's length
        //
        public SerializableStreamView(Stream stream, long? length = null, byte paddingByte = 0, long offset = 0)
        {
            if(!stream.CanSeek)
            {
                throw new ArgumentException("This wrapper is suitable only for seekable streams");
            }

            SetLength(length ?? stream.Length - offset);

            underlyingStream = stream;
            underlyingStreamOffset = offset;
            Position = 0;
            PaddingByte = paddingByte;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            underlyingStream.Dispose();
        }

        public override void Flush()
        {
            underlyingStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var paddingSize = 0;
            var bytesReadCount = underlyingStream.Read(buffer, offset, count);
            if(bytesReadCount < count)
            {
                // pad the rest with `PaddingByte`
                paddingSize = checked((int)Math.Min(count - bytesReadCount, Length - Position));
                for(var i = 0; i < paddingSize; i++)
                {
                    buffer[i + bytesReadCount + offset] = PaddingByte;
                }
                paddingOffset += paddingSize;
            }
            return bytesReadCount + paddingSize;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if(Position + count > Length)
            {
                throw new ArgumentException($"There is no more space left in stream. Asked to write {count} bytes, but only {Length - Position} are left.");
            }

            // writing to the padding area extends the file, but not longer than provided `length`
            var bytesToWriteCount = checked((int)Math.Min(count, Length - underlyingStream.Position));
            if(paddingOffset > 0)
            {
                // this effectively grows the file filling it with `PaddingByte`
                for(var i = 0; i < paddingOffset; i++)
                {
                    underlyingStream.WriteByte(PaddingByte);
                }
                paddingOffset = 0;
            }
            underlyingStream.Write(buffer, offset, bytesToWriteCount);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
            case SeekOrigin.Begin:
                Position = offset;
                break;

            case SeekOrigin.Current:
                Position += offset;
                break;

            case SeekOrigin.End:
                Position = Length + offset;
                break;

            default:
                throw new ArgumentException("Unexpected seek origin");
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            if(value < 0 || underlyingStreamOffset > value)
            {
                throw new ArgumentException("Wrong offset/length values");
            }

            this.length = value;
        }

        public void Load(PrimitiveReader reader)
        {
            var fileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            underlyingStream = new FileStream(fileName, FileMode.OpenOrCreate);
            underlyingStreamOffset = 0;

            length = reader.ReadInt64();
            PaddingByte = reader.ReadByte();
            var numberOfBytes = reader.ReadInt64();
            reader.CopyTo(underlyingStream, numberOfBytes);
            Position = reader.ReadInt64();
        }

        public void Save(PrimitiveWriter writer)
        {
            // we don't have to save offset as after deserialization we will treat it as 0
            // we don't have to save paddingOffset as it will be recalculated by setting `Position` after deserialization
            writer.Write(Length);
            writer.Write(PaddingByte);
            var initialPosition = Position;
            // this seeks to the beginning of meaningful data in the stream
            Position = 0;
            writer.Write(underlyingStream.Length - underlyingStream.Position);
            writer.CopyFrom(underlyingStream, underlyingStream.Length - underlyingStream.Position);
            Position = initialPosition;
            writer.Write(initialPosition);
        }

        public byte PaddingByte { get; private set; }

        public override bool CanRead { get { return underlyingStream.CanRead; } }

        public override bool CanSeek { get { return underlyingStream.CanSeek; } }

        public override bool CanWrite { get { return underlyingStream.CanWrite; } }

        public override long Length => length;

        public override long Position
        {
            get
            {
                return underlyingStream.Position - underlyingStreamOffset + paddingOffset;
            }
            set
            {
                if(value > Length)
                {
                    throw new ArgumentException("Setting position beyond the underlying stream is unsupported");
                }
                else if(value < 0)
                {
                    throw new ArgumentOutOfRangeException("Setting negative position is unsupported");
                }

                if(underlyingStream.Length > underlyingStreamOffset + value)
                {
                    underlyingStream.Seek(underlyingStreamOffset + value, SeekOrigin.Begin);
                    paddingOffset = 0;
                }
                else
                {
                    underlyingStream.Seek(0, SeekOrigin.End);
                    paddingOffset = value - (underlyingStream.Length - underlyingStreamOffset);
                }
            }
        }

        private SerializableStreamView()
        {
            // this is intended for deserialization only
        }

        private long paddingOffset;
        private Stream underlyingStream;
        private long underlyingStreamOffset;
        private long length;
    }
}

