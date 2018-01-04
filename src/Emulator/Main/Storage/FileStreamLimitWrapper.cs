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
        public FileStreamLimitWrapper(FileStream stream, long offset = 0, long? length = null)
        {
            if(!stream.CanSeek)
            {
                throw new ArgumentException("This wrapper is suitable only for seekable streams");
            }
            stream.Seek(offset, SeekOrigin.Begin);

            this.length = length;
            this.offset = offset;
            underlyingStream = stream;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var us = underlyingStream;
            if(us != null)
            {
                us.Dispose();
            }
        }

        public override void Flush()
        {
            underlyingStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(length.HasValue)
            {
                var maxBytesToReadLeft = (int)(this.offset + this.length - underlyingStream.Position);
                return underlyingStream.Read(buffer, offset, Math.Min(count, maxBytesToReadLeft));
            }
            else
            {
                return underlyingStream.Read(buffer, offset, count);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
            case SeekOrigin.Begin:
                Position = offset;
                return offset;

            case SeekOrigin.Current:
                Position += offset;
                return Position;

            case SeekOrigin.End:
                if(length.HasValue)
                {
                    Position = this.offset + this.length.Value + offset;
                    return Position;
                }
                else
                {
                    Position = Math.Max(this.offset, underlyingStream.Length + offset);
                    return Position;
                }
            }

            return -1;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if(length.HasValue)
            {
                var maxBytesToWriteLeft = (int)(this.offset + this.length - underlyingStream.Position);
                if(count > maxBytesToWriteLeft)
                {
                    throw new ArgumentException(string.Format("There is no more space left in stream. Asked to write {0} bytes, but only {1} are left.", count, maxBytesToWriteLeft));
                }
            }

            underlyingStream.Write(buffer, offset, count);
        }

        public override bool CanRead { get { return underlyingStream.CanRead; } }

        public override bool CanSeek { get { return underlyingStream.CanSeek; } }

        public override bool CanWrite { get { return underlyingStream.CanWrite; } }

        public override long Length { get { return length ?? underlyingStream.Length; } }

        public override long Position
        {
            get
            {
                return underlyingStream.Position - offset;
            }
            set
            {
                if(length.HasValue && value > length)
                {
                    throw new ArgumentException("Setting position beyond the underlying stream is unsupported");
                }
                else if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("Position");
                }

                underlyingStream.Position = offset + value;
            }
        }

        public long AbsolutePosition { get { return underlyingStream.Position; } }

        public string Name { get { return underlyingStream.Name; } }

        private readonly FileStream underlyingStream;
        private readonly long? length;
        private readonly long offset;
    }
}

