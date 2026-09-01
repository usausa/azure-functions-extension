namespace AzureFunctionsExtension.Filters;

using System.Threading.Tasks;

public interface IFunctionFilter
{
#pragma warning disable CA17116
    ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next);
#pragma warning restore CA17116
}
