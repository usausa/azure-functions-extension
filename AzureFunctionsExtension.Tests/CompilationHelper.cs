namespace AzureFunctionsExtension.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using AzureFunctionsExtension.Annotations;
using AzureFunctionsExtension.Generator;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SourceGenerateHelper.Testing;

internal static class CompilationHelper
{
    private static GeneratorTestRunner Runner => GeneratorTestRunner
        .For<FunctionGenerator>()
        .WithReference(typeof(AzureFunctionAttribute).Assembly)
        .WithReference(typeof(FunctionContext).Assembly)
        .WithReference(typeof(HttpRequest).Assembly)
        .WithReference(typeof(IActionResult).Assembly)
        .WithReference(typeof(ILogger<>).Assembly)
        .WithReference(typeof(IServiceCollection).Assembly)
        .WithGlobalOption("build_property.UsingAzureFunctionsSdk", "true")
        .WithGlobalOption("build_property.FunctionsEnableMetadataSourceGen", "true")
        .WithGlobalOption("build_property.FunctionsAutoRegisterGeneratedMetadataProvider", "true");

    public static GeneratorResult RunGenerator(string source) =>
        RunGenerator(Runner, source);

    public static GeneratorResult RunGenerator(string source, string assemblyName) =>
        RunGenerator(Runner.WithAssemblyName(assemblyName), source);

    private static GeneratorResult RunGenerator(GeneratorTestRunner runner, string source)
    {
        var result = runner.Run(source);

        return new GeneratorResult(
            result.GeneratorDiagnostics.ToImmutableArray(),
            result.GeneratedSources,
            result.AllGeneratedText);
    }

    public static (GeneratorDriver Driver, Compilation Compilation) CreateDriver(string source) =>
        Runner.CreateDriver(source);

    public static IReadOnlyList<string> GetProblemIds(string source) =>
        [.. Runner.GetProblems(source).Select(static x => x.Id)];

    public static IReadOnlyList<string> GetProblemIds(string source, IReadOnlyDictionary<string, string> properties)
    {
        var runner = Runner;
        foreach (var pair in properties)
        {
            runner = runner.WithGlobalOption($"build_property.{pair.Key}", pair.Value);
        }

        return [.. runner.GetProblems(source).Select(static x => x.Id)];
    }

    public static Assembly LoadAssembly(string source)
    {
        var result = Runner.Run(source);
        Assert.True(result.Problems.Count == 0, String.Join(Environment.NewLine, result.Problems.Select(static x => x.ToString())));

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(emitResult.Success, String.Join(Environment.NewLine, emitResult.Diagnostics.Select(static x => x.ToString())));

        stream.Position = 0;
        return new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
    }

    public static Task<IActionResult> InvokeAsync(Assembly assembly, string handler, HttpRequest request, FunctionContext context)
    {
        var method = assembly.GetType("Test.Function", throwOnError: true)!.GetMethod(handler)!;
        return (Task<IActionResult>)method.Invoke(null, [request, context])!;
    }

    public static void AssertNoGeneratorErrors(GeneratorResult result)
    {
        var errors = result.Diagnostics
            .Where(static x => x.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, String.Join(Environment.NewLine, errors.Select(static x => x.ToString())));
    }

    // ReSharper disable NotAccessedPositionalProperty.Global
    public sealed record GeneratorResult(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> Sources,
        string GeneratedCode);
    // ReSharper restore NotAccessedPositionalProperty.Global

    public static IncrementalRunResult RunIncremental(string source, string addedSource) =>
        Runner.WithTracking().RunIncremental(source, addedSource);
}
