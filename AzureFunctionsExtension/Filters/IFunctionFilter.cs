namespace AzureFunctionsExtension.Filters;

using System.Threading.Tasks;

public interface IFunctionFilter
{
#pragma warning disable CA1716
    ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next);
#pragma warning restore CA1716
}
