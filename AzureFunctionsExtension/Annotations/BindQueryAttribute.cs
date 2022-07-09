namespace AzureFunctionsExtension.Annotations;

using Microsoft.Azure.WebJobs.Description;

[Binding]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindQueryAttribute : Attribute
{
    public string? Name { get; }

    public BindQueryAttribute()
    {
    }

    public BindQueryAttribute(string name)
    {
        Name = name;
    }
}
