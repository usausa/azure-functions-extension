namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromQueryAttribute : Attribute
{
    public string? Name { get; }

    public FromQueryAttribute(string? name = null)
    {
        Name = name;
    }
}
