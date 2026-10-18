namespace AzureFunctionsExtension.Annotations;

using AzureFunctionsExtension.Filters;

// ReSharper disable once UnusedTypeParameter
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FilterAttribute<TFilter> : Attribute
    where TFilter : IFunctionFilter
{
    public int Order { get; set; }
}
