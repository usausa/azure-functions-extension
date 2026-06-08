namespace AzureFunctionsExtension.Filters;

using System.Threading.Tasks;

#pragma warning disable CA1711
public delegate ValueTask FunctionFilterDelegate(FunctionInvocationContext context);
#pragma warning restore CA1711
