//
// Copyright (c) 2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Antmicro.Renode.UI
{
    public static class DllMap
    {
        // Register a call-back for native library resolution.
        public static void Register(Assembly assembly)
        {
            NativeLibrary.SetDllImportResolver(assembly, MapAndLoad);
        }

        // The callback which loads the mapped libray in place of the original.
        private static IntPtr MapAndLoad(string libraryName, Assembly assembly, DllImportSearchPath? dllImportSearchPath)
        {
            var wasMapped = MapLibraryName(assembly.Location, libraryName, out var mappedName);
            if(!wasMapped)
            {
                mappedName = libraryName;
            }
            // First try loading the library normally, then retry falling back to libraries from homebrew, which
            // are installed to /opt/homebrew/lib on macOS/ARM64, which is not on the dyld search path.
            if(NativeLibrary.TryLoad(mappedName, assembly, dllImportSearchPath, out var handle))
            {
                return handle;
            }
            return NativeLibrary.Load(Path.Combine("/opt/homebrew/lib", mappedName), assembly, dllImportSearchPath);
        }

        // Parse the dll.config file and map the old name to the new name of a library.
        private static bool MapLibraryName(string assemblyLocation, string originalLibName, out string mappedLibName)
        {
            string xmlPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), Path.GetFileNameWithoutExtension(assemblyLocation) + ".dll.config");
            mappedLibName = null;

            if (!File.Exists(xmlPath))
            {
                return false;
            }

            var currOsAttr = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : 
                             RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : 
                             RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : null;

            XElement root = XElement.Load(xmlPath);
            var map = (
                from el in root.Elements("dllmap")
                where (string)el.Attribute("dll") == originalLibName
                let osAttr = (string)el.Attribute("os")
                where (osAttr == null || osAttr == currOsAttr || (osAttr.StartsWith("!") && !osAttr.Contains(currOsAttr)))
                select el
            ).SingleOrDefault();

            if (map != null)
            {
                mappedLibName = map.Attribute("target").Value;
            }

            return (mappedLibName != null);
        }
    }
}