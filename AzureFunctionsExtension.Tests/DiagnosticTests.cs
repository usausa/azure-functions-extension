namespace AzureFunctionsExtension.Tests;

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
                public IActionResult Run([FromQuery][FromHeader] int id)
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
}
