//
// Copyright (c) 2010-2022 Antmicro
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
    public class AssemblyHelper
    {
        public static bool TryInitializeBundledAssemblies()
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

        public static IEnumerable<string> GetAssembliesLocations()
        {
            if(BundledAssembliesCount > 0)
            {
                foreach(var assembly in bundledAssemblies.Select(x => x.Location))
                {
                    yield return assembly;
                }
            }
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).Select(x => x.Location).Where(Path.IsPathRooted))
            {
                yield return assembly;
            }
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
                    result.Location = ExtractAssemblyToFile(stream, result.Name);
                }
                return result;
            }
        }

        private static string ExtractAssemblyToFile(Stream stream, string fileName)
        {
            var outputFile = TemporaryFilesManager.Instance.GetTemporaryFile(fileName);
            using(var fileStream = File.Create(outputFile, (int)stream.Length))
            {
                var bytesInStream = new byte[stream.Length];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(bytesInStream, 0, bytesInStream.Length);
                fileStream.Write(bytesInStream, 0, bytesInStream.Length);
            }
            return outputFile;
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
            public string Location;
        }
    }
}
