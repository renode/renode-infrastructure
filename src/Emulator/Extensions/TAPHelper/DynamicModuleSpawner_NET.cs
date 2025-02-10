//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.TAPHelper
{
    public class DynamicModuleSpawner
    {
        public static string GetTAPHelper()
        {			
            var generatedFilePath = TemporaryFilesManager.Instance.GetTemporaryFile();
            var outputFilePath = Path.ChangeExtension(generatedFilePath, ".dll");
            var outputFileName = Path.GetFileName(outputFilePath);

            GenerateTAPHelper(outputFilePath, outputFileName);

            // Generate runtimeconfig.json file necessary to run standalone application with dotnet on .NETCore and above
            File.WriteAllText(
                Path.ChangeExtension(generatedFilePath, "runtimeconfig.json"),
                GenerateRuntimeConfig()
            );

            // Copy Infrastructure.dll to temp directory
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var targetPath = Path.Combine(TemporaryFilesManager.Instance.EmulatorTemporaryPath, "Infrastructure.dll");
            File.Copy(currentAssemblyPath, targetPath, true);

            return outputFilePath;
        }

        private static void GenerateTAPHelper(string path, string filename)
        {
            var sourceCode = @"
                using System.Runtime.InteropServices;
                using Antmicro.Renode.TAPHelper;
                public class TAP
                {
                    public static int Main(string[] args)
                    {
                        var deviceName = args[0];
                        var persistent = bool.Parse(args[1]);
                        var dev = Marshal.StringToCoTaskMemAuto(deviceName);
                        var err = TAPTools.OpenTAP(dev, persistent);
                        Marshal.FreeCoTaskMem(dev);
                        if (err < 0)
                        {
                            return 1;
                        }
                        return 0;
                    }
                }";
            var codeString = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            };

            AssemblyHelper.GetAssembliesLocations().ToList()
                .ForEach(location => references.Add(MetadataReference.CreateFromFile(location)));

            var result = CSharpCompilation.Create(filename,
                new[] { parsedSyntaxTree }, 
                references: references, 
                options: new CSharpCompilationOptions(
                    OutputKind.ConsoleApplication,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)).Emit(path);

            if (!result.Success) 
            {
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var diagnosticString = string.Join(Environment.NewLine, failures.Select(x => x.ToString()));
                throw new RecoverableException("Could not compile TAP assembly. \n" + diagnosticString);
            }
        }

        private static string GenerateRuntimeConfig()
        {
            // It writes JSON of the following form:
            // {
            //   "runtimeOptions": {
            //     "framework": {
            //       "name": "Microsoft.NETCore.App",
            //       "version": "5.0.5"
            //     }
            //   }
            // }
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(
                    stream,
                    new JsonWriterOptions() { Indented = true }
                ))
                {
                    writer.WriteStartObject();
                    writer.WriteStartObject("runtimeOptions");
                    writer.WriteStartObject("framework");
                    writer.WriteString("name", "Microsoft.NETCore.App");
                    writer.WriteString(
                        "version",
                        RuntimeInformation.FrameworkDescription.Replace(".NET ", "")
                    );
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private DynamicModuleSpawner()
        {
        }
    }
}