namespace AzureFunctionsExtension.Example.Tests;

using System.Collections.Generic;
using System.Text;

using AzureFunctionsExtension.Example.Functions;
using AzureFunctionsExtension.Example.Models;

using Microsoft.Extensions.Primitives;

public sealed class HttpFunctionHandlerTests
{
    [Fact]
    public async Task QueryHandlerValidValuesReturns200WithSum()
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
    public async Task QueryHandlerInvalidScalarReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["a"] = "notanumber" });

        var result = await HttpFunction.Query_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task ArrayHandlerReturns200WithSum()
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
    public async Task BodyHandlerValidBodyReturns200()
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
    public async Task BodyHandlerValidationFailsReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var json = HandlerTestHost.Json(new { id = 1, name = string.Empty }); // name is [Required]
        var req = HandlerTestHost.CreateRequest(services, body: json);

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task BodyHandlerInvalidJsonReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services, body: "{ this is not json");

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task BodyHandlerReadsBodyWithoutSynchronousIo()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services);
        req.Body = new SyncReadDisallowedStream(Encoding.UTF8.GetBytes(HandlerTestHost.Json(new { id = 1, name = "widget" })));

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<BodyResponse>(result);
        Assert.NotNull(body);
        Assert.Equal("widget", body.Name);
    }

    [Fact]
    public async Task GetItemHandlerExistingIdReturns200()
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
    public async Task GetItemHandlerNonPositiveIdReturns404()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["id"] = "0" });

        var result = await HttpFunction.GetItem_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(404, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task GetItemHandlerInvalidIdReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["id"] = "abc" });

        var result = await HttpFunction.GetItem_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task RouteArrayHandlerCommaSeparatedValuesReturns200WithArray()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["ids"] = "1,2,3" });

        var result = await HttpFunction.RouteArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<int[]>(result);
        Assert.NotNull(body);
        Assert.Equal([1, 2, 3], body);
    }

    [Fact]
    public async Task RouteArrayHandlerInvalidElementReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["ids"] = "1,abc" });

        var result = await HttpFunction.RouteArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task HeaderEchoHandlerReturns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["X-Correlation-Id"] = "corr-1" });

        var result = await HttpFunction.HeaderEcho_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task HeaderArrayHandlerCommaSeparatedValuesReturns200WithArray()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["X-Ids"] = "1, 2, 3" });

        var result = await HttpFunction.HeaderArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<int[]>(result);
        Assert.NotNull(body);
        Assert.Equal([1, 2, 3], body);
    }

    [Fact]
    public async Task HeaderArrayHandlerMultipleHeaderValuesReturns200WithArray()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["X-Ids"] = new(["1", "2,3"]) });

        var result = await HttpFunction.HeaderArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<int[]>(result);
        Assert.NotNull(body);
        Assert.Equal([1, 2, 3], body);
    }

    [Fact]
    public async Task HeaderArrayHandlerMissingHeaderReturns200WithEmptyArray()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services);

        var result = await HttpFunction.HeaderArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<int[]>(result);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task HeaderArrayHandlerInvalidElementReturns400()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["X-Ids"] = "1,abc" });

        var result = await HttpFunction.HeaderArray_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task LookupHandlerUnknownNameReturns404()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["name"] = "unknown" });

        var result = await HttpFunction.Lookup_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(404, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task LookupHandlerKnownNameReturns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            route: new Dictionary<string, object?> { ["name"] = "widget" });

        var result = await HttpFunction.Lookup_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public async Task GreetHandlerResolvesServiceAndReturns200()
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

    [Fact]
    public async Task GreetHandlerReadsBodyWithoutSynchronousIo()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services);
        req.Body = new SyncReadDisallowedStream(Encoding.UTF8.GetBytes(HandlerTestHost.Json(new { name = "World" })));

        var result = await HttpFunction.Greet_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        var body = HandlerTestHost.ValueOf<GreetResponse>(result);
        Assert.NotNull(body);
        Assert.Equal("Hello, World!", body.Message);
    }
}
