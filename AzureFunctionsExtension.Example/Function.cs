namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

[AzureFunction]
[ServiceResolver(typeof(ServiceResolver))]
internal sealed partial class Function
{
    private static readonly Action<ILogger, int, int?, int, Exception?> QueryRequest =
        LoggerMessage.Define<int, int?, int>(LogLevel.Information, new EventId(1, nameof(Query)), "Query request. a=[{A}], b=[{B}], c=[{C}]");

    private static readonly Action<ILogger, int, int, Exception?> QueryArrayRequest =
        LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(2, nameof(Array)), "Query array request. a.Length=[{A}], b.Length=[{B}]");

    private static readonly Action<ILogger, int, string, bool, DateTime, Exception?> BodyRequestLog =
        LoggerMessage.Define<int, string, bool, DateTime>(LogLevel.Information, new EventId(3, nameof(Body)), "Body request. id=[{Id}], name=[{Name}], flag=[{Flag}], dateTime=[{DateTime:yyyy/MM/dd HH:mm:ss}]");

    private static readonly Action<ILogger, DateTime, Exception?> TimerTriggered =
        LoggerMessage.Define<DateTime>(LogLevel.Information, new EventId(4, nameof(Timer)), "Timer triggered at: {Time}");

    private readonly ILogger<Function> log;

    public Function(ILogger<Function> log)
    {
        this.log = log;
    }

    [HttpEndpoint("get", "query", AuthorizationLevel.Anonymous)]
    public IActionResult Query(
        [FromQuery] int a,
        [FromQuery] int? b,
        [FromQuery] int c = 3)
    {
        QueryRequest(log, a, b, c, null);
        return Results.Of(new QueryResponse { Result = a + (b ?? 0) + c });
    }

    [HttpEndpoint("get", "array", AuthorizationLevel.Anonymous)]
    public IActionResult Array(
        [FromQuery] int[] a,
        [FromQuery] int?[] b)
    {
        QueryArrayRequest(log, a.Length, b.Length, null);
        return Results.Of(new QueryResponse { Result = a.Sum() + b.Sum(static x => x ?? 0) });
    }

    [HttpEndpoint("post", "body", AuthorizationLevel.Function)]
    public IActionResult Body([FromBody] BodyRequest request)
    {
        BodyRequestLog(log, request.Id, request.Name, request.Flag, request.DateTime, null);
        return Results.Of(new BodyResponse
        {
            Id = request.Id,
            Name = request.Name,
            Flag = request.Flag,
            DateTime = DateTime.Now,
        });
    }

    [TimerEndpoint("0 */5 * * * *")]
    public void Timer([FromTrigger] TimerInfo timerInfo)
    {
        _ = timerInfo;
        TimerTriggered(log, DateTime.UtcNow, null);
    }
}
