//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

namespace Antmicro.Renode.Storage
{
    public class FileStreamLimitWrapper : Stream
    {
        //
        //                                        Padding with 0's
        //                                        |
        //         Position = 0                   |   Position = length - 1
        //         |                              |   |
        //         v                              v   v
        // *-------+---------------------------*......#
        // ^       ^                           ^      ^
        // |       |                           |      |
        // |       offset                      |      |
        // |                                   |      length
        // underlying stream beginning         |
        //                                     underlying stream length
        //
        public FileStreamLimitWrapper(FileStream stream, long offset = 0, long? length = null)
        {
            if(!stream.CanSeek)
            {
                throw new ArgumentException("This wrapper is suitable only for seekable streams");
            }

            Length = length ?? (stream.Length - offset);

            if(Length < 0 || offset > Length)
            {
                throw new ArgumentException("Wrong offset/length values");
            }

            underlyingStream = stream;
            this.offset = offset;
            Position = 0;
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
                // pad the rest with 0's
                paddingSize = checked((int)Math.Min(count - bytesReadCount, Length - Position));
                Array.Clear(buffer, offset + bytesReadCount, paddingSize);
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
                Position = Length;
                break;

            default:
                throw new ArgumentException("Unexpected seek origin");
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead { get { return underlyingStream.CanRead; } }

        public override bool CanSeek { get { return underlyingStream.CanSeek; } }

        public override bool CanWrite { get { return underlyingStream.CanWrite; } }

        public override long Length { get; }

        public override long Position
        {
            get
            {
                return underlyingStream.Position - offset;
            }
            set
            {
                if(value > Length)
                {
                    throw new ArgumentException("Setting position beyond the underlying stream is unsupported");
                }
                else if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("Position");
                }

                underlyingStream.Seek(offset + value, SeekOrigin.Begin);
            }
        }

        public string Name { get { return underlyingStream.Name; } }

        private readonly FileStream underlyingStream;
        private readonly long offset;
    }
}

