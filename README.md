# Azure Functions Extension for .NET

[![NuGet Badge](https://buildstats.info/nuget/AzureFunctionsExtension)](https://www.nuget.org/packages/AzureFunctionsExtension/)

## What is this?

A Source Generator based extension library for the **Azure Functions .NET Isolated worker**.

It lets you write Functions as plain methods with ASP.NET Core style binding attributes
(`[FromQuery]`, `[FromBody]`, `[FromRoute]`, `[FromHeader]`, ...), and generates the
Azure Functions entry points for you at compile time. Dependency injection uses the
standard .NET / Azure Functions container; dependencies are resolved from
`FunctionContext.InstanceServices`.

## Supported triggers

Initial release supports the following triggers:

* `[HttpEndpoint]` &mdash; HTTP trigger (ASP.NET Core integration)
* `[TimerEndpoint]` &mdash; Timer trigger
* `[QueueEndpoint]` &mdash; Storage Queue trigger

The following are **not** supported yet:

* Service Bus trigger
* Event Grid trigger
* Other generic / custom triggers

## Getting started

Install the package:

```
dotnet add package AzureFunctionsExtension
```

### Program.cs

Configure the Isolated worker host and register the extension. Function classes and their
dependencies are registered with the standard DI container.

```csharp
using AzureFunctionsExtension;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddAzureFunctionExtension();

        // Application services
        services.AddSingleton<IGreetingService, GreetingService>();

        // Function classes
        services.AddTransient<SampleFunction>();
    })
    .Build();

host.Run();
```

### A function class

* Mark the class with `[AzureFunction]` and make it `partial` (the generator emits the entry points).
* Annotate each handler with `[HttpEndpoint]` / `[TimerEndpoint]` / `[QueueEndpoint]`.
* Constructor injection works as usual.

```csharp
namespace MyApp;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;

using Microsoft.Azure.Functions.Worker;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

[AzureFunction]
public sealed partial class SampleFunction
{
    private readonly IGreetingService greeting;

    public SampleFunction(IGreetingService greeting)
    {
        this.greeting = greeting;
    }

    [HttpEndpoint("get", "hello", AuthorizationLevel.Anonymous)]
    public IActionResult Hello([FromQuery] string name)
    {
        return Results.Ok(new { Message = greeting.Greet(name) });
    }
}
```

> The examples alias `IActionResult` instead of importing `Microsoft.AspNetCore.Mvc`,
> so the `[From*]` attributes resolve unambiguously to `AzureFunctionsExtension.Annotations`.

## Attributes

| Attribute | Target | Description |
| --- | --- | --- |
| `[AzureFunction]` | class | Marks a `partial` class. The generator emits the Azure Functions entry points. |
| `[HttpEndpoint(method, route, authLevel?)]` | method | HTTP trigger. `authLevel` defaults to `Function`. |
| `[TimerEndpoint(schedule)]` | method | Timer trigger (NCRONTAB expression). |
| `[QueueEndpoint(queueName, connection?)]` | method | Storage Queue trigger. |
| `[FromQuery(name?)]` | parameter | Bind from the query string. |
| `[FromRoute(name?)]` | parameter | Bind from a route value. |
| `[FromHeader(name?)]` | parameter | Bind from an HTTP header. |
| `[FromBody(SkipValidate?)]` | parameter | Deserialize the JSON body. Validated with DataAnnotations unless `SkipValidate = true`. |
| `[FromTrigger]` | parameter | Bind the raw trigger payload (queue message, `TimerInfo`, ...). |
| `[FromServices(key?)]` | parameter | Resolve the parameter from DI (`FunctionContext.InstanceServices`). With a key, resolves a keyed service. |
| `[Filter<TFilter>(Order?)]` | class | Apply an `IFunctionFilter` around every handler in the class. |

## Diagnostics

The generator reports the following diagnostics.

| ID | Severity | Phase | Description |
|:---|:------:|:--------|:-----|
| `AFE0001` | Error | Class definition | `[AzureFunction]` class is not `partial` |
| `AFE0002` | Error | Class definition | `[AzureFunction]` class is generic |
| `AFE0003` | Error | Class definition | `[AzureFunction]` class is a nested type |
| `AFE0004` | Error | Class definition | `[AzureFunction]` applied to a record &mdash; not supported |
| `AFE0005` | Error | Class definition | `[AzureFunction]` class is `abstract` |
| `AFE0006` | Error | Filter | Filter type does not implement `IFunctionFilter` |
| `AFE0007` | Error | Function | Function has multiple endpoint attributes |
| `AFE0008` | Error | Function | Overloaded function name (function names must be unique) |
| `AFE0009` | Error | Function | HTTP-only binding attribute used on a non-HTTP function |
| `AFE0010` | Error | Function | Timer/Queue function has multiple trigger payload parameters |
| `AFE0011` | Error | Parameter | Parameter has multiple binding attributes |
| `AFE0012` | Error | Parameter | Parameter type is not supported by binding |
| `AFE0013` | Warning | Route | Route template variable is not bound with `[FromRoute]` |

## HTTP binding

### Query

Scalar, `Nullable<T>`, default values, and arrays (comma separated) are supported.
Built-in types, enums, and `Nullable` of those are converted automatically.

```csharp
[HttpEndpoint("get", "query", AuthorizationLevel.Anonymous)]
public IActionResult Query(
    [FromQuery] int a,
    [FromQuery] int? b,
    [FromQuery] int c = 3)
{
    return Results.Ok(new { Result = a + (b ?? 0) + c });
}

[HttpEndpoint("get", "array", AuthorizationLevel.Anonymous)]
public IActionResult Array(
    [FromQuery] int[] a,
    [FromQuery] int?[] b)
{
    return Results.Ok(new { Result = a.Sum() + b.Sum(static x => x ?? 0) });
}
```

When a value cannot be converted, a `400 Bad Request` is returned automatically.

### Route

```csharp
[HttpEndpoint("get", "items/{id}", AuthorizationLevel.Anonymous)]
public IActionResult GetItem([FromRoute] int id)
{
    if (id <= 0)
    {
        return Results.NotFound($"Item {id} not found.");
    }

    return Results.Ok(new { Id = id, Name = $"Item-{id}" });
}
```

### Header

```csharp
[HttpEndpoint("get", "header-echo", AuthorizationLevel.Anonymous)]
public IActionResult HeaderEcho([FromHeader("X-Correlation-Id")] string? correlationId)
{
    return Results.Ok(new { CorrelationId = correlationId ?? "(none)" });
}
```

### Body

The JSON body is deserialized with `System.Text.Json`. A missing or invalid body returns
`400 Bad Request`. DataAnnotations validation runs by default; set
`[FromBody(SkipValidate = true)]` to skip it.

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class BodyRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
}

[HttpEndpoint("post", "body")]
public IActionResult Body([FromBody] BodyRequest request)
{
    return Results.Ok(request);
}
```

## Return values

A handler may return:

* an `IActionResult` &mdash; returned as-is,
* any other object &mdash; serialized as JSON with a `200 OK`,
* `void` / `Task` &mdash; returns `200 OK` with no body.

The `Results` helper creates common results:

```csharp
Results.Ok();
Results.Ok(value);
Results.Created(uri, value);
Results.NoContent();
Results.BadRequest(value);
Results.NotFound(value);
Results.Conflict(value);
Results.StatusCode(429, value);
```

Throw `ApiException` to short-circuit with a specific status code:

```csharp
throw new ApiException(404, $"Item '{name}' not found.");
```

## Dependency injection

DI uses the standard Azure Functions / .NET container &mdash; there is no custom resolver attribute.

* Inject services into the function class through its constructor.
* Inject services into a handler parameter with `[FromServices]`.

Dependencies are resolved from `FunctionContext.InstanceServices`.
`[FromServices("key")]` resolves a keyed service (`GetRequiredKeyedService`); without a key it resolves the default service (`GetRequiredService`).

```csharp
[HttpEndpoint("post", "greet", AuthorizationLevel.Anonymous)]
public async Task<IActionResult> Greet(
    [FromBody(SkipValidate = true)] GreetRequest request,
    [FromServices] IGreetingService greeting)
{
    await Task.Yield();
    return Results.Ok(new { Message = greeting.Greet(request.Name) });
}
```

## Filters

Implement `IFunctionFilter` to run logic around handlers (a middleware-style pipeline).
Set `context.Result` to short-circuit the handler.

```csharp
using AzureFunctionsExtension.Filters;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

internal sealed class LoggingFilter : IFunctionFilter
{
    public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        await next(context);
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        context.FunctionContext.GetLogger(nameof(LoggingFilter))
            .LogInformation("Completed in {ElapsedMs}ms", elapsed.TotalMilliseconds);
    }
}
```

Apply one or more filters to a function class with `[Filter<T>]`. Filters run in ascending
`Order` (outermost first). Register the filter type with DI, or it is created via
`ActivatorUtilities`.

```csharp
[AzureFunction]
[Filter<LoggingFilter>(Order = 0)]
public sealed partial class FilterFunction
{
    [HttpEndpoint("get", "ping", AuthorizationLevel.Anonymous)]
    public IActionResult Ping()
    {
        return Results.Ok(new { Message = "pong" });
    }
}
```

## Timer trigger

```csharp
[TimerEndpoint("0 */5 * * * *")]
public void Timer([FromTrigger] TimerInfo timerInfo)
{
    // ...
}
```

## Queue trigger

```csharp
[AzureFunction]
public sealed partial class QueueFunction
{
    [QueueEndpoint("my-queue")]
    public void ProcessMessage(
        [FromTrigger] string message,
        [FromServices] IGreetingService greeting)
    {
        // ...
    }
}
```

## JSON serialization

`System.Text.Json` is used throughout. Both input and output share the same `JsonOptions`
configured on `AddAzureFunctionExtension`, which is based on `JsonSerializerDefaults.Web`
(camelCase property names on write and case-insensitive property matching on read).

* **Request body** (`[FromBody]`) is deserialized with the registered `IBodySerializer`.
  The default serializer is built from the configured `JsonOptions`.
* **Response** (`Results.Ok(value)` etc.) is serialized with the same `JsonOptions`.

### Configuration

```csharp
services.AddAzureFunctionExtension(c =>
{
    c.Options.PropertyNameCaseInsensitive = true;
    c.Options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
```

### Native AOT / trimming

The library is annotated for trimming and AOT. To serialize without reflection, register an
`IBodySerializer` backed by a `JsonSerializerContext`. Register it **before**
`AddAzureFunctionExtension` (the default serializer is only added when one is not already present).

```csharp
using System.Text.Json.Serialization;

using AzureFunctionsExtension.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BodyRequest))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

```csharp
services.AddSingleton<IBodySerializer>(new JsonBodySerializer(AppJsonContext.Default));
services.AddAzureFunctionExtension();
```
