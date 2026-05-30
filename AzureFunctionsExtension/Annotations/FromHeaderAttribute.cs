namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromHeaderAttribute : Attribute
{
    public string? Name { get; }

    public FromHeaderAttribute(string? name = null)
    {
        Name = name;
    }
}
