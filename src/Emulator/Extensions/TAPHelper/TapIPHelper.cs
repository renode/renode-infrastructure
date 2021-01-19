//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Net;

namespace Antmicro.Renode.TAPHelper
{
    public class TapIPHelper
    {
        public static bool IsValidIPv4(string input)
        {
            return IsValidIP(input, false);
        }

        public static bool IsValidIPv6(string input)
        {
            return IsValidIP(input, true);
        }

        private static bool IsValidIP(string input, bool ipv6 = false)
        {
            IPAddress address;
            if(!IPAddress.TryParse(input, out address)) return false;

            if(ipv6) return (System.Net.Sockets.AddressFamily.InterNetworkV6 == address.AddressFamily);
            return (System.Net.Sockets.AddressFamily.InterNetwork == address.AddressFamily);
        }

    }
}
