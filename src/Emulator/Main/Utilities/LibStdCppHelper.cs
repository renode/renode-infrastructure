//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Utilities
{
    public static class LibStdCppHelper
    {
        public static bool TryGetCxaDemangle(out CxaDemangleDelegate result)
        {
            var name = GetLibName();

            // first we try to get the library from the loader path
            if(TryLoadDemangleFunction(name, out result))
            {
                return true;
            }

            // then we search our own paths
            foreach(var path in gccPaths)
            {
                string libraryName;
                if(TryFindLibStdCppInDir(path, out libraryName) && TryLoadDemangleFunction(libraryName, out result))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryLoadDemangleFunction(string libraryName, out CxaDemangleDelegate result)
        {
            IntPtr libraryAddress;
            if(!SharedLibraries.TryLoadLibrary(libraryName, out libraryAddress))
            {
                result = null;
                return false;
            }
            var functionAddress = SharedLibraries.GetSymbolAddress(libraryAddress, "__cxa_demangle");
            var delegateType = typeof(LibStdCppHelper.CxaDemangleDelegate);
            result = (LibStdCppHelper.CxaDemangleDelegate)Marshal.GetDelegateForFunctionPointer(functionAddress, delegateType);
            return true;
        }

        private static bool TryFindLibStdCppInDir(string gccPath, out string libraryPath)
        {
            libraryPath = null;
            if(!Directory.Exists(gccPath))
            {
                return false;
            }
            var gccDirectory = new DirectoryInfo(gccPath);
            // if the architecture is 64bit, use x86_64* prefix for gcc. Otherwise use any other
            var pointerSize = Marshal.SizeOf(typeof(IntPtr));
            DirectoryInfo[] architectureDirectory;
            if(pointerSize == 8)
            {
                architectureDirectory = gccDirectory.GetDirectories(x64Prefix + "*");
            }
            else
            {
                architectureDirectory = gccDirectory.GetDirectories().Where(x => !x.Name.StartsWith(x64Prefix, StringComparison.Ordinal)).ToArray();
            }
            if(architectureDirectory == null || architectureDirectory.Length == 0)
            {
                return false;
            }
            var directoriesToSearch = architectureDirectory.Concat(new []{ architectureDirectory.SelectMany(x => x.GetDirectories()).OrderByDescending(x => x.Name).FirstOrDefault() }).ToList();
            if(directoriesToSearch == null || directoriesToSearch.Count == 0)
            {
                return false;
            }
            foreach(var currentDirectory in directoriesToSearch)
            {
                var libToFind = currentDirectory.GetFiles(GetLibName() + "*").FirstOrDefault();
                if(libToFind != null)
                {
                    libraryPath = libToFind.FullName;
                    return true;
                }
            }
            return false;
        }

        private static string GetLibName()
        {
            return BareLibraryName + (Misc.IsOnOsX ? ".dylib" : ".so");
        }

        private static readonly string[] gccPaths = 
        {
            "/lib",
            "/usr/lib/",
            "/usr/lib/gcc",
            "/usr/lib64/gcc",
            "/usr/lib32/gcc",
            "/usr/local/lib/gcc"
        };
        private const string x64Prefix = "x86_64";
        private const string BareLibraryName = "libstdc++";

        public delegate string CxaDemangleDelegate(String symbol, IntPtr p1, IntPtr p2, out int result);
    }
}

