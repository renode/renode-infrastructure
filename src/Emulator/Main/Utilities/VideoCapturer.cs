//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode
{
    public static class VideoCapturer
    {
        public static bool Start(string device, IEmulationElement loggingParent)
        {
#if !PLATFORM_LINUX
            throw new RecoverableException("Video capture is supported on Linux only!");
#else

            // stop any previous capturer
            Stop();

            VideoCapturer.loggingParent = loggingParent;

            loggingParent.Log(LogLevel.Debug, "Opening device: {0}...", device);
            fd = LibCWrapper.Open(device, O_RDWR);

            if(fd == -1)
            {
                loggingParent.Log(LogLevel.Error, "Couldn't open device: {0}", device);
                return false;
            }

            if(!CheckImageFormat())
            {
                loggingParent.Log(LogLevel.Error, "Device does not support JPEG output: {0}", device);
                LibCWrapper.Close(fd);
                return false;
            }

            started = true;
            return RequestBuffer();
#endif
        }

        public static void Stop()
        {
#if !PLATFORM_LINUX
            throw new RecoverableException("Video capture is supported on Linux only!");
#else
            if(started)
            {
                started = false;
                FreeBuffer();
                LibCWrapper.Close(fd);
            }
#endif
        }

        public static byte[] GrabSingleFrame()
        {
#if !PLATFORM_LINUX
            throw new RecoverableException("Video capture is supported on Linux only!");
#else
            loggingParent.Log(LogLevel.Debug, "Grabbing a frame...");

            var framebuffer = Marshal.AllocHGlobal(FRAME_BUFFER_SIZE);

            var buf = Marshal.AllocHGlobal(V4L2_BUFFER_SIZE);

            // buf.index = 0;
            Marshal.WriteInt32(buf, 0x0, 0);
            // buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            Marshal.WriteInt32(buf, 0x4, V4L2_BUF_TYPE_VIDEO_CAPTURE);
            // buf.memory = V4L2_MEMORY_USERPTR;
            Marshal.WriteInt32(buf, 0x3c, V4L2_MEMORY_USERPTR);
            // buf.m.userptr = PTR;
            Marshal.WriteInt64(buf, 0x40, framebuffer.ToInt64());
            // buf.length = LENGTH;
            Marshal.WriteInt32(buf, 0x48, FRAME_BUFFER_SIZE);

            if(!DoIoctl(IoctlCode.VIDIOC_QBUF, buf)
                || !DoIoctl(IoctlCode.VIDIOC_STREAMON, IntPtr.Add(buf, 0x4))
                || !DoIoctl(IoctlCode.VIDIOC_DQBUF, buf))
            {
                return null;
            }

            DoIoctl(IoctlCode.VIDIOC_STREAMOFF, IntPtr.Add(buf, 0x4));

            // var butesUsed = buf.bytesused;
            var bytesUsed = Marshal.ReadInt32(buf, 0x8);

            var frame = new byte[bytesUsed];
            Marshal.Copy(framebuffer, frame, 0, bytesUsed);

            Marshal.FreeHGlobal(buf);
            Marshal.FreeHGlobal(framebuffer);

            return frame;
#endif
        }

        public static Tuple<int, int> SetImageSize(int width, int height)
        {
#if !PLATFORM_LINUX
            throw new RecoverableException("Video capture is supported on Linux only!");
#else
            if(!FreeBuffer())
            {
                return null;
            }

            var fmt = Marshal.AllocHGlobal(V4L2_FORMAT_SIZE);

            // fmt.type = V4L2_BUF_TYPE_VIDEO_CAPTURE
            Marshal.WriteInt32(fmt, 0x0, V4L2_BUF_TYPE_VIDEO_CAPTURE);
            // fmt.fmt.pix.width = width
            Marshal.WriteInt32(fmt, 0x8, width);
            // fmt.fmt.pix.height = height
            Marshal.WriteInt32(fmt, 0xc, height);
            // fmt.fmt.pix.pixelformat = V4L2_PIX_FMT_JPEG
            Marshal.WriteInt32(fmt, 0x10, V4L2_PIX_FMT_JPEG);
            // fmt.fmt.pix.field = V4L2_FIELD_NONE
            Marshal.WriteInt32(fmt, 0x14, V4L2_FIELD_NONE);

            var result = DoIoctl(IoctlCode.VIDIOC_S_FMT, fmt);

            var finalWidth = Marshal.ReadInt32(fmt, 0x8);
            var finalHeight = Marshal.ReadInt32(fmt, 0xc);

            Marshal.FreeHGlobal(fmt);

            if(!result)
            {
                return null;
            }

            if(!RequestBuffer())
            {
                return null;
            }

            return Tuple.Create(finalWidth, finalHeight);
#endif
        }

        private static bool CheckImageFormat()
        {
            var fmt = Marshal.AllocHGlobal(V4L2_FORMAT_SIZE);

            // fmt.type = V4L2_BUF_TYPE_VIDEO_CAPTURE
            Marshal.WriteInt32(fmt, 0x0, V4L2_BUF_TYPE_VIDEO_CAPTURE);

            var result = DoIoctl(IoctlCode.VIDIOC_G_FMT, fmt);
            if(!result)
            {
                return false;
            }

            var format = Marshal.ReadInt32(fmt, 0x10);
            Marshal.FreeHGlobal(fmt);

            return format == V4L2_PIX_FMT_JPEG || format == V4L2_PIX_FMT_MJPG;
        }

        private static bool RequestBuffer()
        {
            DebugHelper.Assert(!bufferAllocated);

            loggingParent.Log(LogLevel.Debug, "Requesting video IO buffer...");

            // INIT
            var req = Marshal.AllocHGlobal(V4L2_REQUESTBUFFERS_SIZE);

            // req.count = 1;
            Marshal.WriteInt32(req, 0x0, 1);
            // req.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            Marshal.WriteInt32(req, 0x4, V4L2_BUF_TYPE_VIDEO_CAPTURE);
            // req.memory = V4L2_MEMORY_USERPTR;
            Marshal.WriteInt32(req, 0x8, V4L2_MEMORY_USERPTR);

            var result = DoIoctl(IoctlCode.VIDIOC_REQBUF, req);

            Marshal.FreeHGlobal(req);

            bufferAllocated = true;

            return result;
        }

        private static bool FreeBuffer()
        {
            if(!bufferAllocated)
            {
                return true;
            }

            loggingParent.Log(LogLevel.Debug, "Freeing video IO buffer...");

            // INIT
            var req = Marshal.AllocHGlobal(V4L2_REQUESTBUFFERS_SIZE);

            // req.count = 0;
            Marshal.WriteInt32(req, 0x0, 0);
            // req.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            Marshal.WriteInt32(req, 0x4, V4L2_BUF_TYPE_VIDEO_CAPTURE);
            // req.memory = V4L2_MEMORY_USERPTR;
            Marshal.WriteInt32(req, 0x8, V4L2_MEMORY_USERPTR);

            var result = DoIoctl(IoctlCode.VIDIOC_REQBUF, req);

            Marshal.FreeHGlobal(req);

            bufferAllocated = false;

            return result;
        }

        private static bool DoIoctl(IoctlCode code, IntPtr data)
        {
            int err;
            if((err = LibCWrapper.Ioctl(fd, (int)code, data)) < 0)
            {
                var lastErrorCode = Marshal.GetLastWin32Error();
                var lastErrorMessage = LibCWrapper.Strerror(lastErrorCode);

                loggingParent.Log(LogLevel.Error, "There was an error when executing the {0} ioctl: {1} (0x{2:X})", Enum.GetName(typeof(IoctlCode), code), lastErrorMessage, lastErrorCode); 
                LibCWrapper.Close(fd);
                return false;
            }

            return true;
        }

        private static int fd;
        private static bool started;
        private static bool bufferAllocated;
        private static IEmulationElement loggingParent;

        private const int O_RDWR = 2;

        private const int V4L2_BUFFER_SIZE = 0x58;
        private const int V4L2_REQUESTBUFFERS_SIZE = 20;
        private const int V4L2_BUF_TYPE_VIDEO_CAPTURE = 0x1;
        private const int V4L2_MEMORY_USERPTR = 0x2;

        private const int V4L2_FIELD_NONE = 0x1;
        private const int V4L2_FORMAT_SIZE = 208;

        // size of the buffer is set arbitrarily to 6MB
        private const int FRAME_BUFFER_SIZE = 6 * 1024 * 1024;

        private const int V4L2_PIX_FMT_JPEG = 0x47504a4d;
        private const int V4L2_PIX_FMT_MJPG = 0x4745504a;

        private enum IoctlCode : uint
        {
            VIDIOC_REQBUF = 0xc0145608,
            VIDIOC_QBUF = 0xc058560f,
            VIDIOC_DQBUF = 0xc0585611,
            VIDIOC_STREAMON = 0x40045612,
            VIDIOC_STREAMOFF = 0x40045613,
            VIDIOC_S_FMT = 0xc0d05605,
            VIDIOC_G_FMT = 0xc0d05604,
        }
    }
}
