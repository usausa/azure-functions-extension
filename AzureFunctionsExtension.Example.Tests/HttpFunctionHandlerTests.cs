namespace AzureFunctionsExtension.Example.Tests;

using System.Collections.Generic;

using AzureFunctionsExtension.Example.Functions;
using AzureFunctionsExtension.Example.Models;

using Microsoft.Extensions.Primitives;

public sealed class HttpFunctionHandlerTests
{
    [Fact]
    public async Task Query_Handler_ValidValues_Returns200WithSum()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["a"] = "1", ["b"] = "2" });

        var result = await HttpFunction.Query_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<QueryResponse>(result);
        Assert.NotNull(body);
        Assert.Equal(6, body.Result); // a(1) + b(2) + c(default 3)
    }

    [Fact]
    public async Task Query_Handler_InvalidScalar_Returns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["a"] = "notanumber" });

        var result = await HttpFunction.Query_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task Array_Handler_Returns200WithSum()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["a"] = "1,2,3", ["b"] = "4,5" });

        var result = await HttpFunction.Array_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<QueryResponse>(result);
        Assert.NotNull(body);
        Assert.Equal(15, body.Result); // (1+2+3) + (4+5)
    }

    [Fact]
    public async Task Body_Handler_ValidBody_Returns200()
    {
        var services = HandlerTestHost.CreateServices();
        var json = HandlerTestHost.Json(new { id = 1, name = "widget", flag = true, dateTime = "2024-01-15T00:00:00" });
        var req = HandlerTestHost.CreateRequest(services, body: json);

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<BodyResponse>(result);
        Assert.NotNull(body);
        Assert.Equal(1, body.Id);
        Assert.Equal("widget", body.Name);
    }

    [Fact]
    public async Task Body_Handler_ValidationFails_Returns400()
    {
        var services = HandlerTestHost.CreateServices();
        var json = HandlerTestHost.Json(new { id = 1, name = string.Empty }); // name is [Required]
        var req = HandlerTestHost.CreateRequest(services, body: json);

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task Body_Handler_InvalidJson_Returns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services, body: "{ this is not json");

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task GetItem_Handler_ExistingId_Returns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["id"] = "5" });

        var result = await HttpFunction.GetItem_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<ItemResponse>(result);
        Assert.NotNull(body);
        Assert.Equal(5, body.Id);
    }

    [Fact]
    public async Task GetItem_Handler_NonPositiveId_Returns404()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["id"] = "0" });

        var result = await HttpFunction.GetItem_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(404, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task GetItem_Handler_InvalidId_Returns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["id"] = "abc" });

        var result = await HttpFunction.GetItem_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task HeaderEcho_Handler_Returns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["X-Correlation-Id"] = "corr-1" });

        var result = await HttpFunction.HeaderEcho_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task Lookup_Handler_UnknownName_Returns404()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["name"] = "unknown" });

        var result = await HttpFunction.Lookup_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(404, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task Lookup_Handler_KnownName_Returns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["name"] = "widget" });

        var result = await HttpFunction.Lookup_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task Greet_Handler_ResolvesServiceAndReturns200()
    {
        var services = HandlerTestHost.CreateServices();
        var json = HandlerTestHost.Json(new { name = "World" });
        var req = HandlerTestHost.CreateRequest(services, body: json);

        var result = await HttpFunction.Greet_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<GreetResponse>(result);
        Assert.NotNull(body);
        Assert.Equal("Hello, World!", body.Message);
    }
}
