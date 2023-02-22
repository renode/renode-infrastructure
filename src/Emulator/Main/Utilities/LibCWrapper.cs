//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
#if PLATFORM_LINUX
using Mono.Unix.Native;
using Mono.Unix;
#endif

namespace Antmicro.Renode.Utilities
{
    public class LibCWrapper
    {
        public static int Open(string path, int mode)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            var marshalledPath = Marshal.StringToHGlobalAnsi(path);
            var result = open(marshalledPath, mode);
            Marshal.FreeHGlobal(marshalledPath);
            return result;
#endif
        }

        public static int Close(int fd)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return close(fd);
#endif
        }

        public static bool Write(int fd, IntPtr buffer, int count)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            var written = 0;
            while(written < count)
            {
                int writtenThisTime = write(fd, buffer + written, count - written);
                if(writtenThisTime <= 0)
                {
                    return false;
                }

                written += writtenThisTime;
            }

            return true;
#endif
        }

        public static byte[] Read(int fd, int count)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            byte[] result = null;
            var buffer = Marshal.AllocHGlobal(count);
            var r = read(fd, buffer, count);
            if(r > 0)
            {
                result = new byte[r];
                Marshal.Copy(buffer, result, 0, r);
            }
            Marshal.FreeHGlobal(buffer);
            return result ?? new byte[0];
#endif
        }

        public static byte[] Read(int fd, int count, int timeout, Func<bool> shouldCancel)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            int pollResult;
            var pollData = new Pollfd {
                fd = fd,
                events = PollEvents.POLLIN
            };

            do
            {
                pollResult = Syscall.poll(new [] { pollData }, timeout);
            }
            while(UnixMarshal.ShouldRetrySyscall(pollResult) && !shouldCancel());

            if(pollResult > 0)
            {
                return Read(fd, count);
            }
            else
            {
                return null;
            }
#endif
        }

        public static int Ioctl(int fd, int request, int arg)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return ioctl(fd, request, arg);
#endif
        }

        public static int Ioctl(int fd, int request, IntPtr arg)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return ioctl(fd, request, arg);
#endif
        }

        public static IntPtr Strcpy(IntPtr dst, IntPtr src)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return strcpy(dst, src);
#endif
        }

        public static string Strerror(int id)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return Marshal.PtrToStringAuto(strerror(id));
#endif
        }

        public static int Socket(int domain, int type, int protocol)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return socket(domain, type, protocol);
#endif
        }

        #region Externs

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int open(IntPtr pathname, int flags);

        [DllImport("libc", EntryPoint = "strcpy")]
        private static extern IntPtr strcpy(IntPtr dst, IntPtr src);

        [DllImport("libc", EntryPoint = "ioctl")]
        private static extern int ioctl(int d, int request, IntPtr a);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int ioctl(int d, int request, int a);

        [DllImport("libc", EntryPoint = "socket", SetLastError = true)]
        private static extern int socket(int domain, int type, int protocol);

        [DllImport("libc", EntryPoint = "close")]
        private static extern int close(int fd);

        [DllImport("libc", EntryPoint = "write")]
        private static extern int write(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "read")]
        private static extern int read(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "strerror")]
        private static extern IntPtr strerror(int fd);

        #endregion
    }
}
