namespace AzureFunctionsExtension.Tests;

using System.Reflection;

using AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

using static AzureFunctionsExtension.Tests.CompilationHelper;

public class DiagnosticTests
{
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

    // ------------------------------------------------------------
    // Class definition (00xx)
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
    // Filter (01xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0101FilterTypeDoesNotImplementIFunctionFilterEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0101");
    }

    // ------------------------------------------------------------
    // Function (02xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0201UndefinedAuthorizationLevelEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0201");
    }

    [Fact]
    public void Afe0202HandlerHasMultipleEndpointAttributesEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0202");
    }

    [Fact]
    public void Afe0203HandlerIsGenericEmitsDiagnostic()
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

        Assert.Equal(["AFE0203"], problems);
    }

    [Fact]
    public void Afe0204QueueHandlerHasMultipleTriggerPayloadsEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0204");
    }

    [Fact]
    public void Afe0204MultipleTriggerPayloadsForSingleTriggerHandlerEmitsNoDiagnostic()
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

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "AFE0204");
    }

    [Fact]
    public void Afe0205HandlerIsOverloadedEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0205");
    }

    [Fact]
    public void Afe0205HandlerNameDiffersOnlyInCaseEmitsDiagnostic()
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

        Assert.Equal(["AFE0205"], problems);
    }

    [Fact]
    public void Afe0205HandlerNameDuplicatedAcrossClassesEmitsDiagnostic()
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

        Assert.Equal(["AFE0205", "AFE0205"], problems);
    }

    [Fact]
    public void Afe0205HandlerNameDuplicatedWithWorkerFunctionEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Azure.Functions.Worker;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "hello")]
                public IActionResult Hello() => new EmptyResult();
            }

            public static class PlainFunction
            {
                [Function("hello")]
                public static IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0205"], problems);
    }

    [Fact]
    public void Afe0205HandlerNameNotDuplicatedWithWorkerFunctionEmitsNoDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Azure.Functions.Worker;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "hello")]
                public IActionResult Hello() => new EmptyResult();
            }

            public static class PlainFunction
            {
                [Function("plain")]
                public static IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Empty(problems);
    }

    [Fact]
    public void Afe0206BaseClassHandlerIsReportedOnce()
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

        var diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "AFE0206");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    // ------------------------------------------------------------
    // Parameter (03xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0301HttpOnlyBindingUsedOnNonHttpHandlerEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0301");
    }

    [Fact]
    public void Afe0301CookieBindingUsedOnNonHttpHandlerEmitsDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("my-queue")]
                public void Run([FromCookie("session")] string session)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0301");
    }

    [Theory]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]")]
    [InlineData("[QueueEndpoint(\"my-queue\")]")]
    public void Afe0301ClaimsPrincipalUsedOnNonHttpHandlerEmitsDiagnostic(string attribute)
    {
        var source = $$"""
            namespace TestFunctions;

            using System.Security.Claims;
            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                {{attribute}}
                public void Run(ClaimsPrincipal user)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0301");
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "AFE0304");
    }

    [Theory]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]")]
    [InlineData("[QueueEndpoint(\"my-queue\")]")]
    public void Afe0301HttpRequestUsedOnNonHttpHandlerEmitsDiagnostic(string attribute)
    {
        var source = $$"""
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Http;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                {{attribute}}
                public void Run(HttpRequest req)
                {
                }
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0301"], problems);
    }

    [Fact]
    public void Afe0301ClaimsPrincipalOnHttpHandlerEmitsNoDiagnostic()
    {
        const string source =
            """
            namespace TestFunctions;

            using System.Security.Claims;
            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run(ClaimsPrincipal user) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Empty(problems);
    }

    [Fact]
    public void Afe0302MultipleBindingAttributesAreAppliedEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0302");
    }

    [Fact]
    public void Afe0303TextBindingTypeIsUnsupportedEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0303");
    }

    [Fact]
    public void Afe0303NullableHttpRequestEmitsNoDiagnostic()
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

    [Theory]
    [InlineData("sample", "[AzureFunctionsExtension.Annotations.FromQuery] int[][] ids")]
    [InlineData("sample", "[AzureFunctionsExtension.Annotations.FromQuery] int[,] ids")]
    [InlineData("sample", "[AzureFunctionsExtension.Annotations.FromHeader(\"x-ids\")] int[][] ids")]
    [InlineData("sample", "[AzureFunctionsExtension.Annotations.FromHeader(\"x-ids\")] int[,] ids")]
    [InlineData("items/{ids}", "[AzureFunctionsExtension.Annotations.FromRoute] int[][] ids")]
    [InlineData("items/{ids}", "[AzureFunctionsExtension.Annotations.FromRoute] int[,] ids")]
    public void Afe0303JaggedOrMultidimensionalArrayEmitsDiagnostic(string route, string parameter)
    {
        var source = $$"""
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "{{route}}")]
                public IActionResult Run({{parameter}}) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0303"], problems);
    }

    [Theory]
    [InlineData("string[] values")]
    [InlineData("int[] values")]
    public void Afe0303CookieArrayEmitsDiagnostic(string parameter)
    {
        var source = $$"""
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "sample")]
                public IActionResult Run([FromCookie("values")] {{parameter}}) => new EmptyResult();
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Equal(["AFE0303"], problems);
    }

    [Theory]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]", "string payload")]
    [InlineData("[QueueEndpoint(\"queue\")]", "Payload payload")]
    public void Afe0304UnsupportedTriggerPayloadEmitsDiagnostic(string attribute, string parameter)
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0304");
    }

    [Fact]
    public void Afe0305MvcBindingAttributeEmitsWarning()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0305");
    }

    [Fact]
    public void Afe0305ForeignCookieAttributeEmitsWarning()
    {
        const string source =
            """
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Parameter)]
                public sealed class FromCookieAttribute : System.Attribute
                {
                    public FromCookieAttribute(string name)
                    {
                        Name = name;
                    }

                    public string Name { get; }
                }
            }

            namespace TestFunctions
            {
                using AzureFunctionsExtension.Annotations;
                using Microsoft.AspNetCore.Mvc;

                [AzureFunction]
                public sealed partial class SampleFunction
                {
                    [HttpEndpoint("get", "sample")]
                    public IActionResult Run([Other.FromCookie("session")] string session) => new EmptyResult();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0305");
    }

    // ------------------------------------------------------------
    // Route (04xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0401RouteParameterIsNotBoundEmitsDiagnostic()
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

        Assert.Contains(result.Diagnostics, static d => d.Id == "AFE0401");
    }

    [Fact]
    public void Afe0401MissingRouteParameterWhenRouteVariableIsBoundEmitsNoDiagnostic()
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
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "AFE0401");
    }

    [Fact]
    public void Afe0401DoesNotStopGeneratingFunctions()
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

        Assert.Equal(["AFE0401"], GetProblemIds(source));
        Assert.Contains("Function(\"Ping\")", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains("Function(\"File\")", result.GeneratedCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Afe0401CanBeSuppressedAtFunction()
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
            #pragma warning disable AFE0401
                [HttpEndpoint("get", "files/{*path}")]
                public IActionResult File(HttpRequest request) => new EmptyResult();
            #pragma warning restore AFE0401
            }
            """;

        var problems = GetProblemIds(source);

        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // Generation (05xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0501CaseOnlyClassNamesGenerateTheFirstOnly()
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

        Assert.Contains("AFE0501", problems);
        Assert.DoesNotContain("CS8785", problems);
    }

    // ------------------------------------------------------------
    // Build configuration (06xx)
    // ------------------------------------------------------------

    [Fact]
    public void Afe0601NotUsingAzureFunctionsSdkEmitsDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["UsingAzureFunctionsSdk"] = string.Empty
        });

        Assert.Equal(["AFE0601"], problems);
    }

    [Fact]
    public void Afe0601UsingAzureFunctionsSdkEmitsNoDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["UsingAzureFunctionsSdk"] = "true"
        });

        Assert.Empty(problems);
    }

    [Fact]
    public void Afe0602MetadataSourceGenDisabledEmitsDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["FunctionsEnableMetadataSourceGen"] = "false"
        });

        Assert.Equal(["AFE0602"], problems);
    }

    [Fact]
    public void Afe0602MetadataProviderAutoRegisterDisabledEmitsDiagnostic()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["FunctionsAutoRegisterGeneratedMetadataProvider"] = "false"
        });

        Assert.Equal(["AFE0602"], problems);
    }

    [Fact]
    public void Afe0601IsReportedInsteadOfAfe0602()
    {
        var problems = GetProblemIds(WorkerSource, new Dictionary<string, string>
        {
            ["UsingAzureFunctionsSdk"] = string.Empty,
            ["FunctionsEnableMetadataSourceGen"] = "false"
        });

        Assert.Equal(["AFE0601"], problems);
    }

    [Fact]
    public void BuildConfigurationIsNotCheckedWithoutAzureFunctionClass()
    {
        const string source =
            """
            namespace TestFunctions;

            public sealed class PlainClass
            {
            }
            """;

        var problems = GetProblemIds(source, new Dictionary<string, string>
        {
            ["UsingAzureFunctionsSdk"] = string.Empty,
            ["FunctionsEnableMetadataSourceGen"] = "false"
        });

        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // Reporting
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
}
