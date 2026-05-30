namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceResolverAttribute : Attribute
{
    public Type Type { get; }

    public ServiceResolverAttribute(Type type)
    {
        Type = type;
    }
}
