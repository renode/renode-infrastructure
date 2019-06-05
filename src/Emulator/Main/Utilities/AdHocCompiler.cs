//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using System.CodeDom.Compiler;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Antmicro.Migrant;

namespace Antmicro.Renode.Utilities
{
    public class AdHocCompiler
    {
        public string Compile(string sourcePath, IEnumerable<string> referencedLibraries = null)
        {
            using(var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                var outputFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                var parameters = new CompilerParameters { GenerateInMemory = false, GenerateExecutable = false, OutputAssembly = outputFileName };
                parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Machine)).Location); // Core
                parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Serializer)).Location); // Migrant
#if PLATFORM_LINUX
                parameters.CompilerOptions = "/langversion:experimental";
#endif
                if(referencedLibraries != null)
                {
                    foreach(var lib in referencedLibraries)
                    {
                        parameters.ReferencedAssemblies.Add(lib);
                    }
                }

                var result = provider.CompileAssemblyFromFile(parameters, new[] { sourcePath });
                if(result.Errors.HasErrors)
                {
                    var errors = result.Errors.Cast<object>().Aggregate(string.Empty,
                                                                        (current, error) => current + ("\n" + error));
                    throw new RecoverableException(string.Format("There were compilation errors:\n{0}", errors));
                }
                return outputFileName;
            }
        }
    }
}

