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

        public static bool RIDMatches(string rid)
        {
            var ridParts = rid.Split('-');
            var os = ridParts[0];
            var arch = ridParts.Length > 1 ? ridParts[1] : null;
            return (os == "any" || (os == "unix" && !IsWindows()) || os == RIDOS) && (arch == null || arch == RIDArch);
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

        public static string RIDOS => IsLinux() ? "linux" : IsWindows() ? "win" : IsMacOS() ? "osx" : "any";

        public static string RIDArch
        {
            get
            {
                var runtimeArch = RuntimeInformation.ProcessArchitecture;
                return runtimeArch == Architecture.X64 ? "x64" : runtimeArch == Architecture.Arm64 ? "arm64" : runtimeArch == Architecture.Arm ? "arm" : null;
            }
        }

        public static string RID => RIDArch != null ? $"{RIDOS}-{RIDArch}" : RIDOS;
    }
}
