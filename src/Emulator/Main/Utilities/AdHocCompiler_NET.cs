//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Renode.Exceptions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Antmicro.Renode.Utilities
{
    public class AdHocCompiler
    {
        public string Compile(string[] sourcePaths)
        {
            var tempFilePath = TemporaryFilesManager.Instance.GetTemporaryFile();
            // With .NET Core and above, one must explicitly specify a .dll extension for output assembly
            var outputFilePath = Path.ChangeExtension(tempFilePath, ".dll");
            var outputFileName = Path.GetFileName(outputFilePath);
            var options = CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.CSharp9)
                .WithPreprocessorSymbols("NET");

            var parsedSyntaxTrees = new List<SyntaxTree> { };

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            };

            foreach(string sourcePath in sourcePaths)
            {
                var sourceCode = File.ReadAllText(sourcePath);
                var codeString = SourceText.From(sourceCode);

                parsedSyntaxTrees.Add(SyntaxFactory.ParseSyntaxTree(codeString, options, sourcePath));
            }

            AssemblyHelper.GetAssembliesLocations().ToList()
                .ForEach(location => references.Add(MetadataReference.CreateFromFile(location)));

            var result = CSharpCompilation.Create(outputFileName,
                syntaxTrees: parsedSyntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: true,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)).Emit(outputFilePath);

            if(!result.Success)
            {
                // Access diagnostic informations
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var diagnosticString = string.Join(Environment.NewLine, failures);
                var sourcesString = string.Join(", ", sourcePaths);
                throw new RecoverableException($"Could not compile assembly from: {sourcesString}\n{diagnosticString}");
            }

            return outputFilePath;
        }
    }
}
