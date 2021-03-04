//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.IO;
using BigGustave;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public struct RawImageData
    {
        public RawImageData(byte[] bytes, int width, int height)
        {
            if(bytes.Length != width * height * PixelFormat.GetColorDepth())
            {
                throw new RecoverableException("Number of bytes does not correspond with specified dimensions.");
            }
            Bytes = bytes;
            Width = width;
            Height = height;
        }

        public Stream ToPng()
        {
            var stream = new MemoryStream();
            var builder = PngBuilder.Create(Width, Height, false);
            for(int y = 0; y < Height; ++y)
            {
                for(int x = 0; x < Width; ++x)
                {
                    var p = (Width * y + x) * 4;
                    builder.SetPixel(new BigGustave.Pixel(Bytes[p], Bytes[p + 1], Bytes[p + 2], Bytes[p + 3], false), x, y);
                }
            }
            builder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public byte[] Bytes { get; }
        public int Width { get; }
        public int Height { get; }
        public const PixelFormat PixelFormat = Antmicro.Renode.Backends.Display.PixelFormat.RGBA8888;
    }
}
