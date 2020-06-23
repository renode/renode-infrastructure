//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace Antmicro.Renode.Utilities
{
    class AssemblyHelper
    {
        public static bool InitializeBundledAssemblies()
        {
            var result = false;
            for(var i = 0; i < BundledAssembliesCount; i++)
            {
                var bundledAssembly = GetBundledAssemblyById(i);
                bundledAssemblies.Add(bundledAssembly);
                result = true;
            }

            return result;
        }

        public static IEnumerable<string> GetBundledAssembliesNames()
        {
            return bundledAssemblies.Select(x => x.Name);
        }

        public static AssemblyDefinition GetBundledAssemblyByFullName(string fullName)
        {
            return bundledAssemblies.Select(x => x.Definition).FirstOrDefault(x => x.FullName == fullName);
        }

        public static AssemblyDefinition GetBundledAssemblyByName(string name)
        {
            return bundledAssemblies.FirstOrDefault(x => x.Name == name).Definition;
        }

        public static int BundledAssembliesCount
        {
            get
            {
                try
                {
                    return GetBundlesCountInternal();
                }
                catch
                {
                    // an exception means we couldn't locate
                    // `GetBundlesCount` function in this binary;
                    // that, in turn, means that there are no
                    // bundled assemblies - that's why we simply
                    // return 0
                    return 0;
                }
            }
        }

        private static BundledAssemblyDefinition GetBundledAssemblyById(int id)
        {
            unsafe
            {
                var result = new BundledAssemblyDefinition();
                using(var stream = new UnmanagedMemoryStream((byte*)GetBundleDataPointerInternal(id).ToPointer(), GetBundleDataSizeInternal(id)))
                {
                    result.Name = Marshal.PtrToStringAnsi(GetBundleNameInternal(id));
                    result.Definition = AssemblyDefinition.ReadAssembly(stream);
                }
                return result;
            }
        }

        [DllImport("__Internal", EntryPoint = "GetBundlesCount")]
        private static extern int GetBundlesCountInternal();

        [DllImport("__Internal", EntryPoint = "GetBundleName")]
        private static extern IntPtr GetBundleNameInternal(int id);

        [DllImport("__Internal", EntryPoint = "GetBundleDataSize")]
        private static extern UInt32 GetBundleDataSizeInternal(int id);

        [DllImport("__Internal", EntryPoint = "GetBundleDataPointer")]
        private static extern IntPtr GetBundleDataPointerInternal(int id);

        private static readonly List<BundledAssemblyDefinition> bundledAssemblies = new List<BundledAssemblyDefinition>();

        private struct BundledAssemblyDefinition
        {
            public string Name;
            public AssemblyDefinition Definition;
        }
    }
}
