//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Core
{
    public static class RuntimeInfo
    {
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
#if NET
                return OperatingSystem.IsLinux() ? "Linux" 
                    : OperatingSystem.IsWindows() ? "Windows" 
                    : OperatingSystem.IsMacOS() ? "MacOS" 
                    : "Unknown Platform";
#else
    #if PLATFORM_WINDOWS
                return "Windows";
    #elif PLATFORM_LINUX
                return "Linux";
    #elif PLATFORM_OSX
                return "MacOS";
    #else
                return "Unknown Platform";
    #endif
#endif
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