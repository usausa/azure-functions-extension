namespace AzureFunctionsExtension.Example.Tests;

public sealed class FilterFunctionHandlerTests
{
    [Fact]
    public async Task Ping_Handler_RunsThroughFilterPipelineAndReturns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(services);

        var result = await FilterFunction.Ping_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
    }
}
