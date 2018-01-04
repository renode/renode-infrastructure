//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if PLATFORM_LINUX
using System;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Unix.Native;
using Mono.Unix;
using System.Linq;

namespace Antmicro.Renode.TAPHelper
{
    public class LibC
    {
        private static readonly int O_RDWR                  = 2;
        private static readonly int IFNAMSIZ                = 0x10;
        private static readonly int TUNSETIFF               = 1074025674;
        private static readonly int TUNSETPERSIST           = 0x400454cb;
        private static readonly UInt16 IFF_TUN              = 0x1;
        private static readonly UInt16 IFF_TAP_IFF_NO_PI    = 0x0002 | 0x1000;
        private static readonly int IFR_SIZE                = 80;

        public static int Close(int fd)
        {
            return close(fd);
        }

        private static int Open_TUNTAP(IntPtr dev, UInt16 flags, bool persistent)
        {
            var ifr = Marshal.AllocHGlobal(IFR_SIZE); // we need 40 bytes, but we allocate a bit more
            var clonedev = Marshal.StringToHGlobalAnsi("/dev/net/tun");

            var fd = open(clonedev, O_RDWR);
            if(fd < 0)
            {
                Console.Error.WriteLine("Could not open /dev/net/tun, error: {0}", Marshal.GetLastWin32Error());
                return fd;
            }

            var memory = new byte[IFR_SIZE];
            Array.Clear(memory, 0, IFR_SIZE);

            var bytes = BitConverter.GetBytes(flags);
            Array.Copy(bytes, 0, memory, IFNAMSIZ, 2);

            if(dev != IntPtr.Zero)
            {
                var devBytes = Encoding.ASCII.GetBytes(Marshal.PtrToStringAnsi(dev));
                Array.Copy(devBytes, memory, Math.Min(devBytes.Length, IFNAMSIZ));
            }

            Marshal.Copy(memory, 0, ifr, IFR_SIZE);

            int err = 0;
            if((err = ioctl(fd, TUNSETIFF, ifr)) < 0)
            {
                Console.Error.WriteLine("Could not set TUNSETIFF, error: {0}", Marshal.GetLastWin32Error());
                close(fd);
                return err;
            }

            if(persistent)
            {
                if((err = ioctl(fd, TUNSETPERSIST, 1)) < 0)
                {
                    Console.Error.WriteLine("Could not set TUNSETPERSIST, error: {0}", Marshal.GetLastWin32Error());
                    close(fd);
                    return err;
                }
            }

            strcpy(dev, ifr);

            Marshal.FreeHGlobal(ifr);
            Marshal.FreeHGlobal(clonedev);

            return fd;
        }

        public static int OpenTUN(IntPtr dev, bool persistent = false)
        {
            return Open_TUNTAP(dev, IFF_TUN, persistent);
        }

        public static int OpenTAP(IntPtr dev, bool persistent = false)
        {
            return Open_TUNTAP(dev, IFF_TAP_IFF_NO_PI, persistent);
        }

        public static int WriteData(int fd, IntPtr buffer, int count)
        {
            int written = 0;
            while(written < count)
            {
                int this_turn_written = write(fd, buffer + written, count - written);
                if(this_turn_written <= 0)
                {
                    return 0;
                }

                written += this_turn_written;
            }

            return 1;
        }

        public static byte[] ReadData(int fd, int count)
        {
            var buffer = Marshal.AllocHGlobal(count);
            var r = read(fd, buffer, count);
            if(r > 0)
            {
                var result = new byte[r];
                Marshal.Copy(buffer, result, 0, r);
                return result;
            }
            else
            {
                return new byte[0];
            }
        }

        public static byte[] ReadDataWithTimeout(int fd, int count, int timeout, Func<bool> shouldCancel)
        {
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
                return ReadData(fd, count);
            }
            else
            {
                return null;
            }
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

        [DllImport("libc", EntryPoint = "close")]
        private static extern int close(int fd);

        [DllImport("libc", EntryPoint = "write")]
        private static extern int write(int fd, IntPtr buf, int count);

        [DllImport("libc", EntryPoint = "read")]
        private static extern int read(int fd, IntPtr buf, int count);

        #endregion
    }
}
#endif
