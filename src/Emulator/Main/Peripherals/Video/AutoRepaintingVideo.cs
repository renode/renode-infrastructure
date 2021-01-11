//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;
using Xwt.Drawing;

namespace Antmicro.Renode.Peripherals.Video
{
    [Icon("lcd")]
    public abstract class AutoRepaintingVideo : IVideo, IDisposable
    {
        protected AutoRepaintingVideo(Machine machine)
        {
            innerLock = new object();
            // we use synchronized thread since some deriving classes can generate interrupt on repainting
            repainter = machine.ObtainManagedThread(DoRepaint, FramesPerSecond);
            Endianess = ELFSharp.ELF.Endianess.LittleEndian;
        }

        public BitmapImage TakeScreenshot()
        {
#if GUI_DISABLED
            throw new RecoverableException("Taking screenshots with a disabled GUI is not supported");
#else
            if(buffer == null)
            {
                throw new RecoverableException("Frame buffer is empty.");
            }

            // this is inspired by FrameBufferDisplayWidget.cs:102
            var pixelFormat = PixelFormat.RGBA8888;
    #if PLATFORM_WINDOWS
                pixelFormat = PixelFormat.BGRA8888;
    #endif

            var converter = PixelManipulationTools.GetConverter(Format, Endianess, pixelFormat, ELFSharp.ELF.Endianess.BigEndian);
            var outBuffer = new byte[Width * Height * pixelFormat.GetColorDepth()];
            converter.Convert(buffer, ref outBuffer);

            var img = new ImageBuilder(Width, Height).ToBitmap();
            img.Copy(outBuffer);
            return img;
#endif
        }

        public void Dispose()
        {
            repainter.Dispose();
        }

        public event Action<byte[]> FrameRendered;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public PixelFormat Format { get; private set; }
        public ELFSharp.ELF.Endianess Endianess { get; protected set; }

        public event Action<int, int, PixelFormat, ELFSharp.ELF.Endianess> ConfigurationChanged
        {
            add
            {
                lock (innerLock)
                {
                    configurationChanged += value;
                    value(Width, Height, Format, Endianess);
                }
            }

            remove
            {
                configurationChanged -= value;
            }
        }

        public abstract void Reset();

        protected void Reconfigure(int? width = null, int? height = null, PixelFormat? format = null, bool autoRepaint = true)
        {
            lock(innerLock)
            {
                var flag = false;
                if(width != null && Width != width.Value)
                {
                    Width = width.Value;
                    flag = true;
                }

                if(height != null && Height != height.Value)
                {
                    Height = height.Value;
                    flag = true;
                }

                if(format != null && Format != format.Value)
                {
                    Format = format.Value;
                    flag = true;
                }

                if(flag && Width > 0 && Height > 0)
                {
                    buffer = new byte[Width * Height * Format.GetColorDepth()];

                    var cc = configurationChanged;
                    if(cc != null)
                    {
                        cc(Width, Height, Format, Endianess);
                    }
                    if(autoRepaint)
                    {
                        repainter.Start();
                    }
                    else
                    {
                        repainter.Stop();
                    }
                }
                else
                {
                    repainter.Stop();
                }
            }
        }

        protected abstract void Repaint();

        protected void DoRepaint()
        {
            if(buffer != null)
            {
                Repaint();
                var fr = FrameRendered;
                if(fr != null)
                {
                    lock(innerLock)
                    {
                        fr(buffer);
                    }
                }
            }
        }

        protected byte[] buffer;

        private IManagedThread repainter;
        private Action<int, int, PixelFormat, ELFSharp.ELF.Endianess> configurationChanged;
        private readonly object innerLock;

        private const int FramesPerSecond = 25;
    }
}

