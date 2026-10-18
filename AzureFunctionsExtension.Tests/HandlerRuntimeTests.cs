namespace AzureFunctionsExtension.Tests;

using Microsoft.Extensions.Primitives;

public sealed class HandlerRuntimeTests
{
    private const string Head =
        """
        namespace Test;

        using System.Threading;
        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;

        public sealed class PassFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
        }

        public sealed class Payload
        {
            public string Name { get; set; } = string.Empty;
        }

        """;

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task HandlerReceivesBoundValues(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("get", "items/{id}")]
                public IActionResult Get(
                    [AzureFunctionsExtension.Annotations.FromRoute] int id,
                    [AzureFunctionsExtension.Annotations.FromQuery] string? name,
                    [AzureFunctionsExtension.Annotations.FromHeader("x-code")] int code) => new OkObjectResult($"{id}:{name}:{code}");
            }
            """);
        var services = HandlerTestHost.CreateServices();
        var request = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["name"] = "abc" },
            route: new Dictionary<string, object?> { ["id"] = "5" },
            headers: new Dictionary<string, StringValues> { ["x-code"] = "7" });

        var result = await CompilationHelper.InvokeAsync(assembly, "Get_Handler", request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal("5:abc:7", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task InvalidValueReturnsBadRequest(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("get", "query")]
                public IActionResult Get([AzureFunctionsExtension.Annotations.FromQuery] int a) => new OkResult();
            }
            """);
        var services = HandlerTestHost.CreateServices();
        var request = HandlerTestHost.CreateRequest(services, query: new Dictionary<string, StringValues> { ["a"] = "x" });

        var result = await CompilationHelper.InvokeAsync(assembly, "Get_Handler", request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
        Assert.Equal("Invalid parameter: a", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task CancellationTokenOfContextIsPassed(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("get", "cancel")]
                public IActionResult Get(CancellationToken cancellationToken) => new StatusCodeResult(cancellationToken.IsCancellationRequested ? 499 : 200);
            }
            """);
        var services = HandlerTestHost.CreateServices();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CompilationHelper.InvokeAsync(assembly, "Get_Handler", HandlerTestHost.CreateRequest(services), HandlerTestHost.CreateContext(services, cts.Token));

        Assert.Equal(499, HandlerTestHost.StatusOf(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task BodyIsReadAsynchronously(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("post", "body")]
                public IActionResult Post([AzureFunctionsExtension.Annotations.FromBody] Payload payload) => new OkObjectResult(payload.Name);
            }
            """);
        var services = HandlerTestHost.CreateServices();
        var request = HandlerTestHost.CreateRequest(services, body: """{"name":"abc"}""");

        var result = await CompilationHelper.InvokeAsync(assembly, "Post_Handler", request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal("abc", HandlerTestHost.ValueOf<string>(result));
    }
}
