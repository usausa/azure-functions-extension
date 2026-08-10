namespace AzureFunctionsExtension.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
        .WithReference(typeof(IServiceCollection).Assembly);

    public static GeneratorResult RunGenerator(string source)
    {
        var result = Runner.Run(source);

        return new GeneratorResult(
            [.. result.GeneratorDiagnostics],
            result.GeneratedSources,
            result.AllGeneratedText);
    }

    public static void AssertNoGeneratorErrors(GeneratorResult result)
    {
        var errors = result.Diagnostics
            .Where(static x => x.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, String.Join(Environment.NewLine, errors.Select(static x => x.ToString())));
    }

    public sealed record GeneratorResult(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyDictionary<string, string> Sources,
        string GeneratedCode);
}
