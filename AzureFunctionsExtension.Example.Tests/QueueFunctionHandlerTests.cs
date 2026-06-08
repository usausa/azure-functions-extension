namespace AzureFunctionsExtension.Example.Tests;

public sealed class QueueFunctionHandlerTests
{
    // [QueueEndpoint] + [FromTrigger] message + [FromServices] injection
    [Fact]
    public async Task ProcessMessage_Handler_ResolvesServiceAndCompletes()
    {
        var services = HandlerTestHost.CreateServices();

        await QueueFunction.ProcessMessage_Handler("hello", HandlerTestHost.CreateContext(services));
    }
}
