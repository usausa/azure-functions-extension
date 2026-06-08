namespace AzureFunctionsExtension.Example.Tests;

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

// Builds the DI container and HTTP request fakes needed to drive the generated handlers,
// mirroring the registrations in the example's Program.cs.
internal static class HandlerTestHost
{
    public static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureFunctionExtension();
        services.AddSingleton<IGreetingService, GreetingService>();
        services.AddTransient<Function>();
        services.AddTransient<QueueFunction>();
        services.AddTransient<FilterFunction>();
        services.AddTransient<LoggingFilter>();
        return services.BuildServiceProvider();
    }

    public static FunctionContext CreateContext(IServiceProvider services) => new FakeFunctionContext(services);

    public static HttpRequest CreateRequest(
        IServiceProvider services,
        IDictionary<string, StringValues>? query = null,
        IDictionary<string, object?>? route = null,
        IDictionary<string, StringValues>? headers = null,
        string? body = null)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var request = httpContext.Request;

        if (query is not null)
        {
            request.Query = new QueryCollection(new Dictionary<string, StringValues>(query));
        }

        if (route is not null)
        {
            var routeValues = new RouteValueDictionary();
            foreach (var entry in route)
            {
                routeValues[entry.Key] = entry.Value;
            }

            request.RouteValues = routeValues;
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                request.Headers[header.Key] = header.Value;
            }
        }

        if (body is not null)
        {
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        return request;
    }

    // Extracts the HTTP status code from the IActionResult shapes the generator emits.
    public static int? StatusOf(IActionResult result) => result switch
    {
        global::AzureFunctionsExtension.Mvc.SystemTextJsonResult r => r.StatusCode,
        ObjectResult r => r.StatusCode,
        StatusCodeResult r => r.StatusCode,
        _ => null
    };

    public static T? ValueOf<T>(IActionResult result)
        where T : class
        => result switch
        {
            global::AzureFunctionsExtension.Mvc.SystemTextJsonResult r => r.Value as T,
            ObjectResult r => r.Value as T,
            _ => null
        };

    public static string Json(object value) => JsonSerializer.Serialize(value);
}
