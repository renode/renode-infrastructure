//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
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
            var outputFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            var parameters = new List<string>();

            if(AssemblyHelper.BundledAssembliesCount > 0)
            {
                // portable already has all the libs included
                parameters.Add("/nostdlib+");
            }

            if(Environment.OSVersion.Platform == PlatformID.Unix)
            {
                parameters.Add("/langversion:experimental");
            }

            var locations = AssemblyHelper.GetAssembliesLocations();
            foreach(var location in locations)
            {
                parameters.Add($"/r:{location}");
            }

            parameters.Add("/target:library");
            parameters.Add("/debug-");
            parameters.Add("/optimize+");
            parameters.Add($"/out:{outputFileName}");
            parameters.Add("/noconfig");

            parameters.Add("--");
            parameters.Add(sourcePath);

            var result = false;
            var errorOutput = new StringWriter();
            try
            {
                result = Mono.CSharp.CompilerCallableEntryPoint.InvokeCompiler(parameters.ToArray(), errorOutput);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Could not compile assembly: {e.Message}");
            }

            if(!result)
            {
                throw new RecoverableException($"There were compilation errors:\n{(errorOutput.ToString())}");
            }

            return outputFileName;
        }
    }
}

