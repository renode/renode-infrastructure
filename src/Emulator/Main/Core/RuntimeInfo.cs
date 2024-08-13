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
    }
}