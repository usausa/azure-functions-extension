namespace AzureFunctionsExtension.Example.Tests;

using AzureFunctionsExtension.Example.Functions;

public sealed class QueueFunctionHandlerTests
{
    // [QueueEndpoint] + [FromTrigger] message + [FromServices] injection
    [Fact]
    public Task ProcessMessage_Handler_ResolvesServiceAndCompletes()
    {
        var services = HandlerTestHost.CreateServices();

        return QueueFunction.ProcessMessage_Handler("hello", HandlerTestHost.CreateContext(services));
    }
}
