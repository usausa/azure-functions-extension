# Azure Functions Extension for .NET

[![NuGet Badge](https://buildstats.info/nuget/AzureFunctionsExtension)](https://www.nuget.org/packages/AzureFunctionsExtension/)

## What is this?

A Source Generator based extension library for the **Azure Functions .NET Isolated worker**.

It lets you write Functions as plain methods with ASP.NET Core style binding attributes
(`[FromQuery]`, `[FromBody]`, `[FromRoute]`, `[FromHeader]`, `[FromCookie]`, ...), and generates the
Azure Functions entry points for you at compile time. Dependency injection uses the
standard .NET / Azure Functions container; dependencies are resolved from
`FunctionContext.InstanceServices`.

## Supported triggers

The following triggers are supported:

* `[HttpEndpoint]` &mdash; HTTP trigger (ASP.NET Core integration)
* `[TimerEndpoint]` &mdash; Timer trigger
* `[QueueEndpoint]` &mdash; Storage Queue trigger

The following are **not** supported yet:

* Service Bus trigger
* Event Grid trigger
* Other generic / custom triggers

## Getting started

Requirements:

* The project uses the `Azure.Functions.Sdk` MSBuild SDK (1.0.1 or later), with the version pinned in `global.json`.
* The project targets .NET 10 (`net10.0`).
* HTTP functions use the ASP.NET Core integration (`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`).
* `AddAzureFunctionExtension()` is required, and it must be called after `ConfigureFunctionsWebApplication()`.

Install the package:

```
dotnet add package AzureFunctionsExtension
```

### Project file

```xml
<Project Sdk="Azure.Functions.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AzureFunctionsExtension" Version="x.x.x" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.1" />
  </ItemGroup>

</Project>
```

The SDK makes the project an executable for Functions v4 and copies `host.json` and `local.settings.json` to the output,
so `OutputType`, `AzureFunctionsVersion`, and the copy settings are not needed.

Pin the SDK version in `global.json`:

```json
{
  "msbuild-sdks": {
    "Azure.Functions.Sdk": "1.0.1"
  }
}
```

> Pin the version in `global.json`, not in the project file (`Sdk="Azure.Functions.Sdk/1.0.1"`).
> The project that the SDK generates during restore (`obj/azure_functions/azure_functions.g.csproj`) refers to the SDK
> without a version, so a parallel build fails with `MSB4236` unless the version is pinned in `global.json`.

### Program.cs

Configure the Isolated worker host and register the extension. Register the services that the
functions use with the standard DI container. Function classes need no registration (see [Dependency injection](#dependency-injection)).

```csharp
using AzureFunctionsExtension;

using Microsoft.Azure.Functions.Worker.Builder;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddAzureFunctionExtension();

// Application services
builder.Services.AddSingleton<IGreetingService, GreetingService>();

builder.Build().Run();
```

* The generator emits a catalog of the functions. `AddAzureFunctionExtension()` adds them to the function metadata
  that the worker reports to the Functions host (worker indexing) and runs them, so they need no other registration.
  Functions written by hand with `[Function]` can be in the same app, with names that differ from the generated ones (`AFE0306`).
* `AddAzureFunctionExtension()` registers the executor that runs the generated functions. It must be registered after
  the one of the Worker SDK, so call it after `ConfigureFunctionsWebApplication()`. `HostBuilder`
  (`ConfigureFunctionsWebApplication()` and then `ConfigureServices(...)`) works in the same order.
* A wrong setup stops the worker at startup: without the call, with
  `AzureFunctionsExtension is not registered. Call AddAzureFunctionExtension().`, and with the call before
  `ConfigureFunctionsWebApplication()`, with
  `The function executor of AzureFunctionsExtension is not used. Call AddAzureFunctionExtension() after ConfigureFunctionsWebApplication().`
* Difference from AmazonLambdaExtension: Lambda calls the handler named in the function configuration, so nothing is
  registered in code there. The Functions host finds functions through the worker, so here the generated functions
  are added to the metadata of the worker.

### Migrating from Microsoft.Azure.Functions.Worker.Sdk

Earlier versions used the `Microsoft.Azure.Functions.Worker.Sdk` package. To move to `Azure.Functions.Sdk`:

1. Change `<Project Sdk="Microsoft.NET.Sdk">` to `<Project Sdk="Azure.Functions.Sdk">`, and remove the
   `Microsoft.Azure.Functions.Worker.Sdk` package reference (using both is `AZFW0105`).
   `OutputType`, `AzureFunctionsVersion`, and the copy settings of `host.json` / `local.settings.json` can be removed.
2. Add `global.json` with the SDK version.
3. Remove `FunctionsEnableWorkerIndexing` and `FunctionsEnableExecutorSourceGen` if the project sets them
   (the package no longer sets them, and the SDK always uses worker indexing).
   Do not disable `FunctionsEnableMetadataSourceGen` or `FunctionsAutoRegisterGeneratedMetadataProvider` (`AFE0602`).
4. Call `AddAzureFunctionExtension()` after `ConfigureFunctionsWebApplication()`.
5. When the Functions project is built as part of a solution, restore the project by itself first
   (`dotnet restore MyApp/MyApp.csproj`, then `dotnet build MySolution.slnx`); otherwise the build reports `AZFW0108`.
   `dotnet clean` and `dotnet build --no-incremental` remove the restored host extensions (`obj/azure_functions`),
   so restore the project again after them.

Without `Azure.Functions.Sdk`, the build fails with `AFE0601`.

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
| `[FromCookie(name)]` | parameter | Bind from a cookie (see [Claims and cookies](#claims-and-cookies)). |
| `[FromBody(SkipValidate?)]` | parameter | Deserialize the JSON body. Validated with DataAnnotations unless `SkipValidate = true`. |
| `[FromTrigger]` | parameter | Bind the raw trigger payload (queue message, `TimerInfo`, ...). |
| `[FromServices(key?)]` | parameter | Resolve the parameter from DI (`FunctionContext.InstanceServices`). With a key, resolves a keyed service. |
| `[Filter<TFilter>(Order?)]` | class, assembly | Apply an `IFunctionFilter` around every handler in the class (on the assembly, in every `[AzureFunction]` class). |

Parameters of the following types need no attribute: `HttpRequest`, `FunctionContext`, `CancellationToken`
(see [Cancellation](#cancellation)), `ILogger<T>`, and `ClaimsPrincipal` (see [Claims and cookies](#claims-and-cookies)).
`HttpRequest` and `ClaimsPrincipal` can be used only in HTTP functions (`AFE0403`).

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

When a value cannot be converted, a `400 Bad Request` is returned automatically (see [Binding errors](#binding-errors)).

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

Arrays are bound by splitting the route value on commas, like the query string
(`/route-array/1,2,3` binds `[1, 2, 3]`).

```csharp
[HttpEndpoint("get", "route-array/{ids}", AuthorizationLevel.Anonymous)]
public IActionResult RouteArray([FromRoute] int[] ids)
{
    return Results.Ok(ids);
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

Arrays are bound by splitting the header value on commas, like the query string.
Multiple header lines are joined with commas first, so `X-Ids: 1,2,3` and the two lines
`X-Ids: 1` and `X-Ids: 2,3` bind the same array.

```csharp
[HttpEndpoint("get", "header-array", AuthorizationLevel.Anonymous)]
public IActionResult HeaderArray([FromHeader("X-Ids")] int[] ids)
{
    return Results.Ok(ids);
}
```

The elements are not trimmed, so a `string[]` keeps the space in `X-Ids: a, b`.
A header whose value itself contains commas (such as an HTTP date) cannot be bound as an array.

### Body

The JSON body is deserialized with `System.Text.Json`. A missing or invalid body returns
`400 Bad Request`. DataAnnotations validation runs by default (see [Validation](#validation)); set
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

The body is read asynchronously from the request stream (`IBodySerializer.DeserializeAsync`), because the ASP.NET Core
integration does not allow synchronous reads. Difference from AmazonLambdaExtension: the body of an HTTP API request
is a string in the Lambda event, so the serializer there reads strings.

### Validation

A `[FromBody]` parameter is validated by the registered `IRequestValidator` unless `SkipValidate = true`. The rules are
the DataAnnotations attributes (`[Required]`, `[Range]`, `[StringLength]`, ...) and `IValidatableObject`.
`AddAzureFunctionExtension()` registers `DataAnnotationsRequestValidator` when no validator is registered.

| Implementation | Use |
| --- | --- |
| `ValidatableTypeRequestValidator` | Uses the validation information that the generator of `Microsoft.Extensions.Validation` (the validation of .NET 10) creates at build time. Nested objects and the elements of collection properties are validated as well. |
| `DataAnnotationsRequestValidator` | Uses `Validator.TryValidateObject`. The default. |

```csharp
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Validation;

[ValidatableType]
public sealed class BodyRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
```

```csharp
builder.Services.AddAzureFunctionExtension();
builder.Services.AddValidation();
builder.Services.AddSingleton<IRequestValidator, ValidatableTypeRequestValidator>();
```

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);ASP0029</NoWarn>
</PropertyGroup>
```

* `Microsoft.Extensions.Validation` and its generator are part of the ASP.NET Core shared framework, which the HTTP
  extension of the worker references. The generator intercepts the call of `AddValidation()` and adds the types marked
  with `[ValidatableType]` in the same project, together with the types of their properties. Call `AddValidation()` in
  the project that declares the body types. A test project that builds the services by itself calls it through a
  method of that project (`AddRequestValidation()` of the Example).
* The registration replaces the default one, before or after `AddAzureFunctionExtension()`.
* `[ValidatableType]` is experimental in .NET 10 and reports `ASP0029` as an error. Suppress it in the project, as above.
* Mark each body type with `[ValidatableType]` and make it `public`. Validating a type that the generator did not add
  (not marked, not `public`, declared in another project, or without any rule) throws `InvalidOperationException`
  (`Validatable type is not found. type=[...]`), so a missing mark is found at the first request. Set
  `SkipValidate = true` for a body without rules, a `string` body, and a collection body.
* The results differ from `DataAnnotationsRequestValidator` in the following points. The other results (the messages,
  the order, `[Display(Name)]`, `IValidatableObject`, and the properties of the base types) are the same.
  * Nested objects and the elements of collection properties are validated. The member name of a result is the path
    (`Child.Value`, `Items[1].Value`).
  * The attributes on the parameters of the primary constructor of a record are used. `DataAnnotationsRequestValidator`
    uses only the attributes on the properties (`[property: Required]`).
  * Properties with `[JsonIgnore]` are not validated.
  * For an attribute on the type that does not return its own `ValidationResult`, the default message uses the display
    name of the last validated property, and the result has its member name.
* Difference from AmazonLambdaExtension: the package there references `Microsoft.Extensions.Validation`, and the
  validator is registered in `ConfigureServices()` of the `ServiceResolver`.

### Claims and cookies

Receive the caller as a `ClaimsPrincipal` parameter (no attribute), as in ASP.NET Core minimal APIs.

```csharp
[HttpEndpoint("get", "me", AuthorizationLevel.Anonymous)]
public IActionResult Me(ClaimsPrincipal user, [FromCookie("theme")] string? theme)
{
    return user.Identity?.IsAuthenticated == true
        ? Results.Ok(new { Id = user.FindFirst("sub")?.Value, Admin = user.IsInRole("admin"), Theme = theme })
        : Results.StatusCode(401);
}
```

* The `ClaimsPrincipal` is `HttpContext.User`, which the app sets. Without it, the `ClaimsPrincipal` is empty and unauthenticated.
* The app can set `HttpContext.User` in a worker middleware, where `context.GetHttpContext()` returns the `HttpContext`
  of an HTTP function. For example, App Service authentication passes the claims of the caller in the
  `X-MS-CLIENT-PRINCIPAL` header (see [Decode the client principal header](https://learn.microsoft.com/azure/app-service/configure-authentication-user-identities#decode-the-client-principal-header)):

```csharp
internal sealed class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if ((httpContext is not null) && httpContext.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var header))
        {
            httpContext.User = Parse(header.ToString());
        }

        return next(context);
    }

    public static ClaimsPrincipal Parse(string header)
    {
        using var document = JsonDocument.Parse(Convert.FromBase64String(header));
        var root = document.RootElement;
        var identity = new ClaimsIdentity(GetString(root, "auth_typ"), GetString(root, "name_typ"), GetString(root, "role_typ"));
        if (root.TryGetProperty("claims", out var claims))
        {
            foreach (var claim in claims.EnumerateArray())
            {
                identity.AddClaim(new Claim(GetString(claim, "typ")!, GetString(claim, "val")!));
            }
        }

        return new ClaimsPrincipal(identity);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
```

```csharp
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<ClientPrincipalMiddleware>();
```

* App Service sets `X-MS-CLIENT-PRINCIPAL`, and external requests cannot set it, only when App Service authentication
  is enabled. Elsewhere (for example, locally with Core Tools), any client can send the header, so use such a middleware
  only behind App Service authentication.
* If the app creates the user with ASP.NET Core's JwtBearer, `MapInboundClaims = false` and the following claim types give
  the same claim names, `Identity.Name`, and `IsInRole` as AmazonLambdaExtension (which uses the `name` and `role` claims by default):

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "role";
});
```

* Filters read and replace the caller with `FunctionInvocationContext.User`. In an HTTP function it reads and writes
  `HttpContext.User`, so the function receives the replaced one. In Timer and Queue functions it is kept in the context
  (empty and unauthenticated at first).
* `ClaimsPrincipal` parameters can be used only in HTTP functions (`AFE0403`).
* `[FromCookie("name")]` reads `HttpRequest.Cookies`, so the rules are those of ASP.NET Core: names are case-insensitive,
  values are URL-decoded (`+` is kept), the last one wins for the same name, an empty value is ignored (an earlier value stays),
  an entry with a character that is not allowed (whitespace, `"`, `,`, `;`, `\`, or non-ASCII) or with a space before `=`
  is ignored, and a quoted value keeps its quotes.
* `[FromCookie]` converts the value like `[FromQuery]`; a missing value binds the default value (or `null`), and arrays
  are not supported (`AFE0406`). It can be used only in HTTP functions (`AFE0403`).
* Difference from AmazonLambdaExtension: the user there is created from the claims of the HTTP API JWT authorizer,
  which API Gateway passes as strings. So an array element containing a space is split there (`["a b","c"]` arrives as
  `[a b c]`), an object claim is a `map[...]` string, and the `ValueType` of every claim is `string`. With JwtBearer here,
  `["a b","c"]` gives `a b` and `c`, an object claim is JSON, and numbers and booleans keep their `ValueType`.

### Binding errors

By default, a binding or validation failure returns a `400` with a text message.
To return your own response (for example, RFC 9457 Problem Details), register an `IBindingErrorResultFactory`
as a singleton (other lifetimes are not supported).

```csharp
internal sealed class BindingErrorResultFactory : IBindingErrorResultFactory
{
    public IActionResult Create(BindingError bindingError)
    {
        var problem = new ValidationProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Errors =
            {
                [bindingError.Name] = bindingError.ValidationResults.Count > 0
                    ? [.. bindingError.ValidationResults.Select(static x => x.ErrorMessage ?? string.Empty)]
                    : [bindingError.Kind.ToString()]
            }
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
```

```csharp
builder.Services.AddSingleton<IBindingErrorResultFactory, BindingErrorResultFactory>();
```

| `BindingError` | Description |
| --- | --- |
| `Kind` | `InvalidValue` (conversion failed), `InvalidBody` (invalid JSON), `MissingBody` (non-nullable body is null), `ValidationFailed` (`IRequestValidator` failed) |
| `Source` | `Route` / `Query` / `Header` / `Body` / `Cookie` |
| `Name` | Binding key (route / query / header / cookie name), or the body parameter name |
| `ValidationResults` | Results of `IRequestValidator` (only for `ValidationFailed`) |

* The factory is used by HTTP functions. Without the registration, the response is the same text `400` as before.
* `ObjectResult` does not set the `application/problem+json` content type by itself, so set `ContentTypes` as above.
* Exceptions thrown by handlers are not passed to the factory. Handle them in a filter (for example, an outermost error filter).
* With filters, parameters are bound inside the pipeline, so a failure response (`context.Result`) also passes through
  the outer filters. For example, an outermost error filter can add a request ID to it or write a log.
* `BindingError`, `BindingErrorKind`, and `BindingSource` have the same names as in AmazonLambdaExtension, which also has
  `BindingSource.Authorizer`, so the numeric value of `Cookie` differs (4 here, 5 there). Compare them by name,
  or write them as strings (for example, with `JsonStringEnumConverter`).

## Return values

A handler may return:

* an `IActionResult` &mdash; returned as-is
* any other object &mdash; returned as `Results.Ok(value)`, which writes it as JSON with a `200 OK`
  (see [JSON serialization](#json-serialization) for `string` and `null`)
* `void` / `Task` &mdash; returns `200 OK` with no body

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
* The function class needs no registration. As the default function activator of the Functions worker does, the generated
  code creates the class for each invocation with `ActivatorUtilities.CreateInstance`, and a registration of the class is not used.

Dependencies are resolved from `FunctionContext.InstanceServices`.
`[FromServices("key")]` resolves a keyed service (`GetRequiredKeyedService`); without a key it resolves the default service (`GetRequiredService`).

Difference from AmazonLambdaExtension: the class there is created once for the execution environment, so its constructor
dependencies are effectively singletons. Here they come from the scope of each invocation.

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

### Assembly-level filters

`[Filter<T>]` can also be applied to the assembly. It applies to every `[AzureFunction]` class in the assembly,
so a common pipeline does not need to be repeated on each class.

```csharp
[assembly: Filter<RequestIdFilter>]
```

* Assembly-level and class-level filters are chained together in ascending `Order`. With the same `Order`, the assembly-level filter is outer.
* When the same filter type is applied to both the assembly and a class, it runs twice.
* Assembly-level filters apply to every function in the assembly, including Timer and Queue functions. A filter for
  HTTP requests should check the type of `context.Request` and pass other invocations through (`HttpRequest` for HTTP
  functions, `TimerInfo` for Timer functions, and the message `string` for Queue functions).

```csharp
internal sealed class RequestIdFilter : IFunctionFilter
{
    public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
    {
        if (context.Request is not HttpRequest request)
        {
            return next(context);
        }

        request.HttpContext.Response.Headers["X-Request-Id"] = context.FunctionContext.InvocationId;
        return next(context);
    }
}
```

* A filter type that does not implement `IFunctionFilter` on the assembly is reported once at the attribute (`AFE0201`).

### Cancellation

Filters (`FunctionInvocationContext.CancellationToken`) and handlers (a `CancellationToken` parameter) receive
`FunctionContext.CancellationToken`. The Functions host cancels it when the invocation is canceled, for example
when the client disconnects or the app restarts or stops
(see [Cancellation tokens](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide#cancellation-tokens)).

To also stop at a time limit, register `FunctionCancellationOptions` as a singleton. The token is then canceled
at `Timeout` from the start of the invocation, so that the work can stop before the platform stops it
(for example, an HTTP function must respond within 230 seconds regardless of `functionTimeout`; see
[Function app timeout duration](https://learn.microsoft.com/azure/azure-functions/functions-scale#function-app-timeout-duration)).

```csharp
[HttpEndpoint("get", "items", AuthorizationLevel.Anonymous)]
public async Task<IActionResult> List(CancellationToken cancellationToken)
{
    return Results.Ok(await data.ListAsync(cancellationToken));
}
```

```csharp
builder.Services.AddSingleton(new FunctionCancellationOptions { Timeout = TimeSpan.FromSeconds(200) });
```

* The token is created only when the class has filters or the handler takes a `CancellationToken`.
  Then `[FromBody]` also reads the body with the token.
* The default `Timeout` is `Timeout.InfiniteTimeSpan`, so the token is canceled only by the host.
* When the work stops with `OperationCanceledException`, an HTTP function returns `500`, and a Timer or Queue function fails.
  A filter can catch it and return its own response:

```csharp
internal sealed class TimeoutFilter : IFunctionFilter
{
    public async ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            context.Result = Results.StatusCode(503);
        }
    }
}
```

* Difference from AmazonLambdaExtension: the token there is canceled at the remaining time of the invocation minus
  a margin and works by default, because Lambda tells the remaining time of the invocation. Azure Functions does not
  tell it, so the time limit here is from the start of the invocation and is added only when it is registered.

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

`System.Text.Json` is used throughout, with the MVC `JsonOptions` (`Microsoft.AspNetCore.Mvc.JsonOptions`). Its default
is based on `JsonSerializerDefaults.Web` (camelCase property names on write and case-insensitive property matching on
read), with a maximum depth of 32.

* **Request body** (`[FromBody]`) is deserialized with the registered `IBodySerializer`
  (`DeserializeAsync(Stream, CancellationToken)`). The default serializer is built from the MVC `JsonOptions`.
* **Response**: `Results.Ok(value)`, a handler that returns any other object, and the other results with a body
  (`Results.Created`, `BadRequest`, `NotFound`, `Conflict`, `StatusCode(statusCode, value)`, and an `ObjectResult` that
  the app creates, such as the one of an `IBindingErrorResultFactory`) are ASP.NET Core MVC results. They are written
  with the MVC `JsonOptions` by the runtime type of the value, as `application/json; charset=utf-8`.
* As in ASP.NET Core MVC, a `string` value is written as `text/plain` (as a JSON string when the `Accept` header asks
  for `application/json`), and `Results.Ok(null)` or a handler that returns `null` responds with 204 and no body.
* An `IBodySerializer` registered before `AddAzureFunctionExtension` replaces the default one for request bodies only.
* Difference from AmazonLambdaExtension: the registered `IBodySerializer` there reads the request bodies and writes all
  the response bodies.

### Configuration

```csharp
services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
```

### Native AOT / trimming

Not supported. The isolated worker of Azure Functions (`Microsoft.Azure.Functions.Worker`) does not support Native AOT
and trimming, so this library does not support them either.
