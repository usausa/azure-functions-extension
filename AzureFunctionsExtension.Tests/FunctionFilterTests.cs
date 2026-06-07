#pragma warning disable CA1707
namespace AzureFunctionsExtension.Tests;

using AzureFunctionsExtension.Filters;

using Xunit;

public sealed class FunctionFilterTests
{
    [Fact]
    public async Task Pipeline_SingleFilter_RunsAroundTerminal()
    {
        List<string> log = [];
        ValueTask Terminal(FunctionInvocationContext c)
        {
            log.Add("terminal");
            c.Result = "result";
            return ValueTask.CompletedTask;
        }

        var pipeline = Compose(Terminal, new RecordingFilter(log, "f1"));
        var ctx = new FunctionInvocationContext();

        await pipeline(ctx);

        string[] expected = ["f1:before", "terminal", "f1:after"];
        Assert.Equal(expected, log);
        Assert.Equal("result", ctx.Result);
    }

    [Fact]
    public async Task Pipeline_MultipleFilters_ExecuteInOnionOrder()
    {
        List<string> log = [];
        ValueTask Terminal(FunctionInvocationContext c)
        {
            log.Add("terminal");
            return ValueTask.CompletedTask;
        }

        var pipeline = Compose(Terminal, new RecordingFilter(log, "f1"), new RecordingFilter(log, "f2"));
        var ctx = new FunctionInvocationContext();

        await pipeline(ctx);

        string[] expected = ["f1:before", "f2:before", "terminal", "f2:after", "f1:after"];
        Assert.Equal(expected, log);
    }

    [Fact]
    public async Task Pipeline_FilterShortCircuits_TerminalNotInvoked()
    {
        var terminalInvoked = false;
        ValueTask Terminal(FunctionInvocationContext c)
        {
            terminalInvoked = true;
            return ValueTask.CompletedTask;
        }

        var pipeline = Compose(Terminal, new ShortCircuitFilter("blocked"));
        var ctx = new FunctionInvocationContext();

        await pipeline(ctx);

        Assert.False(terminalInvoked);
        Assert.Equal("blocked", ctx.Result);
    }

    [Fact]
    public async Task Pipeline_OuterFilterObservesResultSetByTerminal()
    {
        List<string> log = [];
        static ValueTask Terminal(FunctionInvocationContext c)
        {
            c.Result = "42";
            return ValueTask.CompletedTask;
        }

        var pipeline = Compose(Terminal, new ResultCapturingFilter(log));
        var ctx = new FunctionInvocationContext();

        await pipeline(ctx);

        Assert.Contains("result=42", log);
    }

    [Fact]
    public async Task Pipeline_SharesStateViaItems()
    {
        List<string> log = [];
        ValueTask Terminal(FunctionInvocationContext c)
        {
            log.Add($"items={c.Items["trace"]}");
            return ValueTask.CompletedTask;
        }

        var pipeline = Compose(Terminal, new ItemsWritingFilter());
        var ctx = new FunctionInvocationContext();

        await pipeline(ctx);

        Assert.Contains("items=abc", log);
    }

    [Fact]
    public async Task Pipeline_TerminalThrows_PropagatesAndSkipsAfter()
    {
        List<string> log = [];
        static ValueTask Terminal(FunctionInvocationContext c) => throw new InvalidOperationException("boom");

        var pipeline = Compose(Terminal, new RecordingFilter(log, "f1"));
        var ctx = new FunctionInvocationContext();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline(ctx));

        string[] expected = ["f1:before"];
        Assert.Equal(expected, log);
    }

    // Composes filters around a terminal delegate in reverse (onion) order,
    // mirroring the pipeline the source generator emits.
    private static FunctionFilterDelegate Compose(FunctionFilterDelegate terminal, params IFunctionFilter[] filters)
    {
        var pipeline = terminal;
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = ctx => filter.InvokeAsync(ctx, next);
        }

        return pipeline;
    }

    private sealed class RecordingFilter : IFunctionFilter
    {
        private readonly List<string> log;
        private readonly string name;

        public RecordingFilter(List<string> log, string name)
        {
            this.log = log;
            this.name = name;
        }

        public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
        {
            log.Add($"{name}:before");
            await next(context);
            log.Add($"{name}:after");
        }
    }

    private sealed class ResultCapturingFilter : IFunctionFilter
    {
        private readonly List<string> log;

        public ResultCapturingFilter(List<string> log)
        {
            this.log = log;
        }

        public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
        {
            await next(context);
            log.Add($"result={context.Result}");
        }
    }

    private sealed class ItemsWritingFilter : IFunctionFilter
    {
        public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
        {
            context.Items["trace"] = "abc";
            return next(context);
        }
    }

    private sealed class ShortCircuitFilter : IFunctionFilter
    {
        private readonly object result;

        public ShortCircuitFilter(object result)
        {
            this.result = result;
        }

        public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
        {
            context.Result = result;
            return ValueTask.CompletedTask;
        }
    }
}
