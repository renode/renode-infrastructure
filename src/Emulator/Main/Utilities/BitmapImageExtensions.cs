//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Xwt.Backends;
using Color = Xwt.Drawing.Color;
using BitmapImage = Xwt.Drawing.BitmapImage;
#if GUI_DISABLED
using Antmicro.Renode.Exceptions;
#else
    #if PLATFORM_WINDOWS
using Xwt.WPFBackend;
using System.Windows.Media;
using System.Windows.Media.Imaging;
    #else
using Xwt.GtkBackend;
using System.Runtime.InteropServices;
    #endif
#endif

namespace Antmicro.Renode.Utilities
{
    public static class BitmapImageExtensions
    {
        public static void Copy(this BitmapImage bmp, byte[] frame)
        {
#if GUI_DISABLED
            throw new RecoverableException("The BitmapImageExtensions.Copy() method is not supported in the non-gui configuration");
#else
            var backend = bmp.GetBackend();
    #if PLATFORM_WINDOWS
            var pixelFormat = PixelFormats.Bgra32;
            var stride = (int)bmp.PixelWidth * (pixelFormat.BitsPerPixel / 8);   // width * pixel size in bytes
            var dpi = 96;           // dots per inch - WPF supports automatic scaling
                                    // by using the device independent pixel as its primary unit of measurement,
                                    // which is 1/96 of an inch
            ((WpfImage)backend).MainFrame = BitmapSource.Create((int)bmp.PixelWidth, (int)bmp.PixelHeight, dpi, dpi, pixelFormat, BitmapPalettes.WebPalette, frame, stride);
    #else
            var outBuffer = ((GtkImage)backend).Frames[0].Pixbuf.Pixels;
            Marshal.Copy(frame, 0, outBuffer, frame.Length);
    #endif
#endif
        }

        public static void InvertColorOfPixel(this BitmapImage img, int x, int y)
        {
            var color = img.GetPixel(x, y);
            var invertedColor = Color.FromBytes((byte)(255 * (1.0 - color.Red)), (byte)(255 * (1.0 - color.Green)), (byte)(255 * (1.0 - color.Blue)));
            img.SetPixel(x, y, invertedColor);
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

