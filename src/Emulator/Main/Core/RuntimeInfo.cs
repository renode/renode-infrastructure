//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
#pragma warning disable IDE0005
using System.Reflection;
#pragma warning restore IDE0005
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Core
{
    public static class RuntimeInfo
    {
        public static bool IsLinux()
        {
#if NET
            return OperatingSystem.IsLinux();
#elif PLATFORM_LINUX
            return true;
#else
            return false;
#endif
        }

        public static bool IsMacOS()
        {
#if NET
            return OperatingSystem.IsMacOS();
#elif PLATFORM_OSX
            return true;
#else
            return false;
#endif
        }

        public static bool IsWindows()
        {
#if NET
            return OperatingSystem.IsWindows();
#elif PLATFORM_WINDOWS
            return true;
#else
            return false;
#endif
        }

        public static bool IsMono => Type.GetType("Mono.Runtime") != null;

        public static string Version
        {
            get
            {
#if MONO
                var getDisplayName = Type.GetType("Mono.Runtime").GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
                return $"Mono {(string)getDisplayName?.Invoke(null, null) ?? "(unknown version)"}";
#elif NET || NET47_OR_GREATER
                return RuntimeInformation.FrameworkDescription;
#else
                return $".NET Framework {Environment.Version}";
#endif
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
#if NET
                return RuntimeInformation.ProcessArchitecture.ToString();
#else
                return "X64";
#endif

            }
        }
    }
}