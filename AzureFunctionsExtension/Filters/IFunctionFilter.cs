namespace AzureFunctionsExtension.Filters;

using System.Threading.Tasks;

public interface IFunctionFilter
{
    ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next);
}
