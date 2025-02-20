//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Video;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;

namespace Antmicro.Renode.Testing
{
    public static class FrameBufferTesterExtension
    {
        public static void CreateFrameBufferTester(this Emulation emulation, string name, float timeoutInSeconds)
        {
            var tester = new FrameBufferTester(TimeSpan.FromSeconds(timeoutInSeconds));
            emulation.ExternalsManager.AddExternal(tester, name);
        }
    }

    public class FrameBufferTester : IExternal, IConnectable<IVideo>
    {
        public FrameBufferTester(TimeSpan timeout)
        {
            framesQueue = new BlockingCollection<byte[]>();
            globalTimeout = timeout;
            newFrameEvent = new AutoResetEvent(false);
        }

        public void AttachTo(IVideo obj)
        {
            if(video != null)
            {
                throw new RecoverableException("Cannot attach to the provided video device as it would overwrite the existing configuration.");
            }
            video = obj;
            video.ConfigurationChanged += HandleConfigurationChange;
            video.FrameRendered += HandleNewFrame;
        }

        public void DetachFrom(IVideo obj)
        {
            video.ConfigurationChanged -= HandleConfigurationChange;
            video.FrameRendered -= HandleNewFrame;
            video = null;
        }

        private static byte[] BitmapToByteArray(Bitmap image)
        {
            BitmapData data = null;

            try
            {
                data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, image.PixelFormat);
                var bytedata = new byte[data.Stride * image.Height];

                Marshal.Copy(data.Scan0, bytedata, 0, bytedata.Length);

                return bytedata;
            }
            finally
            {
                if(data != null)
                {
                    image.UnlockBits(data);
                }
            }
        }

        public FrameBufferTester WaitForFrame(string fileName, float? timeout = null)
        {
            var image = Image.FromFile(fileName);
            var bytes = BitmapToByteArray((Bitmap)image);
            return WaitForFrame(bytes, timeout.HasValue ? TimeSpan.FromSeconds(timeout.Value) : (TimeSpan?)null);
        }

        public FrameBufferTester WaitForFrame(byte[] frame, TimeSpan? timeout = null)
        {
            var machine = video.GetMachine();
            var finalTimeout = timeout ?? globalTimeout;
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)finalTimeout.TotalMilliseconds);

            var emulation = EmulationManager.Instance.CurrentEmulation;
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }

            do
            {
                if(framesQueue.TryTake(out var queuedFrame)
                    && queuedFrame.Length == frame.Length
                    && Enumerable.SequenceEqual(queuedFrame, frame))
                {
                    return this;
                }

                WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, newFrameEvent });
            }
            while(!timeoutEvent.IsTriggered);

            throw new ArgumentException();
        }

        public FrameBufferTester WaitForFrameROI(string fileName, uint startX, uint startY, uint width, uint height, float? timeout = null)
        {
            var image = Image.FromFile(fileName);
            var bytes = BitmapToByteArray((Bitmap)image);
            return WaitForFrameROI(bytes, startX, startY, width, height, timeout.HasValue ? TimeSpan.FromSeconds(timeout.Value) : (TimeSpan?)null);
        }

        public FrameBufferTester WaitForFrameROI(byte[] frame, uint startX, uint startY, uint width, uint height, TimeSpan? timeout = null)
        {
            if(width > frameWidth || startX > frameWidth - width || height > frameHeight || startY > frameHeight - height)
            {
                throw new ArgumentException("Region of interest doesn't fit in the frame");
            }

            if(height == 0 || width == 0)
            {
                throw new ArgumentException("Width and height can't be equal to 0.");
            }
            
            var machine = video.GetMachine();
            var finalTimeout = timeout ?? globalTimeout;
            var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)finalTimeout.TotalMilliseconds);

            var emulation = EmulationManager.Instance.CurrentEmulation;
            if(!emulation.IsStarted)
            {
                emulation.StartAll();
            }

            do
            {
                if(framesQueue.TryTake(out var queuedFrame)
                        && queuedFrame.Length == frame.Length)
                {
                    bool roiEqual = true;
                    for(uint i = startY; roiEqual && i < startY + height; i++)
                    {
                        for(uint j = startX; roiEqual && j < startX + width; j++)
                        {
                            for(uint k = 0; roiEqual && k < 4; k++)
                            {
                                int index = (int)(i*frameWidth*4 + j*4 + k);
                                if(frame[index] != queuedFrame[index])
                                {
                                    roiEqual = false;
                                }
                            }
                        }
                    }
                    if(roiEqual)
                    {
                        return this;
                    }
                }
                WaitHandle.WaitAny(new [] { timeoutEvent.WaitHandle, newFrameEvent });
            }
            while(!timeoutEvent.IsTriggered);

            throw new ArgumentException();
        }

        private void HandleConfigurationChange(int width, int height, Backends.Display.PixelFormat format, ELFSharp.ELF.Endianess endianess)
        {
            if(width == 0 || height == 0)
            {
                return;
            }
            
            this.format = format;
            this.endianess = endianess;
            InitConverter();
            frameWidth = width;
            frameHeight = height;
        }

        private void HandleNewFrame(byte[] obj)
        {
            var buffer = new byte[frameWidth * frameHeight * 4];
            converter.Convert(obj, ref buffer);
            framesQueue.Add(buffer);
            newFrameEvent.Set();
        }

        [PostDeserialization]
        private void InitConverter()
        {
            if(format != null && endianess != null)
            {
                converter = PixelManipulationTools.GetConverter((Backends.Display.PixelFormat)format, (ELFSharp.ELF.Endianess)endianess, Backends.Display.PixelFormat.ARGB8888, ELFSharp.ELF.Endianess.LittleEndian);
            }
        }
        
        [Transient]
        private IPixelConverter converter;

        private int frameWidth;
        private int frameHeight;
        private IVideo video;
        private Backends.Display.PixelFormat? format;
        private ELFSharp.ELF.Endianess? endianess;
        
        // Even if newFrameEvent was set before saving the emulation it doesn't matter
        // as we ultimately would have to start the `WaitForFrame` loop from the beginning either way
        [Constructor(false)]
        private AutoResetEvent newFrameEvent;
        private readonly TimeSpan globalTimeout;
        private readonly BlockingCollection<byte[]> framesQueue;
    }
}

