//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Linq;
using ELFSharp;
using System.Collections.Generic;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using System.IO;
using System.Text;
using System.ComponentModel;

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
            if (!TryLoadLibrary(path, out address))
            {
                HandleError("opening");
            }
            return address;
        }

        public static bool TryLoadLibrary(string path, out IntPtr address)
        {
#if PLATFORM_WINDOWS
            address = WindowsLoadLibrary(path);
#else
            //HACK: returns 0 on first call, somehow
            dlerror();
            address = dlopen(path, 2); //relocation now (RTLD_NOW)
#endif
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
#if PLATFORM_WINDOWS
            var result = WindowsCloseLibrary(address);
            if (!result)
            {
                HandleError("unloading");
            }
#else
            var result = dlclose(address);
            if (result != 0)
            {
                HandleError("unloading");
            }
#endif
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
            var elf = ELFReader.Load(path);
            var symtab = (ISymbolTable)elf.GetSection(".symtab");
            return symtab.Entries.Select(x => x.Name);
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
#if PLATFORM_WINDOWS
            var address = WindowsGetSymbolAddress(libraryAddress, name);
#else
            var address = dlsym(libraryAddress, name);
#endif
            if (address == IntPtr.Zero)
            {
                HandleError("getting symbol from");
            }
            return address;
        }

        private static void HandleError(string operation)
        {
            string message = null;
#if PLATFORM_WINDOWS
            var errno = Marshal.GetLastWin32Error();
            //For an unknown reason, in some cases, Windows doesn't set error code.
            if(errno != 0)
            {
                message = new Win32Exception(errno).Message;
            }
#else
            var messagePtr = dlerror();
            if(messagePtr != IntPtr.Zero)
            {
                message = Marshal.PtrToStringAuto(messagePtr);
            }
#endif
            throw new InvalidOperationException(string.Format("Error while {1} dynamic library: {0}", message ?? "unknown error", operation));
        }

#if PLATFORM_WINDOWS
        [DllImport("kernel32", SetLastError=true, CharSet = CharSet.Ansi, EntryPoint="LoadLibrary")]
        static extern IntPtr WindowsLoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

        [DllImport("kernel32.dll", EntryPoint="GetProcAddress")]
        public static extern IntPtr WindowsGetSymbolAddress(IntPtr hModule, string symbolName);

        [DllImport("kernel32.dll", EntryPoint="FreeLibrary")]
        public static extern bool WindowsCloseLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", EntryPoint="GetLastError")]
        public static extern UInt32 WindowsGetLastError();
#elif PLATFORM_LINUX
        [DllImport("libdl.so.2")]
        private static extern IntPtr dlopen(string file, int mode);

        [DllImport("libdl.so.2")]
        private static extern IntPtr dlerror();

        [DllImport("libdl.so.2")]
        private static extern IntPtr dlsym(IntPtr handle, string name);

        [DllImport("libdl.so.2")]
        private static extern int dlclose(IntPtr handle);
#else
        [DllImport("dl")]
        private static extern IntPtr dlopen(string file, int mode);

        [DllImport("dl")]
        private static extern IntPtr dlerror();

        [DllImport("dl")]
        private static extern IntPtr dlsym(IntPtr handle, string name);

        [DllImport("dl")]
        private static extern int dlclose(IntPtr handle);
#endif
    }
}

