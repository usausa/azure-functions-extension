namespace AzureFunctionsExtension.Tests;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public sealed class CancellationTests
{
    private const string Head =
        """
        namespace Test;

        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.Azure.Functions.Worker;

        public static class State
        {
            public static string Of(CancellationToken token, FunctionContext context) =>
                token == context.CancellationToken ? "context" : token.IsCancellationRequested ? "canceled" : "active";

            public static async Task<string> WaitAsync(CancellationToken token, FunctionContext context)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                    return "completed";
                }
                catch (OperationCanceledException)
                {
                    return Of(token, context);
                }
            }
        }

        public sealed class Payload
        {
            public string? Name { get; set; }
        }

        public sealed class PassFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
        }

        public sealed class WaitFilter : IFunctionFilter
        {
            public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                context.Result = new OkObjectResult("filter-" + await State.WaitAsync(context.CancellationToken, context.FunctionContext));
            }
        }

        public sealed class TimeoutFilter : IFunctionFilter
        {
            public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                try
                {
                    await next(context);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    context.Result = new ObjectResult("timeout") { StatusCode = 503 };
                }
            }
        }

        """;

    private static string Source(string filter) =>
        Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("get", "token")]
                public IActionResult Token(CancellationToken token, FunctionContext context) => new OkObjectResult(State.Of(token, context));

                [HttpEndpoint("get", "wait")]
                public async Task<IActionResult> Wait(CancellationToken token, FunctionContext context) => new OkObjectResult(await State.WaitAsync(token, context));

                [HttpEndpoint("post", "body")]
                public IActionResult Body([AzureFunctionsExtension.Annotations.FromBody] Payload payload, CancellationToken token) => new OkObjectResult(payload.Name);
            }
            """;

    //--------------------------------------------------------------------------------
    // Handler and filter
    //--------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task HandlerReceivesContextTokenWithoutOptions(string filter)
    {
        var result = await InvokeAsync(filter, "Token_Handler", null);

        Assert.Equal("context", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task OptionsWithoutTimeoutDoNotCancelToken(string filter)
    {
        var result = await InvokeAsync(filter, "Token_Handler", new FunctionCancellationOptions());

        Assert.Equal("active", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task TimeoutCancelsHandlerToken(string filter)
    {
        var result = await InvokeAsync(filter, "Wait_Handler", new FunctionCancellationOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal("canceled", HandlerTestHost.ValueOf<string>(result));
    }

    [Fact]
    public async Task TimeoutCancelsFilterToken()
    {
        var result = await InvokeAsync("[Filter<WaitFilter>]", "Token_Handler", new FunctionCancellationOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal("filter-canceled", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("", 500)]
    [InlineData("[Filter<TimeoutFilter>]", 503)]
    public async Task TimeoutCancelsBodyRead(string filter, int status)
    {
        var assembly = CompilationHelper.LoadAssembly(Source(filter));
        var services = CreateServices(new FunctionCancellationOptions { Timeout = TimeSpan.FromMilliseconds(50) });
        var request = HandlerTestHost.CreateRequest(services);
        request.Body = new PendingReadStream();

        var result = await CompilationHelper.InvokeAsync(assembly, "Body_Handler", request, new FakeFunctionContext(services, "Test.Function.Body_Handler"));

        Assert.Equal(status, HandlerTestHost.StatusOf(result));
    }

    //--------------------------------------------------------------------------------
    // Generated code
    //--------------------------------------------------------------------------------

    [Fact]
    public void HandlerWithoutTokenAndFiltersDoesNotCreateToken()
    {
        var result = CompilationHelper.RunGenerator(
            """
            namespace Test;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class Function
            {
                [HttpEndpoint("get", "a")]
                public IActionResult Get() => new OkResult();
            }
            """);
        CompilationHelper.AssertNoGeneratorErrors(result);

        Assert.DoesNotContain("__cts__", result.GeneratedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("FunctionCancellationOptions", result.GeneratedCode, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]", "", "target.Run(__cts__?.Token ?? context.CancellationToken)")]
    [InlineData("[QueueEndpoint(\"queue\")]", "", "target.Run(__cts__?.Token ?? context.CancellationToken)")]
    [InlineData("[TimerEndpoint(\"0 */5 * * * *\")]", "[Filter<PassFilter>]", "CancellationToken = __cts__?.Token ?? context.CancellationToken,")]
    [InlineData("[QueueEndpoint(\"queue\")]", "[Filter<PassFilter>]", "CancellationToken = __cts__?.Token ?? context.CancellationToken,")]
    public void TimerAndQueueHandlersUseInvocationToken(string attribute, string filter, string expected)
    {
        var result = CompilationHelper.RunGenerator(
            $$"""
            namespace Test;

            using System.Threading;
            using System.Threading.Tasks;
            using AzureFunctionsExtension.Annotations;
            using AzureFunctionsExtension.Filters;

            public sealed class PassFilter : IFunctionFilter
            {
                public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
            }

            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                {{attribute}}
                public Task Run(CancellationToken token) => Task.CompletedTask;
            }
            """);
        CompilationHelper.AssertNoGeneratorErrors(result);

        Assert.Contains("using var __cts__ = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<global::AzureFunctionsExtension.FunctionCancellationOptions>(services)?.CreateTokenSource(context.CancellationToken);", result.GeneratedCode, StringComparison.Ordinal);
        Assert.Contains(expected, result.GeneratedCode, StringComparison.Ordinal);
    }

    //--------------------------------------------------------------------------------
    // FunctionCancellationOptions
    //--------------------------------------------------------------------------------

    [Fact]
    public void DefaultTimeoutDoesNotCancel()
    {
        var options = new FunctionCancellationOptions();

        using var source = options.CreateTokenSource(TestContext.Current.CancellationToken);

        Assert.Equal(Timeout.InfiniteTimeSpan, options.Timeout);
        Assert.False(source.Token.IsCancellationRequested);
    }

    [Fact]
    public void TokenSourceIsLinkedToToken()
    {
        using var parent = new CancellationTokenSource();
        using var source = new FunctionCancellationOptions().CreateTokenSource(parent.Token);

        parent.Cancel();

        Assert.True(source.Token.IsCancellationRequested);
    }

    [Fact]
    public void TimeoutCancelsTokenSource()
    {
        using var source = new FunctionCancellationOptions { Timeout = TimeSpan.FromMilliseconds(10) }.CreateTokenSource(TestContext.Current.CancellationToken);

        Assert.True(source.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)));
    }

    private static IServiceProvider CreateServices(FunctionCancellationOptions? options) =>
        HandlerTestHost.CreateServices(collection =>
        {
            if (options is not null)
            {
                collection.AddSingleton(options);
            }
        });

    private static Task<IActionResult> InvokeAsync(string filter, string handler, FunctionCancellationOptions? options)
    {
        var assembly = CompilationHelper.LoadAssembly(Source(filter));
        var services = CreateServices(options);
        return CompilationHelper.InvokeAsync(assembly, handler, HandlerTestHost.CreateRequest(services), HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));
    }
}
