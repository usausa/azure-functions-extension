namespace AzureFunctionsExtension.Tests;

using System.Reflection;

using AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

using static AzureFunctionsExtension.Tests.CompilationHelper;

public class DiagnosticTests
{
    // ------------------------------------------------------------
    // AFE0001
    // ------------------------------------------------------------

    [Fact]
    public void Afe0001ClassIsNotPartialEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0001");
    }

    // ------------------------------------------------------------
    // AFE0002
    // ------------------------------------------------------------

    [Fact]
    public void Afe0002ClassIsGenericEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction<T>
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0002");
    }

    // ------------------------------------------------------------
    // AFE0003
    // ------------------------------------------------------------

    [Fact]
    public void Afe0003ClassIsNestedEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            public static class Outer
            {
                [AzureFunction]
                public sealed partial class SampleFunction
                {
                    [HttpEndpoint("get", "sample")]
                    public IActionResult Run() => new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0003");
    }

    // ------------------------------------------------------------
    // AFE0004
    // ------------------------------------------------------------

    [Fact]
    public void Afe0004TypeIsRecordEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial record SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0004");
    }

    // ------------------------------------------------------------
    // AFE0005
    // ------------------------------------------------------------

    [Fact]
    public void Afe0005ClassIsAbstractEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public abstract partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0005");
    }

    // ------------------------------------------------------------
    // AFE0006
    // ------------------------------------------------------------

    [Fact]
    public void Afe0006FilterTypeDoesNotImplementIFunctionFilterEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            public sealed class NotAFilter
            {
            }

            [AzureFunction]
            [Filter<NotAFilter>]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0006");
    }

    // ------------------------------------------------------------
    // AFE0007
    // ------------------------------------------------------------

    [Fact]
    public void Afe0007HandlerHasMultipleEndpointAttributesEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                [TimerEndpoint("0 */5 * * * *")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0007");
    }

    // ------------------------------------------------------------
    // AFE0008
    // ------------------------------------------------------------

    [Fact]
    public void Afe0008HandlerIsOverloadedEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();

                [HttpEndpoint("post", "sample")]
                public IActionResult Run(int id) => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0008");
    }

    [Fact]
    public void Afe0008HandlerNameDiffersOnlyInCaseEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "a")]
                public IActionResult Run() => new EmptyResult();

                [HttpEndpoint("get", "b")]
                public IActionResult run() => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0008"], problems);
    }

    [Fact]
    public void Afe0008HandlerNameDuplicatedAcrossClassesEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class FirstFunction
            {
                [HttpEndpoint("get", "a")]
                public IActionResult Run() => new EmptyResult();
            }

            [AzureFunction]
            public sealed partial class SecondFunction
            {
                [HttpEndpoint("get", "b")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0008", "AFE0008"], problems);
    }

    // ------------------------------------------------------------
    // AFE0009
    // ------------------------------------------------------------

    [Fact]
    public void Afe0009HttpOnlyBindingUsedOnNonHttpHandlerEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("my-queue")]
                public void Run([FromQuery] int id)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0009");
    }

    // ------------------------------------------------------------
    // AFE0010
    // ------------------------------------------------------------

    [Fact]
    public void Afe0010QueueHandlerHasMultipleTriggerPayloadsEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("my-queue")]
                public void Run(string first, string second)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0010");
    }

    [Fact]
    public void Afe0010MultipleTriggerPayloadsForSingleTriggerHandlerEmitsNoDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("my-queue")]
                public void Run(string message)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "AFE0010");
    }

    // ------------------------------------------------------------
    // AFE0011
    // ------------------------------------------------------------

    [Fact]
    public void Afe0011MultipleBindingAttributesAreAppliedEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run([AzureFunctionsExtension.Annotations.FromQuery][AzureFunctionsExtension.Annotations.FromHeader] int id)
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0011");
    }

    // ------------------------------------------------------------
    // AFE0012
    // ------------------------------------------------------------

    [Fact]
    public void Afe0012TextBindingTypeIsUnsupportedEmitsDiagnostic()
    {
        const string source =
            """
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0012");
    }

    [Fact]
    public void Afe0012NullableHttpRequestEmitsNoDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run(HttpRequest? request) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // AFE0013
    // ------------------------------------------------------------

    [Fact]
    public void Afe0013RouteParameterIsNotBoundEmitsDiagnostic()
    {
        const string source =
            """
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0013");
    }

    [Fact]
    public void Afe0013MissingRouteParameterWhenRouteVariableIsBoundEmitsNoDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "items/{id}")]
                public IActionResult Run([AzureFunctionsExtension.Annotations.FromRoute] int id)
                {
                    return new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        AssertNoGeneratorErrors(result);
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "AFE0013");
    }

    [Fact]
    public void Afe0013DoesNotStopGeneratingFunctions()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "ping")]
                public IActionResult Ping() => new EmptyResult();

                [HttpEndpoint("get", "files/{*path}")]
                public IActionResult File(HttpRequest request) => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Equal(["AFE0013"], GetProblemIds(source));
        Assert.Contains("Function(\"Ping\")", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("Function(\"File\")", result.GeneratedCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Afe0013CanBeSuppressedAtFunction()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
            #pragma warning disable AFE0013
                [HttpEndpoint("get", "files/{*path}")]
                public IActionResult File(HttpRequest request) => new EmptyResult();
            #pragma warning restore AFE0013
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // AFE0014
    // ------------------------------------------------------------

    [Fact]
    public void Afe0014HandlerIsGenericEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("items")]
                public void Run<T>([FromTrigger] string message)
                {
                }
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0014"], problems);
    }

    // ------------------------------------------------------------
    // AFE0015
    // ------------------------------------------------------------

    private const string WorkerSource =
        """
        namespace TestFunctions;

        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        [AzureFunction]
        public sealed partial class SampleFunction
        {
            [HttpEndpoint("get", "sample")]
            public IActionResult Run() => new EmptyResult();
        }
        """;

    [Fact]
    public void Afe0015WorkerMetadataSourceGenEnabledEmitsDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["FunctionsEnableMetadataSourceGen"] = "true",
            ["FunctionsAutoRegisterGeneratedMetadataProvider"] = "true",
            ["FunctionsEnableExecutorSourceGen"] = "false",
            ["FunctionsAutoRegisterGeneratedFunctionsExecutor"] = "true"
        });

        Assert.Equal(["AFE0015"], problems);
    }

    [Fact]
    public void Afe0015WorkerExecutorSourceGenEnabledEmitsDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["FunctionsEnableMetadataSourceGen"] = "false",
            ["FunctionsAutoRegisterGeneratedMetadataProvider"] = "false",
            ["FunctionsEnableExecutorSourceGen"] = "true",
            ["FunctionsAutoRegisterGeneratedFunctionsExecutor"] = "true"
        });

        Assert.Equal(["AFE0015"], problems);
    }

    [Fact]
    public void Afe0015WorkerSourceGenDisabledEmitsNoDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["FunctionsEnableMetadataSourceGen"] = "false",
            ["FunctionsAutoRegisterGeneratedMetadataProvider"] = "false",
            ["FunctionsEnableExecutorSourceGen"] = "false",
            ["FunctionsAutoRegisterGeneratedFunctionsExecutor"] = "true"
        });

        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // Suppression
    // ------------------------------------------------------------

    [Fact]
    public void ErrorsCannotBeSuppressed()
    {
        var descriptors = typeof(Diagnostics)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(static x => (DiagnosticDescriptor)x.GetValue(null)!)
            .ToList();

        Assert.All(
            descriptors.Where(static x => x.DefaultSeverity == DiagnosticSeverity.Error),
            static x => Assert.Equal([WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler], x.CustomTags));
        Assert.All(
            descriptors.Where(static x => x.DefaultSeverity != DiagnosticSeverity.Error),
            static x => Assert.Empty(x.CustomTags));
    }

    // ------------------------------------------------------------
    // AFE0003 (file-local)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0003FileLocalClassEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            file sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0003");
    }

    // ------------------------------------------------------------
    // AFE0016
    // ------------------------------------------------------------

    [Theory]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]", "string payload")]
    [InlineData("[QueueEndpoint(\"queue\")]", "Payload payload")]
    public void Afe0016UnsupportedTriggerPayloadEmitsDiagnostic(string attribute, string parameter)
    {
        var source = $$"""
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            public sealed class Payload
            {
                public int Id { get; set; }
            }

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                {{attribute}}
                public void Run({{parameter}})
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0016");
    }

    // ------------------------------------------------------------
    // AFE0017
    // ------------------------------------------------------------

    [Fact]
    public void Afe0017BaseClassHandlerIsReportedOnce()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            public abstract class FunctionBase
            {
                [HttpEndpoint("get", "base")]
                public IActionResult Base() => new EmptyResult();
            }

            [AzureFunction]
            public sealed partial class FirstFunction : FunctionBase
            {
                [HttpEndpoint("get", "first")]
                public IActionResult First() => new EmptyResult();
            }

            [AzureFunction]
            public sealed partial class SecondFunction : FunctionBase
            {
                [HttpEndpoint("get", "second")]
                public IActionResult Second() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        var diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "AFE0017");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    // ------------------------------------------------------------
    // AFE0018
    // ------------------------------------------------------------

    [Fact]
    public void Afe0018MvcBindingAttributeEmitsWarning()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run([Microsoft.AspNetCore.Mvc.FromHeader(Name = "x-id")] string id) => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0018");
    }

    // ------------------------------------------------------------
    // AFE0019
    // ------------------------------------------------------------

    [Fact]
    public void Afe0019UndefinedAuthorizationLevelEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Azure.Functions.Worker;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample", (AuthorizationLevel)9)]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0019");
    }

    // ------------------------------------------------------------
    // AFE0020
    // ------------------------------------------------------------

    [Fact]
    public void Afe0020CaseOnlyClassNamesGenerateTheFirstOnly()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "first")]
                public IActionResult First() => new EmptyResult();
            }

            [AzureFunction]
            public sealed partial class sampleFunction
            {
                [HttpEndpoint("get", "second")]
                public IActionResult Second() => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Contains("AFE0020", problems);
        Assert.DoesNotContain("CS8785", problems);
    }
}
