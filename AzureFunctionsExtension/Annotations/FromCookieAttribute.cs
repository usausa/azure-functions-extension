namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromCookieAttribute : Attribute
{
    public string Name { get; }

    public FromCookieAttribute(string name)
    {
        Name = name;
    }
}
