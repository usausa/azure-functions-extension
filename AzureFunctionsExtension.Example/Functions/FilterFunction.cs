namespace AzureFunctionsExtension.Example.Functions;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;
using AzureFunctionsExtension.Example.Filters;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

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
        return Results.Ok(new { Message = "pong" });
    }
}
