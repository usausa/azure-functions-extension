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

// テスト用のコンパイルヘルパー。
// ソースコードを Roslyn でコンパイルし、FunctionGenerator を実行して生成コードと診断を取得する。
internal static class CompilationHelper
{
    internal static void AssertNoGeneratorErrors(GeneratorTestResult result)
    {
        var errors = result.Diagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(static d => d.ToString())));
    }

    internal static GeneratorTestResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new FunctionGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();
        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Concat(runResult.Diagnostics)
            .Distinct()
            .ToImmutableArray();
        var generatedCode = string.Join(
            Environment.NewLine + Environment.NewLine,
            runResult.Results
                .SelectMany(static x => x.GeneratedSources)
                .Select(static x => x.SourceText.ToString()));

        return new GeneratorTestResult(diagnostics, generatedCode);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator)
            ?? [];
        var assemblyPaths = new HashSet<string>(trustedAssemblies, StringComparer.OrdinalIgnoreCase)
        {
            typeof(AzureFunctionAttribute).Assembly.Location,
            typeof(FunctionGenerator).Assembly.Location,
            typeof(FunctionContext).Assembly.Location,
            typeof(HttpRequest).Assembly.Location,
            typeof(IActionResult).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
            typeof(ILogger<>).Assembly.Location
        };

        return [.. assemblyPaths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }

    internal sealed record GeneratorTestResult(
        ImmutableArray<Diagnostic> Diagnostics,
        string GeneratedCode);
}
