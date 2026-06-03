namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

// Demonstrates [Filter<T>] filter pipeline applied to all handlers in the class.
[AzureFunction]
[Filter<LoggingFilter>(Order = 0)]
internal sealed partial class FilterFunction
{
    private static readonly Action<ILogger, Exception?> PingLog =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(Ping)), "Ping");

    private readonly ILogger<FilterFunction> log;

    public FilterFunction(ILogger<FilterFunction> log)
    {
        this.log = log;
    }

    [HttpEndpoint("get", "ping", AuthorizationLevel.Anonymous)]
    public IActionResult Ping()
    {
        PingLog(log, null);
        return Results.Of(new { Message = "pong" });
    }
}
