//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Video;

namespace Antmicro.Renode.Testing
{
    public static class FrameBufferTesterExtension
    {
        public static void CreateFrameBufferTester(this Emulation emulation, string name, int timeoutInSeconds = 300)
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

        public FrameBufferTester WaitForFrame(string fileName, int? timeoutInSeconds = null)
        {
            var image = Image.FromFile(fileName);
            var bytes = BitmapToByteArray((Bitmap)image);
            return WaitForFrame(bytes, timeoutInSeconds.HasValue ? TimeSpan.FromSeconds(timeoutInSeconds.Value) : (TimeSpan?)null);
        }

        public FrameBufferTester WaitForFrame(byte[] frame, TimeSpan? timeout = null)
        {
            byte[] queuedFrame;
            var finalTimeout = timeout ?? globalTimeout;
            Stopwatch stopwatch = null;
            try
            {
                stopwatch = Stopwatch.StartNew();
                TimeSpan timeLeft;
                do
                {
                    timeLeft = finalTimeout - stopwatch.Elapsed;
                    if(timeLeft.Ticks > 0 && framesQueue.TryTake(out queuedFrame, timeLeft)
                        && frame.Length == queuedFrame.Length)
                    {
                        var shouldContinue = false;
                        for(var i = 0; i < frame.Length; i++)
                        {
                            if(frame[i] != queuedFrame[i])
                            {
                                shouldContinue = true;
                                break;
                            }
                        }
                        if(shouldContinue)
                        {
                            continue;
                        }
                        return this;
                    }
                } while(timeLeft.Ticks > 0);

                throw new ArgumentException();
            }
            finally
            {
                if(stopwatch != null)
                {
                    stopwatch.Stop();
                }
            }
        }

        private void HandleConfigurationChange(int width, int height, Backends.Display.PixelFormat format, ELFSharp.ELF.Endianess endianess)
        {
            if(width == 0 || height == 0)
            {
                return;
            }

            converter = PixelManipulationTools.GetConverter(format, endianess, Backends.Display.PixelFormat.ARGB8888, ELFSharp.ELF.Endianess.LittleEndian);
            frameSize = width * height * 4;
        }

        private void HandleNewFrame(byte[] obj)
        {
            var buffer = new byte[frameSize];
            converter.Convert(obj, ref buffer);
            framesQueue.Add(buffer);
        }

        private IVideo video;
        private IPixelConverter converter;
        private int frameSize;

        private readonly TimeSpan globalTimeout;
        private readonly BlockingCollection<byte[]> framesQueue;
    }
}

