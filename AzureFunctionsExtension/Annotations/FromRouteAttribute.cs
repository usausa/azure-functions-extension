namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromRouteAttribute : Attribute
{
    public string? Name { get; }

    public FromRouteAttribute(string? name = null)
    {
        Name = name;
    }
}
