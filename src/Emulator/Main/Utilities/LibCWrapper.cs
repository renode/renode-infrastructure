//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Text;

using Antmicro.Renode.Core;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct Pollfd
    {
        public int Fd;
        public PollEvents Events;
        public PollEvents Revents;
    }

    [StructLayout(LayoutKind.Explicit, Size = 512)]
    public struct Stat
    {
        [FieldOffset(24)] public int RdevMacOS;
        [FieldOffset(40)] public ulong RdevLinux;
    }

    public enum PollEvents : short
    {
        POLLIN = 0x1,
        POLLHUP = 0x10
    }

    public class LibCWrapper
    {
        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int ioctl(int d, int request, ref InterfaceRequest ifreq);

        [DllImport("libc", EntryPoint = "bind", SetLastError = true)]
        public static extern int bind(int sockfd, ref SocketAddressCan addr, int addrSize);

        public static int Open(string path, int flags)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            var marshalledPath = Marshal.StringToHGlobalAnsi(path);
            var result = open(marshalledPath, flags);
            Marshal.FreeHGlobal(marshalledPath);
            return result;
        }

        public static int Creat(string path, int mode)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            var marshalledPath = Marshal.StringToHGlobalAnsi(path);
            var result = creat(marshalledPath, mode);
            Marshal.FreeHGlobal(marshalledPath);
            return result;
        }

        public static int Close(int fd)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return close(fd);
        }

        public static bool Write(int fd, IntPtr buffer, int count)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
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
        }

        public static byte[] Read(int fd, int count)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
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
        }

        public static byte[] Read(int fd, int count, int timeout, Func<bool> shouldCancel)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            int pollResult;
            var pollData = new Pollfd
            {
                Fd = fd,
                Events = PollEvents.POLLIN
            };

            do
            {
                pollResult = Poll(new[] { pollData }, timeout);
                if(shouldCancel())
                {
                    return null;
                }
            }
            while(ShouldRetrySyscall(pollResult));

            if(pollResult > 0)
            {
                return Read(fd, count);
            }
            else
            {
                return null;
            }
        }

        public static bool ShouldRetrySyscall(int result)
        {
            return result == -1 && Marshal.GetLastWin32Error() == EINTR;
        }

        public static int Poll(Pollfd[] fds, int timeout)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return poll(fds, (ulong)fds.Length, timeout);
        }

        public static int Stat(string path, out Stat statBuf)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            var marshalledPath = Marshal.StringToHGlobalAnsi(path);
            var res = stat(marshalledPath, out statBuf);
            Marshal.FreeHGlobal(marshalledPath);
            return res;
        }

        public static int Ioctl(int fd, int request, int arg)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return ioctl(fd, request, arg);
        }

        public static int Ioctl(int fd, int request, IntPtr arg)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return ioctl(fd, request, arg);
        }

        public static int Ioctl(int fd, int request, ref InterfaceRequest ifreq)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return ioctl(fd, request, ref ifreq);
        }

        public static IntPtr Strcpy(IntPtr dst, IntPtr src)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return strcpy(dst, src);
        }

        public static string Strerror(int id)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return Marshal.PtrToStringAuto(strerror(id));
        }

        public static string GetLastError()
        {
            return Strerror(Marshal.GetLastWin32Error());
        }

        public static int Socket(int domain, int type, int protocol)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return socket(domain, type, protocol);
        }

        public static int SetSocketOption(int socket, int level, int optionName, ref int optionValue)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return setsockopt(socket, level, optionName, ref optionValue, sizeof(int));
        }

        public static int Bind(int domain, SocketAddressCan addr, int addrSize)
        {
            if(RuntimeInfo.IsWindows())
            {
                throw new NotSupportedException("This API is available on Unix only!");
            }
            return bind(domain, ref addr, addrSize);
        }

        public static int O_CREAT => RuntimeInfo.IsMacOS() ? 0x200 : 0x40;

        public static int O_EXCL => RuntimeInfo.IsMacOS() ? 0x800 : 0x80;

        public static int O_TRUNC => RuntimeInfo.IsMacOS() ? 0x400 : 0x200;

        // Source: https://github.com/apple-oss-distributions/Libc/blob/main/exclave/sys/fcntl.h, https://sourceware.org/git/?p=glibc.git;a=blob;f=bits/fcntl.h;h=ed14c22625b2a6706930967a1cc3e7e167999fdb;hb=HEAD
        public const int O_RDONLY = 0;
        public const int O_WRONLY = 1;
        public const int O_RDWR = 2;
        public const int DEFFILEMODE = 0b110110110;

        // Disable incorrect warning about the `s_` prefix: https://github.com/dotnet/roslyn/issues/57706
#pragma warning disable IDE1006
        public const int S_IRUSR = 0x100;
        public const int S_IWUSR = 0x80;
#pragma warning restore IDE1006

        #region Externs

        // dotnet doesn't support varags, so we can only use the 2-argument `open`
        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int open(IntPtr pathname, int flags);

        [DllImport("libc", EntryPoint = "creat", SetLastError = true)]
        private static extern int creat(IntPtr pathname, int mode);

        [DllImport("libc", EntryPoint = "strcpy")]
        private static extern IntPtr strcpy(IntPtr dst, IntPtr src);

        [DllImport("libc", EntryPoint = "ioctl")]
        private static extern int ioctl(int d, int request, IntPtr a);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int ioctl(int d, int request, int a);

        [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
        private static extern int poll(Pollfd[] fds, ulong count, int timeout);

        [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
        private static extern int stat(IntPtr path, out Stat stat);

        [DllImport("libc", EntryPoint = "socket", SetLastError = true)]
        private static extern int socket(int domain, int type, int protocol);

        [DllImport("libc", EntryPoint = "setsockopt", SetLastError = true)]
        private static extern int setsockopt(int socket, int level, int optionName, ref int optionValue, int optionLength);

        [DllImport("libc", EntryPoint = "close")]
        private static extern int close(int fd);

        [DllImport("libc", EntryPoint = "write", SetLastError = true)]
        private static extern int write(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "read")]
        private static extern int read(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "strerror")]
        private static extern IntPtr strerror(int fd);

        private const int EINTR = 4;

        #endregion
    }
}