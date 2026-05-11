//
// Copyright (c) 2010-2026 Antmicro
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
using Xwt.GtkBackend;

using Antmicro.Renode.Core;

using System.Runtime.InteropServices;
#endif
using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public static class BitmapImageExtensions
    {
        static BitmapImageExtensions()
        {
#if !GUI_DISABLED
            if(!RuntimeInfo.IsWindows())
#endif
            {
                return;
            }
            // We can't reference WPF directly because Renode is built as a non-Windows target (and WPF is a Windows-only target dependency), but let's use dynamic import to get what we need for `Copy`
            Assembly assembly;
            try
            {
                assembly = Assembly.Load("PresentationCore");
            }
            catch(Exception ex)
            {
                Logger.Log(LogLevel.Error, "Failed to load WPF: {0}", ex);
                return;
            }
            WpfBgra32 = assembly.GetType("System.Windows.Media.PixelFormats").GetProperty("Bgra32").GetValue(null);
            var method = assembly.GetType("System.Windows.Media.Imaging.BitmapSource")
                .GetMethods()
                .Where(v => v.Name == "Create" && v.GetParameters().Length == 8)
                .Single();
            var methodType = Expression.GetDelegateType(
                method.GetParameters().Append(method.ReturnParameter).Select(v => v.ParameterType).ToArray()
            );
            WpfBitmapSourceCreate = method.CreateDelegate(methodType);

            WpfWebPalette = assembly.GetType("System.Windows.Media.Imaging.BitmapPalettes").GetProperty("WebPalette").GetValue(null);
        }

        public static void Copy(this BitmapImage bmp, byte[] frame)
        {
#if GUI_DISABLED
            throw new RecoverableException("The BitmapImageExtensions.Copy() method is not supported in the non-gui configuration");
#else
            var backend = bmp.GetBackend();
            if(RuntimeInfo.IsWindows())
            {
                var stride = (int)bmp.PixelWidth * (WpfBgra32.BitsPerPixel / 8);   // width * pixel size in bytes
                var dpi = 96;           // dots per inch - WPF supports automatic scaling
                                        // by using the device independent pixel as its primary unit of measurement,
                                        // which is 1/96 of an inch
                ((dynamic)backend).MainFrame = WpfBitmapSourceCreate((int)bmp.PixelWidth, (int)bmp.PixelHeight, dpi, dpi, WpfBgra32, WpfWebPalette, frame, stride);
            }
            else
            {
                var outBuffer = ((GtkImage)backend).Frames[0].Pixbuf.Pixels;
                Marshal.Copy(frame, 0, outBuffer, frame.Length);
            }
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
            const int cursorLength = 2;
            for(var rx = -1 * cursorLength; rx <= cursorLength; rx++)
            {
                if(img.IsInImage(x + rx, y))
                {
                    img.InvertColorOfPixel(x + rx, y);
                }
            }

            for(var ry = -1 * cursorLength; ry <= cursorLength; ry++)
            {
                if(img.IsInImage(x, y + ry) && ry != 0)
                {
                    img.InvertColorOfPixel(x, y + ry);
                }
            }
        }

        private static readonly dynamic WpfBgra32;
        private static readonly dynamic WpfBitmapSourceCreate;
        private static readonly dynamic WpfWebPalette;
    }
}