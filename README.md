# Azure Functions Extension for .NET

[![NuGet Badge](https://buildstats.info/nuget/AzureFunctionsExtension)](https://www.nuget.org/packages/AzureFunctionsExtension/)

## What is this?

Azure Functions extension library.

## Supported features

* Model binding attribute lile [FromQuery], [FromBody]
* System.Text.Json serializer

## Binding

### Query binding

* Add [BindQuery] attribute to parameters
* Built-in types and Nullable Built-in types and TypeDescriptor and Array supported

```csharp
[FunctionName("Query")]
public IActionResult Query(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query")] HttpRequest req,
    [BindQuery] int a,
    [BindQuery] int? b,
    [BindQuery] int c = 3)
{
    return Results.Of(new QueryResponse
    {
        Result = a + (b ?? 0) + c
    });
}

[FunctionName("Array")]
public IActionResult Array(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "array")] HttpRequest req,
    [BindQuery] int[] a,
    [BindQuery] int?[] b)
{
    return Results.Of(new QueryResponse
    {
        Result = a.Sum() + b.Sum(x => x ?? 0)
    });
}
```

### Body binding

* Add [BindBody] attribute to parameters
* Serialize Body using System.Text.Json

```csharp
[FunctionName("Body")]
public IActionResult Body(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "body")] HttpRequest req,
    [BindBody] BodyRequest request)
{
    return Results.Of(new BodyResponse
    {
        Id = request.Id,
        Name = request.Name
    });
}
```

```csharp
public class BodyRequest
{
    public int Id { get; set; }

    public string Name { get; set; }
}
```

## System.Text.Json serializer

### IActionResult

* SystemTextJsonResult action result
* Results helper class

```csharp
return new SystemTextJsonResult(response);
```

```csharp
// if response is null return NotFoundResult, response is not null return SystemTextJsonResult
return Results.Of(response);
```

### Configuration

* Use AddAzureFunctionExtension() method to configure

```csharp
public sealed class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddAzureFunctionExtension(c =>
        {
            c.Options.Converters.Add(new DateTimeConverter());
        });
    }
}
```
