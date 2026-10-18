namespace AzureFunctionsExtension.Example.Tests;

public sealed class RequestIdFilterTests
{
    [Fact]
    public async Task AssemblyFilterAddsRequestIdToEveryHttpFunction()
    {
        var services = HandlerTestHost.CreateServices();
        var ping = HandlerTestHost.CreateRequest(services);
        var header = HandlerTestHost.CreateRequest(services);

        await FilterFunction.Ping_Handler(ping, HandlerTestHost.CreateContext(services));
        await HttpFunction.HeaderEcho_Handler(header, HandlerTestHost.CreateContext(services));

        Assert.Equal("test-invocation", ping.HttpContext.Response.Headers["X-Request-Id"].ToString());
        Assert.Equal("test-invocation", header.HttpContext.Response.Headers["X-Request-Id"].ToString());
    }
}
