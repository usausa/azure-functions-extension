namespace AzureFunctionsExtension.Tests;

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using AzureFunctionsExtension.Annotations;
using AzureFunctionsExtension.Generator;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

public sealed class FunctionGeneratorTests
{
    [Fact]
    public void GeneratesWrappersUsingFunctionContextServicesAndCancellationToken()
    {
        const string source = """
            namespace TestFunctions;

            using System.Threading;
            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Extensions.Logging;

            public interface IClock
            {
            }

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                private readonly ILogger<SampleFunction> log;

                public SampleFunction(ILogger<SampleFunction> log)
                {
                    this.log = log;
                }

                [HttpEndpoint("get", "sample")]
                public IActionResult Run(CancellationToken cancellationToken, [AzureFunctionsExtension.Annotations.FromServices] IClock clock)
                {
                    _ = log;
                    _ = cancellationToken;
                    _ = clock;
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        AssertNoGeneratorErrors(result);
        Assert.Contains("var services = context.InstanceServices;", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("ActivatorUtilities.CreateInstance<global::TestFunctions.SampleFunction>(services)", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("context.CancellationToken", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<global::TestFunctions.IClock>(services)", result.GeneratedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("__provider__", result.GeneratedCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsDiagnosticWhenMultipleBindingAttributesAreApplied()
    {
        const string source = """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run([FromQuery][FromHeader] int id)
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0004");
    }

    [Fact]
    public void ReportsDiagnosticWhenTextBindingTypeIsUnsupported()
    {
        const string source = """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            public sealed class Payload
            {
                public int Id { get; set; }
            }

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run([FromQuery] Payload payload)
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0006");
    }

    [Fact]
    public void ReportsDiagnosticWhenRouteParameterIsNotBound()
    {
        const string source = """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "items/{id}")]
                public IActionResult Run()
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0010");
    }

    [Fact]
    public void GeneratesBadRequestHandlingForInvalidOrMissingRequestBody()
    {
        const string source = """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            public sealed class Payload
            {
                public string Name { get; set; } = string.Empty;
            }

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("post", "sample")]
                public IActionResult Run([AzureFunctionsExtension.Annotations.FromBody] Payload payload)
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        AssertNoGeneratorErrors(result);
        Assert.Contains("catch (global::System.Text.Json.JsonException)", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("Request body is required.", result.GeneratedCode, StringComparison.Ordinal);
    }

    private static void AssertNoGeneratorErrors(GeneratorTestResult result)
    {
        var errors = result.Diagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(static d => d.ToString())));
    }

    private static GeneratorTestResult RunGenerator(string source)
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
            typeof(ILogger<>).Assembly.Location,
        };

        return [.. assemblyPaths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }

    private sealed record GeneratorTestResult(
        ImmutableArray<Diagnostic> Diagnostics,
        string GeneratedCode);
}
