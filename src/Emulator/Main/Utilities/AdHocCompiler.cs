//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public class AdHocCompiler
    {
        public string Compile(string sourcePath)
        {
            using(var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                var blacklist = new List<string> { "mscorlib", "System." };
                var outputFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                var parameters = new CompilerParameters { GenerateInMemory = false, GenerateExecutable = false, OutputAssembly = outputFileName };
#if PLATFORM_LINUX
                parameters.CompilerOptions = "/langversion:experimental";
#endif
                var locations = AssemblyHelper.GetAssembliesLocations();
                if(AssemblyHelper.BundledAssembliesCount > 0)
                {
                    // Assigning any non-empty string to this property prevents the compiler from referencing mscorlib.dll
                    parameters.CoreAssemblyFileName = "some bogus string";
                    locations = locations.Where(x => !blacklist.Any(x.Contains));
                }
                foreach(var location in locations)
                {
                    parameters.ReferencedAssemblies.Add(location);
                }

                var result = provider.CompileAssemblyFromFile(parameters, new[] { sourcePath });
                if(result.Errors.HasErrors)
                {
                    var errors = result.Errors.Cast<object>().Aggregate(string.Empty,
                                                                        (current, error) => current + ("\n" + error));
                    throw new RecoverableException(string.Format("There were compilation errors:\n{0}", errors));
                }
                try
                {
                    // Try to read any information from the compiled assembly to check if we have silently failed
                    var name = result.CompiledAssembly.FullName;
                }
                catch(Exception e)
                {
                    throw new RecoverableException(string.Format("Could not compile assembly: {0}", e.Message));
                }

                return outputFileName;
            }
        }
    }
}

