namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Method)]
public sealed class QueueEndpointAttribute : Attribute
{
    public string QueueName { get; }

    public string? Connection { get; }

    public QueueEndpointAttribute(string queueName, string? connection = null)
    {
        QueueName = queueName;
        Connection = connection;
    }
}
