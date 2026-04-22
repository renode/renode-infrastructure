//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Core
{
    public static class RuntimeInfo
    {
        public static bool IsLinux()
        {
            return OperatingSystem.IsLinux();
        }

        public static bool IsMacOS()
        {
            return OperatingSystem.IsMacOS();
        }

        public static bool IsWindows()
        {
            return OperatingSystem.IsWindows();
        }

        public static string Version
        {
            get
            {
                return RuntimeInformation.FrameworkDescription;
            }
        }

        public static string OSIdentifier
        {
            get
            {
                return IsLinux() ? "Linux"
                    : IsWindows() ? "Windows"
                    : IsMacOS() ? "MacOS"
                    : "Unknown Platform";
            }
        }

        public static string ArchitectureIdentifier
        {
            get
            {
                return RuntimeInformation.ProcessArchitecture.ToString();
            }
        }
    }
}
