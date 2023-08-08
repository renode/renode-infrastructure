//
// Copyright (c) 2010-2022 Antmicro
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

namespace Antmicro.Renode.Peripherals.Video
{
    [Icon("lcd")]
    public abstract class AutoRepaintingVideo : IVideo, IDisposable
    {
        protected AutoRepaintingVideo(IMachine machine)
        {
            innerLock = new object();
            // we use synchronized thread since some deriving classes can generate interrupt on repainting
            this.machine = machine;
            repainter = machine.ObtainManagedThread(DoRepaint, FramesPerVirtualSecond);
            Endianess = ELFSharp.ELF.Endianess.LittleEndian;
        }

        public RawImageData TakeScreenshot()
        {
            if(buffer == null)
            {
                throw new RecoverableException("Frame buffer is empty.");
            }

            var converter = PixelManipulationTools.GetConverter(Format, Endianess, RawImageData.PixelFormat, ELFSharp.ELF.Endianess.BigEndian);
            var outBuffer = new byte[Width * Height * RawImageData.PixelFormat.GetColorDepth()];
            converter.Convert(buffer, ref outBuffer);

            return new RawImageData(outBuffer, Width, Height);
        }

        public void Dispose()
        {
            repainter.Dispose();
        }

        [field: Transient]
        public event Action<byte[]> FrameRendered;

        public uint FramesPerVirtualSecond
        {
            get
            {
                return framesPerVirtualSecond;
            }
            set
            {
                repainter.Dispose();
                repainter = machine.ObtainManagedThread(DoRepaint, value);
                if(RepainterIsRunning)
                {
                    repainter.Start();
                }

                framesPerVirtualSecond = value;
            }
        }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public PixelFormat Format { get; private set; }
        public ELFSharp.ELF.Endianess Endianess { get; protected set; }
        public bool RepainterIsRunning { get; private set; }

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
                        RepainterIsRunning = true;
                        repainter.Start();
                    }
                    else
                    {
                        RepainterIsRunning = false;
                        repainter.Stop();
                    }
                }
                else
                {
                    RepainterIsRunning = false;
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
        [Transient]
        private Action<int, int, PixelFormat, ELFSharp.ELF.Endianess> configurationChanged;
        private readonly object innerLock;
        private readonly IMachine machine;
        private uint framesPerVirtualSecond = 25;
    }
}

