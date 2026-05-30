namespace AzureFunctionsExtension.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FilterAttribute<TFilter> : Attribute
    where TFilter : AzureFunctionsExtension.Filters.IFunctionFilter
{
    public int Order { get; set; }
}
