//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Xwt.Drawing;
using Xwt.Backends;
using Xwt.GtkBackend;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Extensions.Analyzers.Video
{
    internal static class BitmapImageExtensions
    {
        public static void Copy(this BitmapImage bmp, byte[] frame)
        {
            var backend = bmp.GetBackend();
            var outBuffer = ((GtkImage)backend).Frames[0].Pixbuf.Pixels;
            Marshal.Copy(frame, 0, outBuffer, frame.Length);
        }

        public static void InvertColorOfPixel(this BitmapImage img, int x, int y)
        {
            var color = img.GetPixel(x, y);
            var invertedColor = Color.FromBytes((byte)(255 * (1.0 - color.Red)), (byte)(255 * (1.0 - color.Green)), (byte)(255 * (1.0 - color.Blue)));
            img.SetPixelDirectly(x, y, invertedColor);
        }

        public static bool IsInImage(this BitmapImage img, int x, int y)
        {
            return x >= 0 && x < img.PixelWidth && y >= 0 && y < img.PixelHeight;
        }

        public static void DrawCursor(this BitmapImage img, int x, int y)
        {
            const int CursorLength = 2;
            for(var rx = -1 * CursorLength; rx <= CursorLength; rx++)
            {
                if(img.IsInImage(x + rx, y))
                {
                    img.InvertColorOfPixel(x + rx, y);
                }
            }

            for(var ry = -1 * CursorLength; ry <= CursorLength; ry++)
            {
                if(img.IsInImage(x, y + ry) && ry != 0)
                {
                    img.InvertColorOfPixel(x, y + ry);
                }
            }
        }
    }
}

