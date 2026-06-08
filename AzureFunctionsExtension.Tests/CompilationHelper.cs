namespace AzureFunctionsExtension.Tests;

using System.Collections.Immutable;

using AzureFunctionsExtension.Annotations;
using AzureFunctionsExtension.Generator;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

internal static class CompilationHelper
{
    private const string GlobalUsings =
        """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    public static void AssertNoGeneratorErrors(GeneratorResult result)
    {
        var errors = result.Diagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(static d => d.ToString())));
    }

    public static GeneratorResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var globalUsings = CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree, globalUsings],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FunctionGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();
        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Concat(runResult.Diagnostics)
            .Distinct()
            .ToImmutableArray();
        var sources = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .ToDictionary(static s => s.HintName, static s => s.SourceText.ToString());
        var generatedCode = string.Join(
            Environment.NewLine + Environment.NewLine,
            runResult.Results
                .SelectMany(static r => r.GeneratedSources)
                .Select(static s => s.SourceText.ToString()));

        return new GeneratorResult(diagnostics, sources, generatedCode);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator)
            ?? [];
        var assemblyPaths = new HashSet<string>(trustedAssemblies, StringComparer.OrdinalIgnoreCase)
        {
            typeof(IServiceCollection).Assembly.Location,
            typeof(ILogger<>).Assembly.Location,
            typeof(AzureFunctionAttribute).Assembly.Location,
            typeof(FunctionGenerator).Assembly.Location,
            typeof(FunctionContext).Assembly.Location,
            typeof(HttpRequest).Assembly.Location,
            typeof(IActionResult).Assembly.Location
        };

        return [.. assemblyPaths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }

    public sealed record GeneratorResult(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> Sources,
        string GeneratedCode);
}
