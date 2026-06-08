namespace AzureFunctionsExtension.Tests;

using static AzureFunctionsExtension.Tests.CompilationHelper;

public sealed class DiagnosticsTests
{
    //--------------------------------------------------------------------------------
    // AFE0001
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenClassIsNotPartial()
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

    //--------------------------------------------------------------------------------
    // AFE0002
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenClassIsGeneric()
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

    //--------------------------------------------------------------------------------
    // AFE0003
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenClassIsNested()
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

    //--------------------------------------------------------------------------------
    // AFE0004
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenTypeIsRecord()
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

    //--------------------------------------------------------------------------------
    // AFE0005
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenClassIsAbstract()
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

    //--------------------------------------------------------------------------------
    // AFE0006
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenFilterTypeDoesNotImplementIFunctionFilter()
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

    //--------------------------------------------------------------------------------
    // AFE0007
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenHandlerHasMultipleEndpointAttributes()
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

    //--------------------------------------------------------------------------------
    // AFE0008
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenHandlerIsOverloaded()
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

    //--------------------------------------------------------------------------------
    // AFE0009
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenHttpOnlyBindingUsedOnNonHttpHandler()
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

    //--------------------------------------------------------------------------------
    // AFE0010
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenQueueHandlerHasMultipleTriggerPayloads()
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
    public void DoesNotReportMultipleTriggerPayloadsForSingleTriggerHandler()
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

    //--------------------------------------------------------------------------------
    // AFE0011
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenMultipleBindingAttributesAreApplied()
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

    //--------------------------------------------------------------------------------
    // AFE0012
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenTextBindingTypeIsUnsupported()
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

    //--------------------------------------------------------------------------------
    // AFE0013
    //--------------------------------------------------------------------------------

    [Fact]
    public void ReportsDiagnosticWhenRouteParameterIsNotBound()
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
    public void DoesNotReportMissingRouteParameterWhenRouteVariableIsBound()
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
