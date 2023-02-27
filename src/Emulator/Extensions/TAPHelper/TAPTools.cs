//
// Copyright (c) 2010-2023 Antmicro
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
using System.Net.NetworkInformation;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.TAPHelper
{
    public class TAPTools
    {
        private const int O_RDWR                  = 2;
        private const int IFNAMSIZ                = 0x10;
        private const int TUNSETIFF               = 1074025674;
        private const int TUNSETPERSIST           = 0x400454cb;
        private const UInt16 IFF_TUN              = 0x1;
        private const UInt16 IFF_TAP_IFF_NO_PI    = 0x0002 | 0x1000;
        private const int IFR_SIZE                = 80;

        private const int SIOCSIFFLAGS            = 0x8914;
        private const int SIOCGIFFLAGS            = 0x8913;
        private const UInt16 IFF_UP               = 1;

        private static bool DoesInterfaceExist(string name)
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach(var iface in ifaces)
            {
                if(iface.Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static int Up_TUNTAP(IntPtr ifrIn)
        {
            int err = 0;

            // Bring TAP up - we need to create a regular socket for this
            int sock = LibCWrapper.Socket(2, 2, 0); //AF_INET, SOCK_DGRAM
            if(sock == -1)
            {
                Logger.Log(LogLevel.Debug, "Could not create ioctl socket, error {0}", Marshal.GetLastWin32Error());
                return sock;
            }

            // make copy of ifrIn, so our ioctls don't clobber the donor
            var ifr = Marshal.AllocHGlobal(IFR_SIZE);
            LibCWrapper.Strcpy(ifr, ifrIn);
            try
            {
                if((err = LibCWrapper.Ioctl(sock, SIOCGIFFLAGS, ifr)) < 0)
                {
                    Logger.Log(LogLevel.Debug, "Could not get flags on TUN/TAP interface, error {0}", Marshal.GetLastWin32Error());
                    return err;
                }

                var currentFlags = BitConverter.ToUInt16(Marshal.ReadInt16(ifr, IFNAMSIZ).AsRawBytes(), 0);

                // Only try this if TAP is down
                if((currentFlags & IFF_UP) == 0)
                {
                    currentFlags |= IFF_UP;
                    Marshal.WriteInt16(ifr, IFNAMSIZ, BitConverter.ToInt16(currentFlags.AsRawBytes(), 0));

                    if((err = LibCWrapper.Ioctl(sock, SIOCSIFFLAGS, ifr)) < 0)
                    {
                        Logger.Log(LogLevel.Debug, "Could not activate TUN/TAP interface, error {0}", Marshal.GetLastWin32Error());
                        return err;
                    }
                }
            }
            finally
            {
                LibCWrapper.Close(sock);
                Marshal.FreeHGlobal(ifr);
            }

            return err;
        }

        private static int Open_TUNTAP(IntPtr dev, UInt16 flags, bool persistent)
        {
            var fd = LibCWrapper.Open("/dev/net/tun", O_RDWR);
            if(fd < 0)
            {
                Logger.Log(LogLevel.Debug, "Could not open /dev/net/tun, error: {0}", Marshal.GetLastWin32Error());
                return fd;
            }

            var ifr = Marshal.AllocHGlobal(IFR_SIZE); // we need 40 bytes, but we allocate a bit more
            try
            {
                var memory = new byte[IFR_SIZE];
                Array.Clear(memory, 0, IFR_SIZE);

                var bytes = BitConverter.GetBytes(flags);
                Array.Copy(bytes, 0, memory, IFNAMSIZ, 2);

                bool exists = false;
                if(dev != IntPtr.Zero)
                {
                    string devname = Marshal.PtrToStringAnsi(dev);
                    exists = DoesInterfaceExist(devname);

                    var devBytes = Encoding.ASCII.GetBytes(devname);
                    Array.Copy(devBytes, memory, Math.Min(devBytes.Length, IFNAMSIZ));
                }

                Marshal.Copy(memory, 0, ifr, IFR_SIZE);

                int err = 0;
                if((err = LibCWrapper.Ioctl(fd, TUNSETIFF, ifr)) < 0)
                {
                    Logger.Log(LogLevel.Debug, "Could not set TUNSETIFF, error: {0}", Marshal.GetLastWin32Error());
                    LibCWrapper.Close(fd);
                    return err;
                }

                if(persistent)
                {
                    if((err = LibCWrapper.Ioctl(fd, TUNSETPERSIST, 1)) < 0)
                    {
                        Logger.Log(LogLevel.Debug, "Could not set TUNSETPERSIST, error: {0}", Marshal.GetLastWin32Error());
                        LibCWrapper.Close(fd);
                        return err;
                    }
                }
                
                // If TAP was created by us, we try to bring it up
                if(!exists)
                {
                    if((err = Up_TUNTAP(ifr)) < 0)
                    {
                        Logger.Log(LogLevel.Debug, "Could not bring device up, do it manually.");
                        LibCWrapper.Close(fd);
                        return err;
                    }
                }

                LibCWrapper.Strcpy(dev, ifr);
            }
            finally
            {
                Marshal.FreeHGlobal(ifr);
            }

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
