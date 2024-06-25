//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Text;
#if PLATFORM_LINUX
using Mono.Unix.Native;
using Mono.Unix;
#endif

namespace Antmicro.Renode.Utilities
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct InterfaceRequest
    {
        // This class represents ifreq structure
        public InterfaceRequest(string name, int interfaceIndex = 0)
        {
            if(name.Length >= InterfaceNameSize)
            {
                throw new ArgumentException($"Interface name must be no longer than {InterfaceNameSize - 1} characters", nameof(name));
            }
            Name = Encoding.ASCII.GetBytes(name).CopyAndResize(InterfaceNameSize);
            InterfaceIndex = interfaceIndex;
        }

        // NOTE: layout of this stucture is deliberate
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=InterfaceNameSize)]
        public byte[] Name;
        public int InterfaceIndex;

        public const int InterfaceNameSize = 16;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SocketAddressCan
    {
        // This class represents sockaddr_can structure
        public SocketAddressCan(int interfaceIndex)
        {
            CanFamily = AddressFamilyCan;
            CanInterfaceIndex = interfaceIndex;
        }

        // NOTE: layout of this stucture is deliberate
        // sockaddr_can.can_family
        public readonly ushort CanFamily;
        // sockaddr_can.can_ifindex
        public int CanInterfaceIndex;

        // AF_CAN
        private const int AddressFamilyCan = 29;
    }

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
                if(shouldCancel())
                {
                    return null;
                }
            }
            while(UnixMarshal.ShouldRetrySyscall(pollResult));

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

        public static int Ioctl(int fd, int request, ref InterfaceRequest ifreq)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return ioctl(fd, request, ref ifreq);
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

        public static string GetLastError()
        {
            return Strerror(Marshal.GetLastWin32Error());
        }

        public static int Socket(int domain, int type, int protocol)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return socket(domain, type, protocol);
#endif
        }

        public static int SetSocketOption(int socket, int level, int optionName, ref int optionValue)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return setsockopt(socket, level, optionName, ref optionValue, 4);
#endif
        }

        public static int Bind(int domain, SocketAddressCan addr, int addrSize)
        {
#if !PLATFORM_LINUX
            throw new NotSupportedException("This API is available on Linux only!");
#else
            return bind(domain, ref addr, addrSize);
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

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int ioctl(int d, int request, ref InterfaceRequest ifreq);

        [DllImport("libc", EntryPoint = "socket", SetLastError = true)]
        private static extern int socket(int domain, int type, int protocol);

        [DllImport("libc", EntryPoint = "setsockopt", SetLastError = true)]
        private static extern int setsockopt(int socket, int level, int optionName, ref int optionValue, int optionLength);

        [DllImport("libc", EntryPoint = "bind", SetLastError = true)]
        public static extern int bind(int sockfd, ref SocketAddressCan addr, int addrSize);

        [DllImport("libc", EntryPoint = "close")]
        private static extern int close(int fd);

        [DllImport("libc", EntryPoint = "write", SetLastError = true)]
        private static extern int write(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "read")]
        private static extern int read(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "strerror")]
        private static extern IntPtr strerror(int fd);

        #endregion
    }
}
