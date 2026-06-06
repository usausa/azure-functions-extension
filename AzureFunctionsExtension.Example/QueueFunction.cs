namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension.Annotations;

using Microsoft.Extensions.Logging;

// Demonstrates [QueueEndpoint] and [FromServices] parameter injection.
[AzureFunction]
internal sealed partial class QueueFunction
{
    private static readonly Action<ILogger, string, string, Exception?> MessageReceived =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, nameof(ProcessMessage)), "Queue message received. Queue=[{Queue}], Greeting=[{Greeting}]");

    private readonly ILogger<QueueFunction> log;

    public QueueFunction(ILogger<QueueFunction> log)
    {
        this.log = log;
    }

    [QueueEndpoint("my-queue")]
    public void ProcessMessage(
        [FromTrigger] string message,
        [FromServices] IGreetingService greeting)
    {
        MessageReceived(log, "my-queue", greeting.Greet(message), null);
    }
}
