//
// Copyright (c) 2010-2021 Antmicro
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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.TAPHelper
{
    public class TAPTools
    {
        private static readonly int O_RDWR                  = 2;
        private static readonly int IFNAMSIZ                = 0x10;
        private static readonly int TUNSETIFF               = 1074025674;
        private static readonly int TUNSETPERSIST           = 0x400454cb;
        private static readonly UInt16 IFF_TUN              = 0x1;
        private static readonly UInt16 IFF_TAP_IFF_NO_PI    = 0x0002 | 0x1000;
        private static readonly int IFR_SIZE                = 80;

        private static int Open_TUNTAP(IntPtr dev, UInt16 flags, bool persistent)
        {
            var ifr = Marshal.AllocHGlobal(IFR_SIZE); // we need 40 bytes, but we allocate a bit more

            var fd = LibCWrapper.Open("/dev/net/tun", O_RDWR);
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
            if((err = LibCWrapper.Ioctl(fd, TUNSETIFF, ifr)) < 0)
            {
                Console.Error.WriteLine("Could not set TUNSETIFF, error: {0}", Marshal.GetLastWin32Error());
                LibCWrapper.Close(fd);
                return err;
            }

            if(persistent)
            {
                if((err = LibCWrapper.Ioctl(fd, TUNSETPERSIST, 1)) < 0)
                {
                    Console.Error.WriteLine("Could not set TUNSETPERSIST, error: {0}", Marshal.GetLastWin32Error());
                    LibCWrapper.Close(fd);
                    return err;
                }
            }

            LibCWrapper.Strcpy(dev, ifr);

            Marshal.FreeHGlobal(ifr);

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
    }
}
#endif
