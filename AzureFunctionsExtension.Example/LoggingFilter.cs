namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension.Filters;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

internal sealed class LoggingFilter : IFunctionFilter
{
    private static readonly Action<ILogger, double, Exception?> HandlerCompleted =
        LoggerMessage.Define<double>(LogLevel.Information, new EventId(1, "HandlerCompleted"), "Handler completed in {ElapsedMs}ms");

    public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        await next(context).ConfigureAwait(false);
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        HandlerCompleted(context.FunctionContext.GetLogger(nameof(LoggingFilter)), elapsed.TotalMilliseconds, null);
    }
}
