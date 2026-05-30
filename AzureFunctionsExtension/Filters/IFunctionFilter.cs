namespace AzureFunctionsExtension.Filters;

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;

#pragma warning disable CA1711
public delegate ValueTask FunctionFilterDelegate(FunctionInvocationContext context);
#pragma warning restore CA1711

public interface IFunctionFilter
{
    ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next);
}

public sealed class FunctionInvocationContext
{
    public object? Request { get; init; }

    public FunctionContext FunctionContext { get; init; } = default!;

    public CancellationToken CancellationToken { get; init; }

    public object? Result { get; set; }

    private Dictionary<string, object?>? items;

    public IDictionary<string, object?> Items =>
        items ??= [];
}
