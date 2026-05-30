namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromBodyAttribute : Attribute
{
    public bool SkipValidate { get; set; }
}
