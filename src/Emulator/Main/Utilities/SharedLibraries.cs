//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
#pragma warning disable IDE0005
using System.ComponentModel;
#pragma warning restore IDE0005
using System.Linq;
using System.Runtime.InteropServices;

using ELFSharp.ELF.Sections;

using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public static class SharedLibraries
    {
        /// <summary>
        /// Loads the given library to memory.
        /// </summary>
        /// <returns>
        /// The address of the loaded library.
        /// </returns>
        /// <param name='path'>
        /// Path to the library file.
        /// </param>
        /// <param name='relocation'>
        /// Whether relocation should be done immediately after loading or being deferred (lazy).
        /// The default option is to relocate immediately.
        /// </param>
        public static IntPtr LoadLibrary(string path)
        {
            IntPtr address;
            if(!TryLoadLibrary(path, out address))
            {
                HandleError("opening");
            }
            return address;
        }

        public static bool TryLoadLibrary(string path, out IntPtr address)
        {
            if(RuntimeInfo.IsWindows())
            {
                address = WindowsLoadLibrary(path);
            }
            else if(RuntimeInfo.IsMacOS())
            {
                //HACK: returns 0 on first call, somehow
                dlerrorMacOS();
                // RTLD_LOCAL prevents loaded symbols from being "made available to resolve references
                // in subsequently loaded shared objects.". It's the default on Linux.
                //
                // With the alternative RTLD_GLOBAL flag, which is the default on macOS, tlib's weak
                // arch-independent functions that are implemented only for some architectures will be
                // resolved by implementations from previously loaded tlib for other architectures.
                // This is certainly an unwanted behavior so we need to pass RTLD_LOCAL on macOS.
                address = dlopenMacOS(path, RTLD_NOW | RTLD_LOCAL);
            }
            else
            {
                dlerrorLinux();

                address = dlopenLinux(path, RTLD_NOW);
            }

            return address != IntPtr.Zero;
        }

        /// <summary>
        /// Unloads the library and frees memory taken by it.
        /// </summary>
        /// <param name='address'>
        /// Address of the library, returned by the <see cref="LoadLibrary" /> function.
        /// </param>
        public static void UnloadLibrary(IntPtr address)
        {
            bool result;
            if(RuntimeInfo.IsWindows())
            {
                result = WindowsCloseLibrary(address);
            }
            else if(RuntimeInfo.IsMacOS())
            {
                result = dlcloseMacOS(address) == 0;
            }
            else
            {
                result = dlcloseLinux(address) == 0;
            }
            if(!result)
            {
                HandleError("unloading");
            }
        }

        /// <summary>
        /// Gets all exported symbol names for a given library.
        /// </summary>
        /// <returns>
        /// Exported symbol names.
        /// </returns>
        /// <param name='path'>
        /// Path to a library file.
        /// </param>
        /// <remarks>
        /// Currently it works only with ELF files.
        /// </remarks>
        public static IEnumerable<string> GetAllSymbols(string path)
        {
            ELFSharp.MachO.MachO machO;
            if(ELFSharp.MachO.MachOReader.TryLoad(path, out machO) == ELFSharp.MachO.MachOResult.OK)
            {
                var machoSymtab = machO.GetCommandsOfType<ELFSharp.MachO.SymbolTable>().Single();
                // it can happen that binary contain multiple entries for a single symbol name,
                // so we should filter it out here
                return machoSymtab.Symbols.Select(x => x.Name.TrimStart('_')).Distinct();
            }
            ELFSharp.PE.PE pe;
            if(ELFSharp.PE.PEReader.TryLoad(path, out pe))
            {
                return pe.GetExportedSymbols();
            }
            using(var elf = ELFUtils.LoadELF(path))
            {
                var symtab = (ISymbolTable)elf.GetSection(".symtab");
                return symtab.Entries.Select(x => x.Name);
            }
        }

        /// <summary>
        /// Gets the address of the symbol with a given name.
        /// </summary>
        /// <returns>
        /// The address of the symbol in memory.
        /// </returns>
        /// <param name='libraryAddress'>
        /// Address to library returned by the <see cref="LoadLibrary" /> function.
        /// </param>
        /// <param name='name'>
        /// Name of the symbol to retrieve.
        /// </param>
        public static IntPtr GetSymbolAddress(IntPtr libraryAddress, string name)
        {
            IntPtr address;
            if(RuntimeInfo.IsWindows())
            {
                address = WindowsGetSymbolAddress(libraryAddress, name);
            }
            else if(RuntimeInfo.IsMacOS())
            {
                address = dlsymMacOS(libraryAddress, name);
            }
            else
            {
                address = dlsymLinux(libraryAddress, name);
            }

            if(address == IntPtr.Zero)
            {
                HandleError("getting symbol from");
            }
            return address;
        }

        private static void HandleError(string operation)
        {
            string message = null;
            if(RuntimeInfo.IsWindows())
            {
                var errno = Marshal.GetLastWin32Error();
                //For an unknown reason, in some cases, Windows doesn't set error code.
                if(errno != 0)
                {
                    message = new Win32Exception(errno).Message;
                }
            }
            else
            {
                var messagePtr = RuntimeInfo.IsMacOS() ? dlerrorMacOS() : dlerrorLinux();
                if(messagePtr != IntPtr.Zero)
                {
                    message = Marshal.PtrToStringAuto(messagePtr);
                }
            }
            throw new InvalidOperationException(string.Format("Error while {1} dynamic library: {0}", message ?? "unknown error", operation));
        }

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
        private static extern IntPtr WindowsGetSymbolAddress(IntPtr hModule, string symbolName);

        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary")]
        private static extern bool WindowsCloseLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", EntryPoint = "GetLastError")]
        private static extern UInt32 WindowsGetLastError();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "LoadLibrary")]
        private static extern IntPtr WindowsLoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopenLinux(string file, int mode);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr dlerrorLinux();

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr dlsymLinux(IntPtr handle, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlclose")]
        private static extern int dlcloseLinux(IntPtr handle);

        [DllImport("dl", EntryPoint = "dlopen")]
        private static extern IntPtr dlopenMacOS(string file, int mode);

        [DllImport("dl", EntryPoint = "dlerror")]
        private static extern IntPtr dlerrorMacOS();

        [DllImport("dl", EntryPoint = "dlsym")]
        private static extern IntPtr dlsymMacOS(IntPtr handle, string name);

        [DllImport("dl", EntryPoint = "dlclose")]
        private static extern int dlcloseMacOS(IntPtr handle);

        // Source: https://opensource.apple.com/source/dyld/dyld-239.3/include/dlfcn.h.auto.html
        // Source: https://sourceware.org/git/?p=glibc.git;a=blob;f=bits/dlfcn.h;hb=HEAD
        private const int RTLD_NOW = 2;
        private const int RTLD_LOCAL = 4;
    }
}