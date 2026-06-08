namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromServicesAttribute : Attribute
{
    public string? Key { get; }

    public FromServicesAttribute(string? key = null)
    {
        Key = key;
    }
}
