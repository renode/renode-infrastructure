//
// Copyright (c) 2010-2022 Antmicro
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
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Antmicro.Renode.Utilities
{
    public class AdHocCompiler
    {
        public string Compile(string sourcePath)
        {
            var tempFilePath = TemporaryFilesManager.Instance.GetTemporaryFile();
            // With .NET Core and above, one must explicitly specify a .dll extension for output assembly
            var outputFilePath = Path.ChangeExtension(tempFilePath, ".dll");
            var outputFileName = Path.GetFileName(outputFilePath);

            var sourceCode = File.ReadAllText(sourcePath);
            var codeString = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            };

            AssemblyHelper.GetAssembliesLocations().ToList()
                .ForEach(location => references.Add(MetadataReference.CreateFromFile(location)));

            var result = CSharpCompilation.Create(outputFileName,
                new[] { parsedSyntaxTree }, 
                references: references, 
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, 
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)).Emit(outputFilePath);

            if (!result.Success) 
            {
                // Access diagnostic informations 
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var diagnosticString = string.Join(Environment.NewLine, failures.Select(x => x.ToString()));
                throw new RecoverableException($"Could not compile assembly from: {sourcePath}\n{diagnosticString}");      
            }

            return outputFilePath;
        }
    }
}