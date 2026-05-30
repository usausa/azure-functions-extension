namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TimerEndpointAttribute : Attribute
{
    public string Schedule { get; }

    public TimerEndpointAttribute(string schedule)
    {
        Schedule = schedule;
    }
}
