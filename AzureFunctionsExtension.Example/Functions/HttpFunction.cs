namespace AzureFunctionsExtension.Example.Functions;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;
using AzureFunctionsExtension.Example.Models;
using AzureFunctionsExtension.Example.Services;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

[AzureFunction]
internal sealed partial class HttpFunction
{
    private static readonly Action<ILogger, int, int?, int, Exception?> QueryRequest =
        LoggerMessage.Define<int, int?, int>(LogLevel.Information, new EventId(1, nameof(Query)), "Query request. a=[{A}], b=[{B}], c=[{C}]");

    private static readonly Action<ILogger, int, int, Exception?> QueryArrayRequest =
        LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(2, nameof(Array)), "Query array request. a.Length=[{A}], b.Length=[{B}]");

    private static readonly Action<ILogger, int, string, bool, DateTime, Exception?> BodyRequestLog =
        LoggerMessage.Define<int, string, bool, DateTime>(LogLevel.Information, new EventId(3, nameof(Body)), "Body request. id=[{Id}], name=[{Name}], flag=[{Flag}], dateTime=[{DateTime:yyyy/MM/dd HH:mm:ss}]");

    private static readonly Action<ILogger, DateTime, Exception?> TimerTriggered =
        LoggerMessage.Define<DateTime>(LogLevel.Information, new EventId(4, nameof(Timer)), "Timer triggered at: {Time}");

    private static readonly Action<ILogger, int, Exception?> RouteRequest =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(5, nameof(GetItem)), "Route request. id=[{Id}]");

    private static readonly Action<ILogger, string?, Exception?> HeaderRequest =
        LoggerMessage.Define<string?>(LogLevel.Information, new EventId(6, nameof(HeaderEcho)), "Header request. correlationId=[{CorrelationId}]");

    private static readonly Action<ILogger, string, Exception?> LookupLog =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, nameof(Lookup)), "Lookup request. name=[{Name}]");

    private static readonly Action<ILogger, string, Exception?> GreetLog =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(8, nameof(Greet)), "Greet request. name=[{Name}]");

    private readonly ILogger<HttpFunction> log;

    public HttpFunction(ILogger<HttpFunction> log)
    {
        this.log = log;
    }

    // [FromQuery] — scalar, nullable, default value
    [HttpEndpoint("get", "query", AuthorizationLevel.Anonymous)]
    public IActionResult Query(
        [FromQuery] int a,
        [FromQuery] int? b,
        [FromQuery] int c = 3)
    {
        QueryRequest(log, a, b, c, null);
        return Results.Ok(new QueryResponse { Result = a + (b ?? 0) + c });
    }

    // [FromQuery] — arrays
    [HttpEndpoint("get", "array", AuthorizationLevel.Anonymous)]
    public IActionResult Array(
        [FromQuery] int[] a,
        [FromQuery] int?[] b)
    {
        QueryArrayRequest(log, a.Length, b.Length, null);
        return Results.Ok(new QueryResponse { Result = a.Sum() + b.Sum(static x => x ?? 0) });
    }

    // [FromBody] — with DataAnnotations validation
    [HttpEndpoint("post", "body")]
    public IActionResult Body([FromBody] BodyRequest request)
    {
        BodyRequestLog(log, request.Id, request.Name, request.Flag, request.DateTime, null);
        return Results.Ok(new BodyResponse
        {
            Id = request.Id,
            Name = request.Name,
            Flag = request.Flag,
            DateTime = DateTime.Now
        });
    }

    // [FromRoute] — path parameter binding; demonstrates Results.NotFound() / Results.Ok()
    [HttpEndpoint("get", "items/{id}", AuthorizationLevel.Anonymous)]
    public IActionResult GetItem([FromRoute] int id)
    {
        RouteRequest(log, id, null);
        if (id <= 0)
        {
            return Results.NotFound($"Item {id} not found.");
        }
        return Results.Ok(new ItemResponse { Id = id, Name = $"Item-{id}" });
    }

    // [FromHeader] — custom HTTP header binding
    [HttpEndpoint("get", "header-echo", AuthorizationLevel.Anonymous)]
    public IActionResult HeaderEcho([FromHeader("X-Correlation-Id")] string? correlationId)
    {
        HeaderRequest(log, correlationId, null);
        return Results.Ok(new { CorrelationId = correlationId ?? "(none)" });
    }

    // ApiException — returns 404 when item is not found
    [HttpEndpoint("get", "items/{name}/lookup", AuthorizationLevel.Anonymous)]
    public IActionResult Lookup([FromRoute] string name)
    {
        LookupLog(log, name, null);
        if (String.IsNullOrWhiteSpace(name) || (name == "unknown"))
        {
            throw new ApiException(404, $"Item '{name}' not found.");
        }
        return Results.Ok(new ItemResponse { Id = 0, Name = name });
    }

    // async Task<IActionResult> + [FromServices] + [FromBody(SkipValidate = true)]
    [HttpEndpoint("post", "greet", AuthorizationLevel.Anonymous)]
    public async Task<IActionResult> Greet(
        [FromBody(SkipValidate = true)] GreetRequest request,
        [FromServices] IGreetingService greeting)
    {
        GreetLog(log, request.Name, null);
        await Task.Yield();
        return Results.Ok(new GreetResponse { Message = greeting.Greet(request.Name) });
    }

    // [TimerEndpoint] + [FromTrigger]
    [TimerEndpoint("0 */5 * * * *")]
    public void Timer([FromTrigger] TimerInfo timerInfo)
    {
        _ = timerInfo;
        TimerTriggered(log, DateTime.UtcNow, null);
    }
}
