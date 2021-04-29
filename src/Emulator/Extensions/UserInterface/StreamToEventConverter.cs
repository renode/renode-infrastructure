//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

namespace Antmicro.Renode.UserInterface
{
    public class StreamToEventConverter : Stream
    {
        public StreamToEventConverter()
        {
        }

        public event Action<byte[]> BytesWritten;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if(IgnoreWrites)
            {
                return;
            }
            var bytesWritten = BytesWritten;
            var realData = new byte[count];
            Array.Copy(buffer, offset, realData, 0, count);
            bytesWritten(realData);
        }

        public bool IgnoreWrites { get; set; }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}

